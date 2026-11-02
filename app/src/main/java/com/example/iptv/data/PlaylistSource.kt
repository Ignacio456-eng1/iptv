package com.example.iptv.data

import java.net.URLEncoder

/** De dónde sale la lista de canales. El usuario aporta siempre su propia fuente. */
sealed interface PlaylistSource {
    /** URL que devuelve una lista M3U/M3U8. */
    fun playlistUrl(): String

    data class M3u(val url: String) : PlaylistSource {
        override fun playlistUrl() = url.trim()
    }

    /** Proveedor con API Xtream Codes: se pide su lista M3U con las credenciales. */
    data class Xtream(val server: String, val username: String, val password: String) : PlaylistSource {
        override fun playlistUrl(): String {
            val base = server.trim().trimEnd('/').let {
                if (it.startsWith("http://") || it.startsWith("https://")) it else "http://$it"
            }
            val u = URLEncoder.encode(username.trim(), "UTF-8")
            val p = URLEncoder.encode(password.trim(), "UTF-8")
            return "$base/get.php?username=$u&password=$p&type=m3u_plus&output=ts"
        }
    }
}
