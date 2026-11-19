package com.example.iptv.ui

import com.example.iptv.data.Channel
import org.junit.Assert.assertEquals
import org.junit.Test

class UiStateTest {

    private val channels = listOf(
        Channel("Noticias 24", "http://e.com/1", group = "Noticias", number = 1),
        Channel("Deportes Uno", "http://e.com/2", group = "Deportes", number = 2),
        Channel("Cine Clásico", "http://e.com/3", group = "Cine", number = 3),
        Channel("CANAL ÑANDÚ", "http://e.com/4", group = "Cine", number = 4),
    )

    private fun names(s: UiState) = s.visibleChannels.map { it.name }

    @Test fun searchIgnoresAccentsAndCase() {
        val s = UiState(channels = channels)
        assertEquals(listOf("Cine Clásico"), names(s.copy(query = "clasico")))
        assertEquals(listOf("Cine Clásico"), names(s.copy(query = "CLÁSICO")))
        assertEquals(listOf("CANAL ÑANDÚ"), names(s.copy(query = "nandu")))
        assertEquals(4, s.copy(query = "  ").visibleChannels.size)
    }

    @Test fun recentGroupKeepsRecencyOrderAndSkipsMissingChannels() {
        val s = UiState(
            channels = channels,
            selectedGroup = GROUP_RECENT,
            recent = listOf("http://e.com/3", "http://ya-no-existe", "http://e.com/1"),
        )
        assertEquals(listOf("Cine Clásico", "Noticias 24"), names(s))
    }

    @Test fun groupAndFavoritesFilters() {
        val s = UiState(channels = channels, favorites = setOf("http://e.com/2"))
        assertEquals(listOf("Cine Clásico", "CANAL ÑANDÚ"), names(s.copy(selectedGroup = "Cine")))
        assertEquals(listOf("Deportes Uno"), names(s.copy(selectedGroup = GROUP_FAVORITES)))
        assertEquals(listOf("CANAL ÑANDÚ"), names(s.copy(selectedGroup = "Cine", query = "ñan")))
    }
}
