package com.example.iptv.data

import java.text.Normalizer

/** Un canal de una lista IPTV. */
data class Channel(
    val name: String,
    val url: String,
    val logo: String? = null,
    val group: String? = null,
    val tvgId: String? = null,
    /** Cabeceras HTTP extra que pide la lista (User-Agent, Referer...). */
    val headers: Map<String, String> = emptyMap(),
    /** Número del canal (su posición en la lista, empezando en 1). 0 = sin número. */
    val number: Int = 0,
) {
    /** Identificador estable para favoritos y recientes. */
    val key: String get() = url

    /** Nombre sin tildes y en minúsculas, para buscar ("clasico" encuentra "Clásico"). */
    val searchName: String by lazy { normalizeForSearch(name) }
}

/** Quita tildes/diéresis y pasa a minúsculas. */
fun normalizeForSearch(text: String): String =
    Normalizer.normalize(text, Normalizer.Form.NFD)
        .replace(Regex("\\p{Mn}+"), "")
        .lowercase()
        .trim()
