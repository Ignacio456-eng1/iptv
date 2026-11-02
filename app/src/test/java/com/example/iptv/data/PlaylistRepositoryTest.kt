package com.example.iptv.data

import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.test.runTest
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import java.io.IOException

@RunWith(AndroidJUnit4::class)
@Config(sdk = [34])
class PlaylistRepositoryTest {

    private val server = MockWebServer()
    private lateinit var repo: PlaylistRepository

    private val playlist = """
        #EXTM3U
        #EXTINF:-1 group-title="Noticias",Canal 1
        http://example.com/1.m3u8
        #EXTINF:-1 group-title="Deportes",Canal 2
        http://example.com/2.m3u8
    """.trimIndent()

    @Before fun setUp() {
        server.start()
        repo = PlaylistRepository(ApplicationProvider.getApplicationContext())
        repo.clearCache()
    }

    @After fun tearDown() = server.shutdown()

    @Test fun downloadsParsesAndCaches() = runTest {
        server.enqueue(MockResponse().setBody(playlist))
        val channels = repo.load(PlaylistSource.M3u(server.url("/lista.m3u").toString()))
        assertEquals(listOf("Canal 1", "Canal 2"), channels.map { it.name })
        assertEquals(2, repo.cached().size)
        assertTrue(server.takeRequest().getHeader("User-Agent")!!.startsWith("IptvPlayer"))
    }

    @Test fun xtreamRequestsGetPhp() = runTest {
        server.enqueue(MockResponse().setBody(playlist))
        val base = server.url("/").toString()
        repo.load(PlaylistSource.Xtream(base, "ana", "secreto"))
        val path = server.takeRequest().path!!
        assertEquals("/get.php?username=ana&password=secreto&type=m3u_plus&output=ts", path)
    }

    @Test fun httpErrorIsReported() = runTest {
        server.enqueue(MockResponse().setResponseCode(401))
        try {
            repo.load(PlaylistSource.M3u(server.url("/x.m3u").toString()))
            fail("debería fallar")
        } catch (e: IOException) {
            assertTrue(e.message!!.contains("401"))
        }
    }

    @Test fun emptyPlaylistIsAnError() = runTest {
        server.enqueue(MockResponse().setBody("#EXTM3U\n"))
        try {
            repo.load(PlaylistSource.M3u(server.url("/vacia.m3u").toString()))
            fail("debería fallar")
        } catch (e: IOException) {
            assertTrue(e.message!!.contains("no contiene canales"))
        }
    }
}
