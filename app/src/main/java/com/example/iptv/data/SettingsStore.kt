package com.example.iptv.data

import android.content.Context
import androidx.datastore.preferences.core.MutablePreferences
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.stringSetPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore(name = "settings")

/** Guarda las listas del usuario, cuál está activa y los favoritos. */
class SettingsStore(private val context: Context) {

    private object Keys {
        val playlists = stringPreferencesKey("playlists")
        val activeId = stringPreferencesKey("active_playlist")
        val favorites = stringSetPreferencesKey("favorites")
        /** Canales vistos hace poco, el más reciente primero, separados por saltos de línea. */
        val recent = stringPreferencesKey("recent_channels")
        /** Listas incluidas en la app que el usuario ha borrado (no se vuelven a añadir). */
        val dismissedPresets = stringSetPreferencesKey("dismissed_presets")

        // Versión 0.1: solo había una lista. Se migra automáticamente.
        val legacyType = stringPreferencesKey("source_type")
        val legacyUrl = stringPreferencesKey("m3u_url")
        val legacyServer = stringPreferencesKey("xt_server")
        val legacyUser = stringPreferencesKey("xt_user")
        val legacyPass = stringPreferencesKey("xt_pass")
        val legacy = listOf(legacyType, legacyUrl, legacyServer, legacyUser, legacyPass)
    }

    val playlists: Flow<List<SavedPlaylist>> = context.dataStore.data.map { readPlaylists(it) }

    val activeId: Flow<String?> = context.dataStore.data.map { p ->
        val lists = readPlaylists(p)
        p[Keys.activeId]?.takeIf { id -> lists.any { it.id == id } } ?: lists.firstOrNull()?.id
    }

    val favorites: Flow<Set<String>> = context.dataStore.data.map { it[Keys.favorites] ?: emptySet() }

    val recent: Flow<List<String>> = context.dataStore.data.map { decodeRecent(it[Keys.recent]) }

    /** Apunta un canal como visto: pasa a ser el primero de "Recientes". */
    suspend fun addRecent(key: String) = context.dataStore.edit { p ->
        val list = listOf(key) + decodeRecent(p[Keys.recent]).filterNot { it == key }
        p[Keys.recent] = list.take(MAX_RECENT).joinToString("\n")
    }

    private fun decodeRecent(raw: String?): List<String> =
        raw?.split('\n')?.filter { it.isNotBlank() } ?: emptyList()

    /** Añade una lista nueva y la deja activa. */
    suspend fun addPlaylist(playlist: SavedPlaylist) = context.dataStore.edit { p ->
        writePlaylists(p, readPlaylists(p).filterNot { it.id == playlist.id } + playlist)
        p[Keys.activeId] = playlist.id
    }

    suspend fun renamePlaylist(id: String, name: String) = context.dataStore.edit { p ->
        writePlaylists(p, readPlaylists(p).map { if (it.id == id) it.copy(name = name) else it })
    }

    /**
     * Añade las listas incluidas en la app (assets/presets.json) que aún no estén y que el usuario no haya borrado.
     * Si no había ninguna lista activa, la primera pasa a serlo.
     */
    suspend fun addPresets(presets: List<SavedPlaylist>) = context.dataStore.edit { p ->
        val current = readPlaylists(p)
        val dismissed = p[Keys.dismissedPresets] ?: emptySet()
        val missing = presets.filter { pr -> pr.id !in dismissed && current.none { it.id == pr.id } }
        if (missing.isEmpty()) return@edit
        val all = current + missing
        writePlaylists(p, all)
        if (p[Keys.activeId]?.let { id -> all.any { it.id == id } } != true) p[Keys.activeId] = all.first().id
    }

    suspend fun removePlaylist(id: String) = context.dataStore.edit { p ->
        if (id.startsWith(PRESET_PREFIX)) p[Keys.dismissedPresets] = (p[Keys.dismissedPresets] ?: emptySet()) + id
        val remaining = readPlaylists(p).filterNot { it.id == id }
        writePlaylists(p, remaining)
        if (p[Keys.activeId] == id) {
            remaining.firstOrNull()?.let { p[Keys.activeId] = it.id } ?: p.remove(Keys.activeId)
        }
    }

    suspend fun setActive(id: String) = context.dataStore.edit { p -> p[Keys.activeId] = id }

    suspend fun toggleFavorite(key: String) = context.dataStore.edit { p ->
        val current = p[Keys.favorites] ?: emptySet()
        p[Keys.favorites] = if (key in current) current - key else current + key
    }

    private fun readPlaylists(p: Preferences): List<SavedPlaylist> {
        p[Keys.playlists]?.let { return PlaylistCodec.decode(it) }
        return listOfNotNull(legacyPlaylist(p))
    }

    private fun writePlaylists(p: MutablePreferences, list: List<SavedPlaylist>) {
        p[Keys.playlists] = PlaylistCodec.encode(list)
        Keys.legacy.forEach { p.remove(it) }
    }

    private fun legacyPlaylist(p: Preferences): SavedPlaylist? {
        val source = when (p[Keys.legacyType]) {
            "m3u" -> p[Keys.legacyUrl]?.let { PlaylistSource.M3u(it) }
            "xtream" -> PlaylistSource.Xtream(
                p[Keys.legacyServer].orEmpty(), p[Keys.legacyUser].orEmpty(), p[Keys.legacyPass].orEmpty()
            )
            else -> null
        } ?: return null
        return SavedPlaylist(LEGACY_ID, "Mi lista", source)
    }

    companion object {
        const val LEGACY_ID = "legacy"
        const val MAX_RECENT = 20
        const val PRESET_PREFIX = "preset-"
    }
}
