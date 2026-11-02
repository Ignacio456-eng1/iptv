package com.example.iptv.ui

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.iptv.data.Channel
import com.example.iptv.data.PlaylistRepository
import com.example.iptv.data.PlaylistSource
import com.example.iptv.data.SavedPlaylist
import com.example.iptv.data.SettingsStore
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID

const val GROUP_ALL = "Todos"
const val GROUP_FAVORITES = "★ Favoritos"

enum class Screen { Channels, Playlists, AddPlaylist }

data class UiState(
    val ready: Boolean = false,
    val playlists: List<SavedPlaylist> = emptyList(),
    val activeId: String? = null,
    val screen: Screen = Screen.Channels,
    val loading: Boolean = false,
    val error: String? = null,
    val channels: List<Channel> = emptyList(),
    val groups: List<String> = listOf(GROUP_ALL),
    val selectedGroup: String = GROUP_ALL,
    val query: String = "",
    val favorites: Set<String> = emptySet(),
    val playing: Channel? = null,
) {
    val active: SavedPlaylist? get() = playlists.firstOrNull { it.id == activeId }

    val visibleChannels: List<Channel>
        get() = channels.asSequence()
            .filter {
                when (selectedGroup) {
                    GROUP_ALL -> true
                    GROUP_FAVORITES -> it.key in favorites
                    else -> it.group == selectedGroup
                }
            }
            .filter { query.isBlank() || it.name.contains(query, ignoreCase = true) }
            .toList()
}

class IptvViewModel(app: Application) : AndroidViewModel(app) {

    private val settings = SettingsStore(app)
    private val repo = PlaylistRepository(app)
    private val local = MutableStateFlow(UiState())
    private var loadJob: Job? = null

    val state: StateFlow<UiState> =
        combine(local, settings.playlists, settings.activeId, settings.favorites) { s, lists, active, favs ->
            s.copy(playlists = lists, activeId = active, favorites = favs)
        }.stateIn(viewModelScope, SharingStarted.Eagerly, UiState())

    init {
        viewModelScope.launch {
            val lists = settings.playlists.first()
            val activeId = settings.activeId.first()
            val active = lists.firstOrNull { it.id == activeId }
            if (active != null) {
                setChannels(repo.cached(active.id))
                reload(active)
            }
            local.update { it.copy(ready = true, screen = if (lists.isEmpty()) Screen.AddPlaylist else Screen.Channels) }
        }
    }

    /** Añade una lista nueva: solo se guarda si se descarga bien y tiene canales. */
    fun addPlaylist(name: String, source: PlaylistSource) {
        loadJob?.cancel()
        loadJob = viewModelScope.launch {
            local.update { it.copy(loading = true, error = null) }
            val id = UUID.randomUUID().toString()
            runCatching { repo.load(id, source) }
                .onSuccess { channels ->
                    val finalName = name.trim().ifEmpty { defaultName(source, state.value.playlists.size) }
                    settings.addPlaylist(SavedPlaylist(id, finalName, source))
                    setChannels(channels, resetFilters = true)
                    local.update { it.copy(loading = false, screen = Screen.Channels) }
                }
                .onFailure { e -> local.update { it.copy(loading = false, error = e.message ?: e.toString()) } }
        }
    }

    /** Cambia a otra lista guardada: muestra su copia guardada y la actualiza. */
    fun selectPlaylist(id: String) {
        val playlist = state.value.playlists.firstOrNull { it.id == id } ?: return
        loadJob?.cancel()
        loadJob = viewModelScope.launch {
            settings.setActive(id)
            setChannels(repo.cached(id), resetFilters = true)
            local.update { it.copy(screen = Screen.Channels) }
            reload(playlist)
        }
    }

    fun deletePlaylist(id: String) {
        viewModelScope.launch {
            val wasActive = state.value.activeId == id
            settings.removePlaylist(id)
            repo.clearCache(id)
            val remaining = settings.playlists.first()
            when {
                remaining.isEmpty() -> {
                    setChannels(emptyList(), resetFilters = true)
                    local.update { it.copy(screen = Screen.AddPlaylist) }
                }
                wasActive -> {
                    val next = remaining.first()
                    settings.setActive(next.id)
                    setChannels(repo.cached(next.id), resetFilters = true)
                    reload(next)
                }
            }
        }
    }

    fun renamePlaylist(id: String, name: String) {
        if (name.isBlank()) return
        viewModelScope.launch { settings.renamePlaylist(id, name.trim()) }
    }

    fun refresh() {
        state.value.active?.let { p -> viewModelScope.launch { reload(p) } }
    }

    private suspend fun reload(playlist: SavedPlaylist) {
        local.update { it.copy(loading = true, error = null) }
        runCatching { repo.load(playlist.id, playlist.source) }
            .onSuccess { if (state.value.activeId == playlist.id) setChannels(it); local.update { s -> s.copy(loading = false) } }
            .onFailure { e -> local.update { it.copy(loading = false, error = e.message ?: e.toString()) } }
    }

    fun showPlaylists() = local.update { it.copy(screen = Screen.Playlists, error = null) }
    fun showAddPlaylist() = local.update { it.copy(screen = Screen.AddPlaylist, error = null) }

    /** Atrás desde "Mis listas" o "Añadir lista". */
    fun back() = local.update {
        when {
            it.screen == Screen.AddPlaylist && state.value.playlists.isNotEmpty() -> it.copy(screen = Screen.Playlists, error = null)
            it.screen == Screen.Playlists -> it.copy(screen = Screen.Channels)
            else -> it
        }
    }

    fun selectGroup(group: String) = local.update { it.copy(selectedGroup = group) }
    fun setQuery(q: String) = local.update { it.copy(query = q) }
    fun dismissError() = local.update { it.copy(error = null) }
    fun toggleFavorite(channel: Channel) = viewModelScope.launch { settings.toggleFavorite(channel.key) }

    fun play(channel: Channel) = local.update { it.copy(playing = channel) }
    fun stop() = local.update { it.copy(playing = null) }

    /** Cambia de canal dentro de la lista visible (+1 siguiente, -1 anterior). */
    fun zap(delta: Int) {
        val s = state.value
        val list = s.visibleChannels.ifEmpty { s.channels }
        val current = s.playing ?: return
        if (list.isEmpty()) return
        val idx = list.indexOfFirst { it.key == current.key }.coerceAtLeast(0)
        val next = list[(idx + delta).mod(list.size)]
        local.update { it.copy(playing = next) }
    }

    private fun setChannels(channels: List<Channel>, resetFilters: Boolean = false) {
        val groups = channels.mapNotNull { it.group }.distinct()
        local.update {
            val keep = !resetFilters && (it.selectedGroup in groups || it.selectedGroup == GROUP_FAVORITES)
            it.copy(
                channels = channels,
                groups = listOf(GROUP_ALL, GROUP_FAVORITES) + groups,
                selectedGroup = if (keep) it.selectedGroup else GROUP_ALL,
                query = if (resetFilters) "" else it.query,
            )
        }
    }

    companion object {
        /** Nombre por defecto: el dominio de la URL o "Lista N". */
        fun defaultName(source: PlaylistSource, existing: Int): String {
            val host = when (source) {
                is PlaylistSource.M3u -> runCatching { Uri.parse(source.url).host }.getOrNull()
                is PlaylistSource.Xtream -> runCatching { Uri.parse(source.playlistUrl()).host }.getOrNull()
            }
            return host?.takeIf { it.isNotBlank() } ?: "Lista ${existing + 1}"
        }
    }
}
