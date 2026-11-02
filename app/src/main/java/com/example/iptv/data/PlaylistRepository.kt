package com.example.iptv.data

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.IOException
import java.util.concurrent.TimeUnit

/** Descarga la lista, la cachea en disco y la convierte en canales. */
class PlaylistRepository(context: Context) {

    private val cacheFile = File(context.filesDir, "playlist.m3u")

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    /** Lista cacheada de la última descarga correcta (para arrancar rápido o sin red). */
    suspend fun cached(): List<Channel> = withContext(Dispatchers.IO) {
        if (cacheFile.exists()) M3uParser.parse(cacheFile.readText()) else emptyList()
    }

    suspend fun load(source: PlaylistSource): List<Channel> = withContext(Dispatchers.IO) {
        val request = Request.Builder()
            .url(source.playlistUrl())
            .header("User-Agent", USER_AGENT)
            .build()
        val text = client.newCall(request).execute().use { resp ->
            if (!resp.isSuccessful) throw IOException("El servidor respondió ${resp.code}")
            resp.body?.string() ?: throw IOException("Respuesta vacía")
        }
        val channels = M3uParser.parse(text)
        if (channels.isEmpty()) throw IOException("La lista no contiene canales válidos")
        cacheFile.writeText(text)
        channels
    }

    fun clearCache() { cacheFile.delete() }

    companion object {
        const val USER_AGENT = "IptvPlayer/0.1 (Android)"
    }
}
