# GH3 Launcher

[English](README.md) · **Español**

Un launcher para **Guitar Hero III: Legends of Rock (PC)** que junta en una sola ventana lo que
normalmente se hace a mano: importar canciones de Clone Hero, ordenar las listas, ver tus
controles como los recibe el juego, ajustes, trucos y respaldos. La idea es que solo tengas que
instalar el juego y ponerte a tocar.

> **Aviso:** se hizo para simplificar las cosas, no como un producto pulido. Se probó en un solo
> equipo y **puede contener errores**. Respalda la carpeta del juego antes de probarlo; si algo
> sale mal, los respaldos del propio launcher (Herramientas) deshacen los cambios de canciones.

> No está afiliado ni respaldado por Activision, Neversoft, RedOctane ni Aspyr. Guitar Hero es una
> marca de Activision Publishing, Inc. **No incluye nada del juego**: el launcher lee el arte, los
> sonidos y la música de tu propia copia mientras corre.

## Funciones

- **Inicio** — botón Jugar, revisión de todo lo que necesitan el juego y el launcher (con botón
  para arreglarlo cuando se puede), con qué toca cada jugador, el setlist de un vistazo y tu
  **tiempo total de juego** ("2 días y 35 minutos", "1 año, 3 meses y 2 días"…: las tres unidades
  más grandes).
- **Canciones** — las listas Setlist, Bonus y Download con portadas y vista previa de audio. La
  lista Download se puede editar: divisiones que puedes renombrar y subir o bajar (el juego las
  muestra en ese orden), reordenar arrastrando, mover canciones entre divisiones, borrar canciones y
  elegir el **cantante** que sale en el escenario en cada una (el de siempre, una mujer, Bret Michaels
  o **nadie**, como en Cliffs of Dover). Las importadas en la sesión actual llevan la marca
  *IMPORTADA*.
- **Importar** — suelta canciones de Clone Hero (`.sng`, `.zip`, `.7z`, `.rar`, carpetas con
  `song.ini` + `notes.chart`/`notes.mid`, o packs completos). Revísalas y escúchalas, corrige
  título/artista/año, elige la cuenta inicial y la división, e instala: el chart y el audio se
  compilan con el toolkit de Honeycomb (FFmpeg para el audio), con movimiento de boca y el cantante
  según el artista (consulta MusicBrainz); una canción sin voces queda sin cantante en el escenario.
  "Reemplazar" vuelve a compilar las que importaste antes.
- **Controles** — ve en vivo cada control que tiene x360ce y lo que GH3 recibe de cada botón.
  Elige **cómo se toca** cada uno: *Guitarra* (GH3 recibe una guitarra: rasgueas tú) o *Control*
  (GH3 recibe un control de Xbox normal, que toca sin rasguear). **Teclado 2**: una guitarra o un
  control virtual en el teclado para un segundo jugador (el teclado propio de GH3 sigue siendo del
  jugador 1). Mejoras de whammy para guitarras (arregla el whammy "pegado" de las guitarras Wii,
  sensibilidad, turbo). Quita dispositivos viejos de la lista de x360ce.
- **Temas** — cómo se ven las notas, la autopista y el HUD, con los temas de la comunidad de FastGH3
  (se descargan desde el launcher o importas un .zip/.gfx/.tex), con un preview en vivo que toca la
  canción de fondo y dibuja sus notas con el tema. Solo se cambian las texturas que trae el tema;
  "Guitar Hero III (original)" devuelve el aspecto de tu juego.
- **Gráficos mejorados** — efectos de ReShade (luz ambiental, bloom, color, contraste, nitidez,
  desenfoque de movimiento) más los ajustes de calidad de GH3, en presets (Remasterizado, Nítido,
  Cinematográfico, Vivo) o uno por uno; se activan y desactivan cuando quieras. ReShade se descarga
  de reshade.me la primera vez.
- **Trucos** — códigos de trucos y **desbloqueos automáticos** (canciones, tienda, trucos, dinero)
  que GH3 aplica al cargar tu partida; útil porque el juego pide una partida nueva cuando cambian
  las listas de canciones.
- **Opciones** — video y audio de GH3, ajustes y plugins de GH3+, el idioma del launcher
  (**English** / Español), qué hace el launcher al pulsar Jugar, música de fondo en el menú (el tema
  del menú de GH3 y las vistas previas de tus canciones, con botones de pausa y siguiente) y los
  sonidos del menú de GH3.
- **Herramientas** — Honeycomb GUI y x360ce, **respaldos** del setlist y de la partida (se hacen
  solos antes de cada cambio y se restauran con un clic), volver a leer el arte del juego,
  registros y desinstalar.

## Requisitos

- Windows 10 u 11 de 64 bits.
- Tu propio **Guitar Hero III: Legends of Rock para PC**, instalado. Se recomienda **BetterGH3** (o
  cualquier copia con **GH3+**, su cargador de plugins): el plugin InputPlus (rasgueo automático,
  Teclado 2, whammy) necesita GH3+. Sin él, el launcher igual importa canciones y abre el juego.
- **Microsoft Edge WebView2 Runtime** — ya viene en Windows 11 y en Windows 10 actualizado. Si
  falta, el launcher lo avisa y da el enlace.
- Nada más: el launcher y el instalador traen su propio .NET. Solo la ventana de Honeycomb
  (Herramientas › Honeycomb) necesita .NET 8 Desktop Runtime; el launcher no.

## Instalación

1. Descarga **`GH3 Launcher Setup.exe`** y ejecútalo.
2. Elige la carpeta de Guitar Hero III (se encuentra sola cuando se puede) y las partes:
   - **GH3 Launcher** — `GH3 Launcher.exe` en la carpeta del juego.
   - **Honeycomb + FFmpeg** — `Honeycomb-GUI\` con `ffmpeg.exe`, `ffprobe.exe` y `ffplay.exe`
     (compilación de FFmpeg del 25 de octubre de 2026). Hace falta para importar canciones.
   - **x360ce** — `x360ce_x86.exe`, `xinput1_3.dll` y un `x360ce.ini` vacío (si ya tienes uno, se
     queda el tuyo). Hace falta para guitarras y controles.
   - Accesos directos en el escritorio y el menú Inicio.
3. Instala. Si la carpeta necesita permisos de administrador (por ejemplo en `Program Files`),
   Windows los pide.

Los archivos que el instalador reemplaza se guardan en `Launcher\backups\setup\` y vuelven a su
lugar al desinstalar. El instalador también acepta
`--target <carpeta> --components launcher,honeycomb,x360ce,desktop,startmenu --lang en|es --silent`.

Al **abrirlo por primera vez**, el launcher lee el arte, el video de carga y los sonidos del menú
de tu copia del juego (más o menos un minuto) y revisa la carpeta. Después: importa canciones y
a tocar.

## Controles (x360ce)

GH3 solo entiende controles de Xbox; **x360ce** (`xinput1_3.dll` junto a `GH3.exe`) hace que
cualquier guitarra o control lo parezca. Su app, `x360ce_x86.exe`, es **donde se configuran los
controles**: qué dispositivo es PAD 1–4 y qué hace cada botón. El launcher no cambia ese mapeo.

Para configurar una guitarra o un control:

1. Controles › **Abrir x360ce** (o Herramientas › x360ce). Si ofrece crear `xinput1_3.dll` o buscar
   configuraciones en internet, puedes decir que sí a lo primero y que no a lo segundo.
2. Agrega tu dispositivo a un PAD, asigna sus botones (para una guitarra elige un tipo de guitarra:
   la guitarra Harmonix de Wii funciona como *Guitar Alternate*) y **guarda**.
3. De vuelta en el launcher, Controles lo muestra con su PAD y enciende cada traste al presionarlo.
4. Elige **cómo se toca**: *Guitarra* (rasgueas tú) o *Control* (un control de Xbox: sin rasgueo,
   trastes en LT, LB, RB, RT y A). El launcher cambia el tipo de dispositivo en x360ce y mueve los
   botones de los trastes para que cada uno conserve su color (cierra x360ce y el juego antes). Un
   control de Xbox real que x360ce pasa tal cual siempre es control. También está en Inicio: clic en
   la tarjeta de un jugador para cambiarlo.

Notas:

- **Teclado 1** es el teclado propio de GH3; sus teclas se cambian en el juego: Options ›
  Controller Settings › Configure Keyboard (el juego deja cambiar los 5 trastes, Star Power, Whammy
  y rasgueo arriba/abajo; pausa y menú izquierda/derecha son fijas). **Teclado 2** lo emula el plugin
  InputPlus en un PAD que x360ce deja libre, como guitarra o como control de Xbox. Ponle teclas que
  el Teclado 1 no use: el juego deja de ver las del Teclado 2 para que ninguna haga dos cosas. Con
  dos teclados físicos funciona igual (Windows los junta en uno).
- GH3 decide quién es el jugador 1 según quién presiona un botón primero; x360ce suena al abrir el
  juego.
- Si **falta `xinput1_3.dll`** o no es el de x360ce, Inicio lo avisa: abre x360ce y deja que lo cree.
  Sin él, ninguna guitarra ni control llega al juego como lo mapea x360ce.
- "Quitar de x360ce" borra la sección de ese dispositivo en `x360ce.ini` (antes guarda una copia en
  `Launcher\backups\`). Cierra x360ce y el juego antes de quitar.

## Dónde queda cada cosa

Todo lo que guarda el launcher está en la carpeta del juego:

| Ruta | Qué es |
| --- | --- |
| `GH3 Launcher.exe` | el launcher |
| `Launcher\config.json` | sus ajustes, el tiempo jugado, el estilo de cada control |
| `Launcher\backups\` | respaldos del setlist y de la partida, archivos que reemplazó el instalador |
| `Launcher\game\` | arte y sonidos leídos de tu copia del juego |
| `Launcher\themes\` | los temas que descargaste o importaste (`backups\theme\` guarda tu zona global original) |
| `Launcher\reshade\` | ReShade y sus efectos, si activas Gráficos mejorados (`d3d9.dll` + `ReShade.ini` junto a `GH3.exe`) |
| `Launcher\launcher.log`, `songtool.log` | registros |
| `Launcher\Licenses\` | licencias de todo lo incluido |
| `PLUGINS\InputPlus.dll`, `InputPlus.ini` | el plugin GH3+ del launcher |

No se envía nada a ningún lado, salvo el artista y el título de las canciones que importas, que se
consultan en MusicBrainz para saber si quien canta es hombre o mujer. El launcher solo descarga
cuando tú lo pides: un tema (del Google Drive de su autor) o ReShade (reshade.me y sus shaders en
GitHub).

## Desinstalar

Configuración de Windows › Aplicaciones › **GH3 Launcher** › Desinstalar, o Herramientas ›
Desinstalar. Quita lo que agregó el instalador y devuelve lo que reemplazó; tú eliges si se borran
los datos del launcher. Guitar Hero III, tus partidas y las canciones que instalaste se quedan.

## Problemas comunes

- **La guitarra no responde, o cada traste rasguea solo** — en Controles revisa que tenga PAD y que
  *Cómo se toca* sea *Guitarra*. Revisa en Inicio si hay un aviso de `xinput1_3.dll`.
- **GH3 dice que la partida está dañada / pide una partida nueva** al cambiar canciones — es normal:
  GH3 ata la partida a sus listas. Trucos › desbloqueos automáticos lo recupera todo en segundos, y
  en Herramientas están los respaldos.
- **Una canción no se importa** — el motivo aparece junto a ella en la lista; `Launcher\songtool.log`
  tiene el detalle. Si falta FFmpeg, Inicio lo avisa.
- **GH3 se cierra al abrir** — las rutas de carpeta muy largas pueden causarlo: mueve el juego a,
  por ejemplo, `C:\Games\GH3`.

## Compilar desde el código

Necesita el **SDK de .NET 10** y una carpeta **Honeycomb-GUI** contra la que compilar (la del juego,
o `-Honeycomb <carpeta>`). Las herramientas de C++ de Visual Studio son opcionales (recompilan el
plugin InputPlus; si no están, se usa `plugin\InputPlus.dll` tal cual).

```powershell
powershell -NoProfile -File build.ps1 -Game "C:\Games\Guitar Hero III"            # dist\GH3 Launcher.exe (también se copia al juego)
powershell -NoProfile -File build.ps1 -Package -Game "C:\Games\Guitar Hero III"   # dist\GH3 Launcher Setup.exe
```

`-Package` empaqueta Honeycomb-GUI, FFmpeg y x360ce de esa carpeta del juego, sin los datos propios
de quien lo empaqueta (`QBDebug\keys_user*.txt`, `Backups\`).

| Carpeta | Qué es |
| --- | --- |
| `host\` | el launcher (WinForms + WebView2): juego, canciones, importación, controles, ajustes |
| `songtool\` | el compilador de canciones, dentro del launcher (`GH3 Launcher.exe songtool …`) |
| `web\` | la interfaz (HTML/CSS/JS, incrustada en el exe) |
| `plugin\` | InputPlus, el plugin de GH3+ (C) |
| `installer\` | `GH3 Launcher Setup.exe` |

## Licencia

El código de GH3 Launcher y sus programas (`GH3 Launcher.exe`, `GH3 Launcher Setup.exe`, el plugin
InputPlus) son de **dominio público** bajo [The Unlicense](LICENSE): úsalos, cópialos, modifícalos,
compártelos o véndelos, como código o como ejecutables, para lo que quieras, sin pedir permiso y sin
dar crédito.

El instalador también trae programas de terceros que conservan sus propias licencias: **FFmpeg**
(GPL-3.0), **Honeycomb** (GPL-3.0) y **x360ce** (LGPL-3.0); el launcher contiene el SDK de WebView2
(BSD-3), SharpCompress (MIT) y fuentes (OFL / Apache 2.0). Mira
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) y [`licenses/`](licenses). Si redistribuyes el
instalador, esas licencias piden que también indiques dónde está su código fuente (los avisos tienen
los enlaces).

## Gracias

A AddyMills por **Honeycomb** / GH Toolkit, al proyecto **x360ce**, a los desarrolladores de
**FFmpeg** y a las comunidades de GH3+ / BetterGH3 y Clone Hero.
