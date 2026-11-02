package com.example.iptv.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.iptv.data.Channel
import com.example.iptv.data.PlaylistRepository
import com.example.iptv.data.PlaylistSource
import com.example.iptv.data.SettingsStore
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

const val GROUP_ALL = "Todos"
const val GROUP_FAVORITES = "★ Favoritos"

data class UiState(
    val ready: Boolean = false,
    val source: PlaylistSource? = null,
    val loading: Boolean = false,
    val error: String? = null,
    val channels: List<Channel> = emptyList(),
    val groups: List<String> = listOf(GROUP_ALL),
    val selectedGroup: String = GROUP_ALL,
    val query: String = "",
    val favorites: Set<String> = emptySet(),
    val playing: Channel? = null,
) {
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

    val state: StateFlow<UiState> = combine(local, settings.favorites) { s, favs -> s.copy(favorites = favs) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, UiState())

    init {
        viewModelScope.launch {
            val source = settings.source.first()
            if (source != null) {
                setChannels(repo.cached())
                local.update { it.copy(source = source) }
                refresh()
            }
            local.update { it.copy(ready = true) }
        }
    }

    fun connect(source: PlaylistSource) {
        viewModelScope.launch {
            local.update { it.copy(loading = true, error = null) }
            runCatching { repo.load(source) }
                .onSuccess { channels ->
                    settings.saveSource(source)
                    setChannels(channels)
                    local.update { it.copy(source = source, loading = false, selectedGroup = GROUP_ALL) }
                }
                .onFailure { e -> local.update { it.copy(loading = false, error = e.message ?: e.toString()) } }
        }
    }

    fun refresh() {
        val source = local.value.source ?: return
        viewModelScope.launch {
            local.update { it.copy(loading = true, error = null) }
            runCatching { repo.load(source) }
                .onSuccess { setChannels(it); local.update { s -> s.copy(loading = false) } }
                .onFailure { e -> local.update { it.copy(loading = false, error = e.message ?: e.toString()) } }
        }
    }

    fun logout() {
        viewModelScope.launch {
            settings.saveSource(null)
            repo.clearCache()
            local.value = UiState(ready = true)
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

    private fun setChannels(channels: List<Channel>) {
        val groups = channels.mapNotNull { it.group }.distinct()
        local.update {
            it.copy(
                channels = channels,
                groups = listOf(GROUP_ALL, GROUP_FAVORITES) + groups,
                selectedGroup = if (it.selectedGroup in groups || it.selectedGroup == GROUP_FAVORITES) it.selectedGroup else GROUP_ALL,
            )
        }
    }
}
