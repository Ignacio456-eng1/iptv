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

    @Test fun recentChannelsNewestFirstWithoutDuplicates() = runTest {
        val store = SettingsStore(ApplicationProvider.getApplicationContext())
        store.addRecent("http://e.com/1")
        store.addRecent("http://e.com/2")
        store.addRecent("http://e.com/1")
        assertEquals(listOf("http://e.com/1", "http://e.com/2"), store.recent.first())

        repeat(30) { store.addRecent("http://e.com/x$it") }
        val recent = store.recent.first()
        assertEquals(SettingsStore.MAX_RECENT, recent.size)
        assertEquals("http://e.com/x29", recent.first())
    }

    @Test fun presetsAddedOnceAndNotRestoredAfterDelete() = runTest {
        val store = SettingsStore(ApplicationProvider.getApplicationContext())
        store.playlists.first().forEach { store.removePlaylist(it.id) }
        val json = """[{"id":"43","name":"43","type":"xtream","server":"http://srv:8080","username":"u","password":"p"},
            {"id":"m","name":"M3U","type":"m3u","url":"http://e.com/l.m3u"}]"""
        val presets = Presets.parse(json)
        assertEquals(listOf("preset-43", "preset-m"), presets.map { it.id })

        store.addPresets(presets)
        assertEquals(listOf("43", "M3U"), store.playlists.first().map { it.name })
        assertEquals("preset-43", store.activeId.first())

        store.addPresets(presets) // no se duplican
        assertEquals(2, store.playlists.first().size)

        store.removePlaylist("preset-43")
        store.addPresets(presets) // la borrada no vuelve
        assertEquals(listOf("preset-m"), store.playlists.first().map { it.id })
        store.removePlaylist("preset-m")
    }
}
