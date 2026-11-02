package com.example.iptv.ui

import android.view.WindowManager
import androidx.annotation.OptIn
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.ForwardingPlayer
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.ui.PlayerView
import com.example.iptv.data.Channel
import com.example.iptv.data.PlaylistRepository
import kotlinx.coroutines.delay

@OptIn(UnstableApi::class)
@Composable
fun PlayerScreen(
    channel: Channel,
    onPrev: () -> Unit,
    onNext: () -> Unit,
) {
    val context = LocalContext.current
    var buffering by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var showName by remember { mutableStateOf(true) }

    val httpFactory = remember {
        DefaultHttpDataSource.Factory()
            .setUserAgent(PlaylistRepository.USER_AGENT)
            .setAllowCrossProtocolRedirects(true)
            .setConnectTimeoutMs(15_000)
            .setReadTimeoutMs(30_000)
    }

    val player = remember {
        ExoPlayer.Builder(context)
            .setMediaSourceFactory(DefaultMediaSourceFactory(httpFactory))
            .build()
            .apply { playWhenReady = true }
    }

    val prev by rememberUpdatedState(onPrev)
    val next by rememberUpdatedState(onNext)
    val zappingPlayer = remember {
        object : ForwardingPlayer(player) {
            override fun getAvailableCommands(): Player.Commands =
                super.getAvailableCommands().buildUpon()
                    .addAll(Player.COMMAND_SEEK_TO_NEXT, Player.COMMAND_SEEK_TO_PREVIOUS)
                    .build()
            override fun isCommandAvailable(command: Int): Boolean =
                command == Player.COMMAND_SEEK_TO_NEXT || command == Player.COMMAND_SEEK_TO_PREVIOUS ||
                    super.isCommandAvailable(command)
            override fun seekToNext() = next()
            override fun seekToPrevious() = prev()
        }
    }

    // Cambiar de canal: aplicar cabeceras de la lista y cargar el stream.
    LaunchedEffect(channel.key) {
        error = null
        buffering = true
        showName = true
        val headers = channel.headers.toMutableMap()
        headers.remove("User-Agent")?.let { httpFactory.setUserAgent(it) }
            ?: httpFactory.setUserAgent(PlaylistRepository.USER_AGENT)
        httpFactory.setDefaultRequestProperties(headers)
        player.setMediaItem(MediaItem.fromUri(channel.url))
        player.prepare()
        delay(4_000)
        showName = false
    }

    DisposableEffect(Unit) {
        val listener = object : Player.Listener {
            override fun onPlaybackStateChanged(state: Int) {
                buffering = state == Player.STATE_BUFFERING
            }
            override fun onPlayerError(e: PlaybackException) {
                buffering = false
                error = "No se pudo reproducir el canal (${e.errorCodeName})"
            }
        }
        player.addListener(listener)
        val window = (context as? android.app.Activity)?.window
        window?.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        onDispose {
            player.removeListener(listener)
            player.release()
            window?.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        }
    }

    Box(Modifier.fillMaxSize().background(Color.Black)) {
        AndroidView(
            factory = { ctx ->
                PlayerView(ctx).apply {
                    // Los botones anterior/siguiente del controlador cambian de canal.
                    this.player = zappingPlayer
                    useController = true
                    setShowNextButton(true)
                    setShowPreviousButton(true)
                    keepScreenOn = true
                    requestFocus()
                }
            },
            modifier = Modifier.fillMaxSize(),
        )

        if (buffering && error == null) {
            CircularProgressIndicator(Modifier.align(Alignment.Center))
        }

        if (showName || error != null) {
            Column(
                Modifier.align(Alignment.TopStart).padding(24.dp)
                    .background(Color(0xAA000000), MaterialTheme.shapes.medium)
                    .padding(horizontal = 16.dp, vertical = 10.dp),
            ) {
                Text(channel.name, style = MaterialTheme.typography.titleLarge, color = Color.White)
                error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            }
        }
    }
}
