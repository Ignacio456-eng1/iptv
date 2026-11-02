package com.example.iptv.ui

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.iptv.data.PlaylistSource
import com.example.iptv.data.SavedPlaylist
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(AndroidJUnit4::class)
@Config(sdk = [34])
class PlaylistsScreenTest {

    @get:Rule val rule = createComposeRule()

    private val lists = listOf(
        SavedPlaylist("a", "Casa", PlaylistSource.M3u("http://e.com/a.m3u")),
        SavedPlaylist("b", "Deportes", PlaylistSource.Xtream("http://srv:8080", "ana", "x")),
    )

    @Test fun selectRenameDeleteAndAdd() {
        var selected: String? = null
        var renamed: Pair<String, String>? = null
        var deleted: String? = null
        var added = false
        rule.setContent {
            PlaylistsScreen(
                playlists = lists, activeId = "a",
                onSelect = { selected = it }, onAdd = { added = true },
                onRename = { id, n -> renamed = id to n }, onDelete = { deleted = it },
                onBack = {},
            )
        }

        rule.onNodeWithText("Casa").assertExists()
        rule.onNodeWithText("Xtream · ana @ http://srv:8080").assertExists()
        rule.onNodeWithContentDescription("Lista activa").assertExists()

        rule.onNodeWithText("Deportes").performClick()
        assertEquals("b", selected)

        rule.onNodeWithContentDescription("Renombrar Deportes").performClick()
        rule.onNodeWithText("Nombre").performTextClearance()
        rule.onNodeWithText("Nombre").performTextInput("Fútbol")
        rule.onNodeWithText("Guardar").performClick()
        assertEquals("b" to "Fútbol", renamed)

        rule.onNodeWithContentDescription("Borrar Casa").performClick()
        rule.onNodeWithText("Borrar").performClick() // confirmar en el diálogo
        assertEquals("a", deleted)

        rule.onNodeWithText("Añadir lista").performClick()
        assertTrue(added)
    }
}
