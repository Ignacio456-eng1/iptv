# IPTV Player para Windows 10/11

Versión de escritorio del reproductor IPTV. Igual que la app de Android, **no incluye canales**:
añades tu propia lista M3U o tus datos Xtream Codes.

## Descargar
En **Releases → "IPTV Player para Windows"** está `IptvPlayer-Setup.exe` (instalador) y
`IptvPlayer-portable-win-x64.zip` (sin instalar: descomprimir y abrir `IptvPlayer.exe`).
No necesita instalar .NET ni VLC: va todo dentro. El instalador no pide permisos de administrador.

Como el .exe no está firmado, la primera vez Windows puede mostrar "Windows protegió su PC":
pulsa **Más información → Ejecutar de todas formas**.

## Qué hace
- Listas **M3U / M3U8** por URL o desde un archivo del PC, y **Xtream Codes** (servidor, usuario, contraseña).
- **Varias listas** guardadas: se cambia de una a otra en el desplegable de arriba; menú ⋯ para actualizar, renombrar y borrar.
- Canales con logos, **grupos**, **búsqueda** y **favoritos** (★).
- Reproducción con **libVLC**: HLS, DASH, MPEG‑TS, MP4… Respeta `#EXTVLCOPT:http-user-agent`, `http-referrer` y `url|User-Agent=…`.
- Pistas de **audio y subtítulos**, formato de imagen, volumen y silencio.
- **Pantalla completa** con F11, doble clic sobre el vídeo o Alt+Intro (Esc para salir).
- Guarda una copia de cada lista para arrancar rápido o sin conexión.

Atajos: **RePág / AvPág** (o ↑/↓ en pantalla completa) cambian de canal, **Espacio** pausa, **M** silencia,
**Ctrl+F** busca, **F5** actualiza la lista.

Los datos se guardan en `%APPDATA%\IptvPlayer\settings.json` y la caché en `%LOCALAPPDATA%\IptvPlayer\cache`.

## Stack
C# · .NET 10 · WPF con el tema Fluent de Windows 11 · LibVLCSharp + libVLC 3 (64 bits) · Inno Setup para el instalador.

## Compilar
```
dotnet test tests/IptvPlayer.Core.Tests          # lógica (también en Linux/macOS)
dotnet publish src/IptvPlayer -c Release -r win-x64 --self-contained true -o out/app
```
Las pruebas de `tests/IptvPlayer.E2E` solo funcionan en Windows: reproducen emisiones de prueba reales con libVLC
y manejan la aplicación instalada con UI Automation (añadir listas, buscar, favoritos, reproducir, pantalla
completa, reiniciar y borrar). Necesitan `IPTV_EXE` con la ruta de `IptvPlayer.exe`.

El workflow **Build Windows** de GitHub Actions hace todo esto en un Windows, crea el instalador, lo instala en
silencio, pasa las pruebas sobre la app instalada y, en `main`, publica el instalador en Releases.

## Estructura
```
src/IptvPlayer.Core/     # lógica sin interfaz: M3uParser, PlaylistSource (M3U/Xtream), SettingsStore,
                         # PlaylistRepository (descarga + caché), MainViewModel (grupos, búsqueda, favoritos, zapping)
src/IptvPlayer/          # aplicación WPF: MainWindow (lista + reproductor), AddPlaylistWindow, InputDialog
tests/IptvPlayer.Core.Tests/  # pruebas de la lógica con un servidor HTTP local
tests/IptvPlayer.E2E/    # pruebas en Windows: libVLC real + interfaz completa
installer/IptvPlayer.iss # instalador Inno Setup
```
