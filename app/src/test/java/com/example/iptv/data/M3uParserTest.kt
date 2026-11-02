package com.example.iptv.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class M3uParserTest {

    @Test
    fun parsesExtendedAttributes() {
        val text = """
            #EXTM3U x-tvg-url="http://example.com/epg.xml"
            #EXTINF:-1 tvg-id="news.es" tvg-logo="http://example.com/logo.png" group-title="Noticias",Canal Noticias, HD
            http://example.com/live/1.m3u8
            #EXTINF:-1,Sin grupo
            http://example.com/live/2.ts
        """.trimIndent()

        val ch = M3uParser.parse(text)
        assertEquals(2, ch.size)
        assertEquals("Canal Noticias, HD", ch[0].name)
        assertEquals("news.es", ch[0].tvgId)
        assertEquals("http://example.com/logo.png", ch[0].logo)
        assertEquals("Noticias", ch[0].group)
        assertEquals("http://example.com/live/1.m3u8", ch[0].url)
        assertEquals("Sin grupo", ch[1].name)
        assertNull(ch[1].group)
    }

    @Test
    fun parsesHeadersAndExtgrp() {
        val text = """
            #EXTM3U
            #EXTINF:-1,Uno
            #EXTGRP:Deportes
            #EXTVLCOPT:http-user-agent=MiAgente/1.0
            http://example.com/a.m3u8
            #EXTINF:-1 group-title="Cine",Dos
            http://example.com/b.m3u8|User-Agent=Otro&Referer=http://ref.example.com/
        """.trimIndent()

        val ch = M3uParser.parse(text)
        assertEquals("Deportes", ch[0].group)
        assertEquals(mapOf("User-Agent" to "MiAgente/1.0"), ch[0].headers)
        assertEquals("http://example.com/b.m3u8", ch[1].url)
        assertEquals("Otro", ch[1].headers["User-Agent"])
        assertEquals("http://ref.example.com/", ch[1].headers["Referer"])
        // Las cabeceras de un canal no se arrastran al siguiente.
        assertEquals(1, ch[0].headers.size)
    }

    @Test
    fun xtreamBuildsPlaylistUrl() {
        val url = PlaylistSource.Xtream("example.com:8080/", "user", "p&ss").playlistUrl()
        assertEquals("http://example.com:8080/get.php?username=user&password=p%26ss&type=m3u_plus&output=ts", url)
    }
}
