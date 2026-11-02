package com.example.iptv.data

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.IOException
import java.util.concurrent.TimeUnit

/** Descarga las listas, guarda una copia en disco por lista y las convierte en canales. */
class PlaylistRepository(context: Context) {

    private val dir = context.filesDir

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    private fun cacheFile(id: String): File {
        val file = File(dir, "playlist_${id.filter { it.isLetterOrDigit() || it == '-' }}.m3u")
        // Copia de la versión 0.1, que solo guardaba una lista.
        val legacy = File(dir, "playlist.m3u")
        if (id == SettingsStore.LEGACY_ID && !file.exists() && legacy.exists()) legacy.renameTo(file)
        return file
    }

    /** Canales de la última descarga correcta de esa lista (para arrancar rápido o sin red). */
    suspend fun cached(id: String): List<Channel> = withContext(Dispatchers.IO) {
        val file = cacheFile(id)
        if (file.exists()) M3uParser.parse(file.readText()) else emptyList()
    }

    /** Descarga la lista, la valida y, si tiene canales, guarda la copia con ese id. */
    suspend fun load(id: String, source: PlaylistSource): List<Channel> = withContext(Dispatchers.IO) {
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
        cacheFile(id).writeText(text)
        channels
    }

    fun clearCache(id: String) { cacheFile(id).delete() }

    companion object {
        const val USER_AGENT = "IptvPlayer/0.2 (Android)"
    }
}
