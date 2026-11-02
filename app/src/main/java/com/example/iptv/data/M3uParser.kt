package com.example.iptv.data

/**
 * Parser de listas M3U / M3U8 "extendidas" como las que usan los proveedores IPTV.
 *
 * Soporta:
 *  - `#EXTINF:-1 tvg-id="..." tvg-logo="..." group-title="...",Nombre`
 *  - `#EXTGRP:Grupo`
 *  - `#EXTVLCOPT:http-user-agent=...` y `#EXTVLCOPT:http-referrer=...`
 *  - Cabeceras estilo Kodi en la URL: `http://host/stream|User-Agent=xxx&Referer=yyy`
 */
object M3uParser {

    private val attrRegex = Regex("""([\w-]+)="([^"]*)"""")

    fun parse(text: String): List<Channel> {
        val channels = mutableListOf<Channel>()
        var name: String? = null
        var attrs: Map<String, String> = emptyMap()
        var group: String? = null
        val headers = mutableMapOf<String, String>()

        fun reset() {
            name = null; attrs = emptyMap(); group = null; headers.clear()
        }

        for (rawLine in text.lineSequence()) {
            val line = rawLine.trim().removePrefix("﻿")
            when {
                line.isEmpty() -> Unit
                line.startsWith("#EXTINF", ignoreCase = true) -> {
                    reset()
                    val info = line.substringAfter(':', "")
                    val commaIdx = firstCommaOutsideQuotes(info)
                    val meta = if (commaIdx >= 0) info.substring(0, commaIdx) else info
                    name = if (commaIdx >= 0) info.substring(commaIdx + 1).trim() else null
                    attrs = attrRegex.findAll(meta).associate { it.groupValues[1].lowercase() to it.groupValues[2] }
                }
                line.startsWith("#EXTGRP:", ignoreCase = true) -> group = line.substringAfter(':').trim()
                line.startsWith("#EXTVLCOPT:", ignoreCase = true) -> {
                    val opt = line.substringAfter(':')
                    val key = opt.substringBefore('=').trim().lowercase()
                    val value = opt.substringAfter('=', "").trim()
                    when (key) {
                        "http-user-agent" -> headers["User-Agent"] = value
                        "http-referrer", "http-referer" -> headers["Referer"] = value
                    }
                }
                line.startsWith("#") -> Unit // #EXTM3U y otras directivas
                else -> {
                    val (url, urlHeaders) = splitKodiHeaders(line)
                    val finalName = name?.takeIf { it.isNotBlank() }
                        ?: attrs["tvg-name"]?.takeIf { it.isNotBlank() }
                        ?: url.substringAfterLast('/')
                    channels += Channel(
                        name = finalName,
                        url = url,
                        logo = attrs["tvg-logo"]?.takeIf { it.isNotBlank() },
                        group = (attrs["group-title"] ?: group)?.takeIf { it.isNotBlank() },
                        tvgId = attrs["tvg-id"]?.takeIf { it.isNotBlank() },
                        headers = headers + urlHeaders,
                    )
                    reset()
                }
            }
        }
        return channels
    }

    /** Primera coma fuera de comillas: separa los atributos del nombre (que puede tener comas). */
    private fun firstCommaOutsideQuotes(s: String): Int {
        var inQuotes = false
        s.forEachIndexed { i, c ->
            if (c == '"') inQuotes = !inQuotes
            else if (c == ',' && !inQuotes) return i
        }
        return -1
    }

    private fun splitKodiHeaders(line: String): Pair<String, Map<String, String>> {
        val pipe = line.indexOf('|')
        if (pipe < 0) return line to emptyMap()
        val url = line.substring(0, pipe)
        val headers = line.substring(pipe + 1).split('&').mapNotNull { part ->
            val k = part.substringBefore('=', "").trim()
            val v = part.substringAfter('=', "").trim()
            if (k.isEmpty()) null else k to v
        }.toMap()
        return url to headers
    }
}
