package com.example.iptv.data

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.stringSetPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore(name = "settings")

/** Guarda la fuente configurada y los favoritos. */
class SettingsStore(private val context: Context) {

    private object Keys {
        val type = stringPreferencesKey("source_type")
        val m3uUrl = stringPreferencesKey("m3u_url")
        val xtServer = stringPreferencesKey("xt_server")
        val xtUser = stringPreferencesKey("xt_user")
        val xtPass = stringPreferencesKey("xt_pass")
        val favorites = stringSetPreferencesKey("favorites")
    }

    val source: Flow<PlaylistSource?> = context.dataStore.data.map { p ->
        when (p[Keys.type]) {
            "m3u" -> p[Keys.m3uUrl]?.let { PlaylistSource.M3u(it) }
            "xtream" -> PlaylistSource.Xtream(
                p[Keys.xtServer].orEmpty(), p[Keys.xtUser].orEmpty(), p[Keys.xtPass].orEmpty()
            )
            else -> null
        }
    }

    val favorites: Flow<Set<String>> = context.dataStore.data.map { it[Keys.favorites] ?: emptySet() }

    suspend fun saveSource(source: PlaylistSource?) {
        context.dataStore.edit { p ->
            listOf(Keys.type, Keys.m3uUrl, Keys.xtServer, Keys.xtUser, Keys.xtPass).forEach { p.remove(it) }
            when (source) {
                is PlaylistSource.M3u -> { p[Keys.type] = "m3u"; p[Keys.m3uUrl] = source.url }
                is PlaylistSource.Xtream -> {
                    p[Keys.type] = "xtream"
                    p[Keys.xtServer] = source.server
                    p[Keys.xtUser] = source.username
                    p[Keys.xtPass] = source.password
                }
                null -> Unit
            }
        }
    }

    suspend fun toggleFavorite(key: String) {
        context.dataStore.edit { p ->
            val current = p[Keys.favorites] ?: emptySet()
            p[Keys.favorites] = if (key in current) current - key else current + key
        }
    }
}
