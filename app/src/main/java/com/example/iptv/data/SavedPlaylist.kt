package com.example.iptv.data

import org.json.JSONArray
import org.json.JSONObject

/** Una lista guardada por el usuario. */
data class SavedPlaylist(
    val id: String,
    val name: String,
    val source: PlaylistSource,
) {
    /** Texto corto para mostrar debajo del nombre. */
    val description: String
        get() = when (source) {
            is PlaylistSource.M3u -> source.url
            is PlaylistSource.Xtream -> "Xtream · ${source.username} @ ${source.server}"
        }
}

/** Conversión a/desde JSON para guardarlas en DataStore. */
internal object PlaylistCodec {

    fun encode(list: List<SavedPlaylist>): String = JSONArray().apply {
        list.forEach { p ->
            put(JSONObject().apply {
                put("id", p.id)
                put("name", p.name)
                when (val s = p.source) {
                    is PlaylistSource.M3u -> { put("type", "m3u"); put("url", s.url) }
                    is PlaylistSource.Xtream -> {
                        put("type", "xtream")
                        put("server", s.server); put("username", s.username); put("password", s.password)
                    }
                }
            })
        }
    }.toString()

    fun decode(json: String?): List<SavedPlaylist> {
        if (json.isNullOrBlank()) return emptyList()
        return runCatching {
            val arr = JSONArray(json)
            (0 until arr.length()).mapNotNull { i ->
                val o = arr.getJSONObject(i)
                val source = when (o.optString("type")) {
                    "m3u" -> PlaylistSource.M3u(o.getString("url"))
                    "xtream" -> PlaylistSource.Xtream(
                        o.getString("server"), o.getString("username"), o.getString("password")
                    )
                    else -> null
                } ?: return@mapNotNull null
                SavedPlaylist(o.getString("id"), o.getString("name"), source)
            }
        }.getOrDefault(emptyList())
    }
}
