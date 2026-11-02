package com.example.iptv.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.lifecycle.compose.collectAsStateWithLifecycle

private val colors = darkColorScheme(
    primary = Color(0xFF8C9EFF),
    secondary = Color(0xFF80CBC4),
    background = Color(0xFF101014),
    surface = Color(0xFF17171D),
    surfaceVariant = Color(0xFF24242C),
)

@Composable
fun IptvApp(vm: IptvViewModel) {
    val state by vm.state.collectAsStateWithLifecycle()

    MaterialTheme(colorScheme = colors) {
        Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
            val playing = state.playing
            when {
                !state.ready -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
                playing != null -> {
                    BackHandler { vm.stop() }
                    PlayerScreen(
                        channel = playing,
                        onPrev = { vm.zap(-1) },
                        onNext = { vm.zap(+1) },
                    )
                }
                state.screen == Screen.AddPlaylist || state.playlists.isEmpty() -> {
                    val canCancel = state.playlists.isNotEmpty()
                    if (canCancel) BackHandler { vm.back() }
                    SetupScreen(
                        loading = state.loading,
                        error = state.error,
                        onConnect = vm::addPlaylist,
                        onCancel = if (canCancel) vm::back else null,
                    )
                }
                state.screen == Screen.Playlists -> {
                    BackHandler { vm.back() }
                    PlaylistsScreen(
                        playlists = state.playlists,
                        activeId = state.activeId,
                        onSelect = vm::selectPlaylist,
                        onAdd = vm::showAddPlaylist,
                        onRename = vm::renamePlaylist,
                        onDelete = vm::deletePlaylist,
                        onBack = vm::back,
                    )
                }
                else -> ChannelsScreen(
                    state = state,
                    onSelectGroup = vm::selectGroup,
                    onQuery = vm::setQuery,
                    onPlay = vm::play,
                    onToggleFavorite = { vm.toggleFavorite(it) },
                    onRefresh = vm::refresh,
                    onShowPlaylists = vm::showPlaylists,
                    onDismissError = vm::dismissError,
                )
            }
        }
    }
}
