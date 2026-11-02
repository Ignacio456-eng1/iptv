package com.example.iptv.ui

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.iptv.data.Channel
import com.example.iptv.data.PlaylistSource
import com.example.iptv.data.SavedPlaylist
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(AndroidJUnit4::class)
@Config(sdk = [34])
class ScreensTest {

    @get:Rule val rule = createComposeRule()

    private val channels = listOf(
        Channel("Noticias 24", "http://e.com/1", group = "Noticias"),
        Channel("Deportes Uno", "http://e.com/2", group = "Deportes"),
        Channel("Cine Clásico", "http://e.com/3", group = "Cine"),
    )

    @Test fun setupButtonEnabledOnlyWithUrl() {
        var connected: PlaylistSource? = null
        rule.setContent { SetupScreen(loading = false, error = null, onConnect = { _, src -> connected = src }) }

        rule.onNodeWithText("Cargar canales").assertIsNotEnabled()
        rule.onNodeWithText("URL de la lista (http://…/lista.m3u)").performTextInput("http://e.com/lista.m3u")
        rule.onNodeWithText("Cargar canales").performScrollTo().assertIsEnabled().performClick()
        assertEquals(PlaylistSource.M3u("http://e.com/lista.m3u"), connected)
    }

    @Test fun setupXtreamNeedsAllFields() {
        var connected: PlaylistSource? = null
        rule.setContent { SetupScreen(loading = false, error = null, onConnect = { _, src -> connected = src }) }

        rule.onNodeWithText("Xtream Codes").performClick()
        rule.onNodeWithText("Servidor (http://host:puerto)").performTextInput("http://srv:8080")
        rule.onNodeWithText("Usuario").performTextInput("ana")
        rule.onNodeWithText("Cargar canales").assertIsNotEnabled()
        rule.onNodeWithText("Contraseña").performScrollTo().performTextInput("secreto")
        rule.onNodeWithText("Cargar canales").performScrollTo().assertIsEnabled().performClick()
        assertEquals(PlaylistSource.Xtream("http://srv:8080", "ana", "secreto"), connected)
    }

    @Test fun channelListFiltersByGroupSearchAndFavorites() {
        var state by mutableStateOf(
            UiState(
                ready = true,
                playlists = listOf(SavedPlaylist("p1", "Mi lista", PlaylistSource.M3u("http://e.com/lista.m3u"))),
                activeId = "p1",
                channels = channels,
                groups = listOf(GROUP_ALL, GROUP_FAVORITES, "Noticias", "Deportes", "Cine"),
            )
        )
        var played: Channel? = null
        rule.setContent {
            ChannelsScreen(
                state = state,
                onSelectGroup = { state = state.copy(selectedGroup = it) },
                onQuery = { state = state.copy(query = it) },
                onPlay = { played = it },
                onToggleFavorite = { ch ->
                    state = state.copy(favorites = state.favorites + ch.key)
                },
                onRefresh = {}, onShowPlaylists = {}, onDismissError = {},
            )
        }

        rule.onNodeWithText("3 canales").assertExists()
        rule.onNodeWithText("Mi lista").assertExists()
        rule.onNodeWithText("Mis listas (1)").assertExists()

        // Filtro por grupo
        rule.onAllNodesWithText("Deportes")[0].performClick()
        rule.onNodeWithText("1 canales").assertExists()
        rule.onNodeWithText("Deportes Uno").assertExists()
        rule.onNodeWithText("Noticias 24").assertDoesNotExist()

        // Búsqueda
        rule.onAllNodesWithText(GROUP_ALL)[0].performClick()
        rule.onNodeWithText("Buscar canal").performTextInput("cine")
        rule.onNodeWithText("Cine Clásico").assertExists()
        rule.onNodeWithText("Deportes Uno").assertDoesNotExist()

        // Favorito y reproducción
        rule.onNodeWithContentDescription("Añadir a favoritos").performClick()
        rule.onNodeWithContentDescription("Quitar de favoritos").assertExists()
        rule.onNodeWithText("Cine Clásico").performClick()
        assertEquals("http://e.com/3", played?.url)
    }
}
