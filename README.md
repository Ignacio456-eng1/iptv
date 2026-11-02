# IPTV Player (Android / Android TV)

Reproductor IPTV para Android y Android TV. **No incluye canales**: el usuario añade su propia lista
(la que le da su proveedor IPTV o una lista pública y legal).

## Qué hace
- Carga listas **M3U / M3U8** por URL o con credenciales **Xtream Codes** (servidor, usuario, contraseña).
- Guarda **varias listas** con nombre: en "Mis listas" puedes cambiar de una a otra, renombrarlas y borrarlas.
- Lista de canales con logos, **grupos**, **búsqueda** y **favoritos**.
- Reproducción con **Media3 / ExoPlayer**: HLS, DASH, MPEG‑TS y progresivo.
- Respeta `#EXTVLCOPT:http-user-agent`, `http-referrer` y cabeceras estilo Kodi (`url|User-Agent=…`).
- Android TV: aparece en el launcher de la tele, navegación con mando (D‑pad) y **CH+/CH‑** para cambiar de canal.
  En el reproductor, los botones anterior/siguiente también cambian de canal.
- Guarda una copia de cada lista para arrancar rápido.
- El APK de pruebas se firma siempre con la misma clave (`app/iptv-debug.keystore`), así que las versiones nuevas se instalan encima.

## Stack
Kotlin · Jetpack Compose (Material 3) · Media3 ExoPlayer · OkHttp · DataStore · Coil. minSdk 23, targetSdk 35.

## Compilar
- **Android Studio**: abre la carpeta y pulsa Run.
- **Línea de comandos**: `./gradlew assembleDebug` → `app/build/outputs/apk/debug/app-debug.apk`
- **GitHub Actions**: al subir el repo, el workflow `Build APK` genera el APK como artefacto descargable.

Tests (parser, descarga de listas con servidor simulado e interfaz con Robolectric): `./gradlew testDebugUnitTest`.

## Estructura
```
app/src/main/java/com/example/iptv/
  MainActivity.kt          # entrada + teclas CH+/CH- del mando
  data/M3uParser.kt        # parser M3U extendido
  data/PlaylistSource.kt   # M3U por URL o Xtream Codes
  data/PlaylistRepository.kt  # descarga + caché
  data/SettingsStore.kt    # fuente y favoritos (DataStore)
  ui/IptvViewModel.kt      # estado: canales, grupos, búsqueda, reproducción
  ui/SetupScreen.kt        # pantalla para añadir la lista
  ui/ChannelsScreen.kt     # lista de canales
  ui/PlaylistsScreen.kt    # "Mis listas"
  data/SavedPlaylist.kt    # lista guardada + JSON
  ui/PlayerScreen.kt       # reproductor
```

## Siguientes pasos posibles
- Guía de programación (EPG XMLTV).
- Imagen en imagen (PiP) en móvil.
- Interfaz específica de TV con `androidx.tv` (tarjetas en cuadrícula).
