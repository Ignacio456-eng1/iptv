package com.example.iptv.data

import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(AndroidJUnit4::class)
@Config(sdk = [34])
class SettingsStoreTest {

    private val a = SavedPlaylist("a", "Casa", PlaylistSource.M3u("http://e.com/a.m3u"))
    private val b = SavedPlaylist("b", "Deportes", PlaylistSource.Xtream("http://srv:8080", "ana", "p\"ss"))

    @Test fun codecRoundTrip() {
        val json = PlaylistCodec.encode(listOf(a, b))
        assertEquals(listOf(a, b), PlaylistCodec.decode(json))
        assertEquals(emptyList<SavedPlaylist>(), PlaylistCodec.decode("no es json"))
    }

    @Test fun savesSeveralListsAndSwitchesActive() = runTest {
        val store = SettingsStore(ApplicationProvider.getApplicationContext())
        store.playlists.first().forEach { store.removePlaylist(it.id) }

        store.addPlaylist(a)
        store.addPlaylist(b)
        assertEquals(listOf("Casa", "Deportes"), store.playlists.first().map { it.name })
        assertEquals("b", store.activeId.first()) // la última añadida queda activa

        store.setActive("a")
        assertEquals("a", store.activeId.first())

        store.renamePlaylist("b", "Fútbol")
        assertEquals("Fútbol", store.playlists.first().first { it.id == "b" }.name)

        store.removePlaylist("a")
        assertEquals(listOf("b"), store.playlists.first().map { it.id })
        assertEquals("b", store.activeId.first()) // al borrar la activa pasa a otra

        store.removePlaylist("b")
        assertEquals(emptyList<SavedPlaylist>(), store.playlists.first())
        assertNull(store.activeId.first())
    }
}
