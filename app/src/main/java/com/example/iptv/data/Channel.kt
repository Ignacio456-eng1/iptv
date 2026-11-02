package com.example.iptv.data

/** Un canal de una lista IPTV. */
data class Channel(
    val name: String,
    val url: String,
    val logo: String? = null,
    val group: String? = null,
    val tvgId: String? = null,
    /** Cabeceras HTTP extra que pide la lista (User-Agent, Referer...). */
    val headers: Map<String, String> = emptyMap(),
) {
    /** Identificador estable para favoritos. */
    val key: String get() = url
}
