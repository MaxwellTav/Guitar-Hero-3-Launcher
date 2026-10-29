/* The launcher's language: English (the default) or Spanish (Opciones › Launcher).
   The screens are written in Spanish. In English every text the page shows is swapped as it
   appears: a MutationObserver looks each text node (and title / placeholder) up in EN (the whole
   text) or EN_RE (texts with numbers or names in them); a text made of " · " parts is done part by
   part. Anything not found stays as it is (song titles, artists, the game's own names). */
const I18N = (() => {
  let lang = 'en';

  const EN = {
    // ---- shell, nav, overlays (index.html, app.js, main.js, player.js)
    'Inicio': 'Home', 'Canciones': 'Songs', 'Importar': 'Import', 'Controles': 'Controls', 'Trucos': 'Cheats', 'Opciones': 'Options', 'Herramientas': 'Tools',
    'Jugar': 'Play', '¡Jugar!': 'Play!', 'Guitar Hero III abierto': 'Guitar Hero III is open', 'Ir al juego': 'Go to the game', 'Cerrar': 'Close',
    'Otra canción': 'Another song', 'Pantalla completa (F11)': 'Fullscreen (F11)', 'Suelta para importar': 'Drop to import',
    '.sng · .zip · .7z · .rar · carpetas de Clone Hero': '.sng · .zip · .7z · .rar · Clone Hero folders',
    'Instalando': 'Installing', 'Algo salió mal': 'Something went wrong', 'Teclado (P1)': 'Keyboard (P1)',
    'No se encontraron canciones': 'No songs found',
    'Arrastra .sng, .zip, .7z, .rar o carpetas con song.ini / notes.chart / notes.mid.': 'Drag .sng, .zip, .7z, .rar or folders with song.ini / notes.chart / notes.mid.',
    '¿Cerrar el juego?': 'Close the game?', 'Se cerrará Guitar Hero III sin guardar lo que no se haya guardado.': 'Guitar Hero III will close without saving anything not saved yet.',
    'Cerrar GH3': 'Close GH3', 'Guitar Hero III se cerró': 'Guitar Hero III closed', 'Esta canción no tiene audio': 'This song has no audio',
    'Cancelar': 'Cancel', 'Aceptar': 'OK', 'Continuar': 'Continue', 'Guardar': 'Save', 'Salir': 'Exit', 'Descartar': 'Discard', 'Eliminar': 'Delete',
    'Abrir': 'Open', 'Ejecutar': 'Run', 'Restaurar': 'Restore', 'Restaurado': 'Restored', '¿Restaurar?': 'Restore?', 'Quitar': 'Remove', 'Olvidar': 'Forget',
    'Restablecer': 'Reset', 'Asignar': 'Assign', 'Activar': 'Enable', 'Activado': 'On', 'Desactivado': 'Off', 'Sí': 'Yes', 'Listo': 'Done', 'Lista.': 'Done.',
    'Aviso': 'Notice', 'Error': 'Error', 'Un momento.': 'Just a moment.', 'Cargando…': 'Loading…', 'Aplicando…': 'Applying…', 'Guardado ✓': 'Saved ✓',
    'Predeterminado': 'Default', 'Predet.': 'Default', 'Ninguna': 'None', 'Ninguno': 'None', 'Todas': 'All', 'Normal': 'Normal', 'Avanzado': 'Advanced',
    'Buscar': 'Search', 'Escuchar': 'Listen', 'Editando': 'Editing', 'Juego': 'Game', 'Lista': 'List', 'Menú': 'Menu', 'Modo': 'Mode', 'Volumen': 'Volume',
    'Guitarra': 'Guitar', 'Control': 'Controller', 'Teclado': 'Keyboard', 'Conectada': 'Connected', 'Conectado': 'Connected', 'Sin conectar': 'Not connected',
    // first start, health, play time (main.js, screen-home.js)
    'Preparando el launcher': 'Getting the launcher ready',
    'Leyendo el arte y los sonidos de tu copia de Guitar Hero III (solo la primera vez)…': 'Reading the art and sounds from your copy of Guitar Hero III (first time only)…',
    'No se pudo preparar el arte del juego': "Couldn't get the game's art ready", 'Reintentar': 'Try again', 'Seguir sin arte': 'Continue without art',
    'Revisa tu instalación': 'Check your installation', 'Abrir como administrador': 'Open as administrator', 'Volver a leer': 'Read again',
    'Ejecuta "GH3 Launcher Setup" otra vez para reparar.': 'Run "GH3 Launcher Setup" again to repair it.',
    'Tiempo jugado': 'Time played', 'Todavía no juegas desde el launcher.': "You haven't played from the launcher yet.",
    'sesión': 'session', 'sesiones': 'sessions', 'la más larga': 'longest', 'última vez': 'last time', 'jugando ahora': 'playing now',
    // ---- home
    '¡A rockear!': "Let's rock!", 'Leyendo tu setlist…': 'Reading your setlist…', '¿Con qué vas a tocar?': 'What are you playing with?',
    'Modo gamepad': 'Gamepad mode', 'Como guitarra': 'As a guitar', 'Estilo Clone Hero': 'Clone Hero style', 'Importadas': 'Imported',
    'Importadas hace poco': 'Recently imported', 'Todavía no importas canciones.': "You haven't imported songs yet.", 'Importar ahora →': 'Import now →',
    'No hay guitarra conectada': 'No guitar connected', 'No hay control conectado': 'No controller connected',
    'Conéctalo antes de jugar, o elige otra entrada. Puedes configurarlo en Controles.': 'Connect it before playing, or pick another input. You can set it up in Controls.',
    // ---- songs
    'Setlist': 'Setlist', 'Bonus': 'Bonus', 'Download': 'Download', 'Leyendo el setlist…': 'Reading the setlist…', 'Buscar canción o artista…': 'Search song or artist…',
    'Editar lista': 'Edit list', 'Volver a leer el setlist del juego': "Read the game's setlist again", 'Setlist actualizado': 'Setlist refreshed',
    'Ordenar por…': 'Sort by…', 'Ordenar las canciones dentro de cada división': 'Sort the songs inside each division', 'Artista (juntos por artista)': 'Artist (grouped by artist)',
    'Título A–Z': 'Title A–Z', 'Año': 'Year', 'Importadas más recientes primero': 'Newest imports first', 'DLC oficiales primero': 'Official DLC first', 'Invertir orden': 'Reverse order',
    'Agregar una división vacía al final': 'Add an empty division at the end', 'División': 'Division', 'División…': 'Division…',
    'Marca canciones para moverlas o borrarlas juntas': 'Tick songs to move or delete them together', 'Mover a…': 'Move to…', '+ División nueva…': '+ New division…',
    'Mover las marcadas a otra división': 'Move the ticked songs to another division', 'Subir las marcadas al principio de su división': 'Move the ticked songs to the top of their division',
    'Bajar las marcadas al final de su división': 'Move the ticked songs to the bottom of their division', 'Arrastra para mover': 'Drag to move',
    'Cantante en el escenario': 'Singer on stage', 'Cantante (el de siempre)': 'Singer (the usual one)', 'Cantante mujer': 'Female singer', 'Bret Michaels': 'Bret Michaels',
    'Arrastra cada canción por su agarradera, también a otra división. Si marcas varias, se mueven juntas. Haz clic en el nombre de una división para cambiarlo. ♂ ♀ B: el cantante de cada canción. Al terminar, presiona "Guardar".':
      'Drag each song by its grip, also to another division. Ticked songs move together. Click a division\'s name to change it. ♂ ♀ B: each song\'s singer. When you are done, press "Save".',
    'Quitar la división (sus canciones pasan a la de arriba)': 'Remove the division (its songs go to the one above)', 'Es la única división': "It's the only division",
    'Vacía: arrastra canciones aquí (si queda vacía, no se guarda)': 'Empty: drag songs here (an empty one is not saved)',
    'No hay canciones descargadas. Importa las tuyas desde Importar.': 'No downloaded songs. Import yours from Import.', 'Canciones bonus': 'Bonus songs',
    '¿Descartar los cambios?': 'Discard the changes?', 'Cambiaste la lista y no lo guardaste.': "You changed the list and didn't save it.",
    'División nueva': 'New division', 'Mover a una división nueva': 'Move to a new division', 'Nombre de la división': 'Division name',
    'GH3 asocia tu partida guardada al orden de las listas: al abrir el juego probablemente te pida crear una partida nueva. Antes de guardar, el launcher respalda tu partida y el setlist.':
      'GH3 ties your save to the order of its lists: when you open the game it will probably ask you to create a new save. Before saving, the launcher backs up your save and the setlist.',
    'Con Trucos › Desbloqueos automáticos recuperas al instante canciones, tienda y trucos en la partida nueva.': 'With Cheats › Automatic unlocks you get songs, store and cheats back in the new save right away.',
    '¿Guardar la lista?': 'Save the list?', 'Lista guardada': 'List saved', 'La lista Download ya está en el juego así.': 'The Download list is now like this in the game.',
    'Antes se respaldan el setlist y tu partida guardada (Herramientas › Respaldos).': 'The setlist and your save are backed up first (Tools › Backups).',
    'Como al agregar canciones, GH3 probablemente te pida una partida nueva.': 'As when adding songs, GH3 will probably ask you for a new save.',
    '¿Eliminar canciones?': 'Delete songs?', 'Canciones eliminadas': 'Songs deleted',
    'Arrastra por la agarradera (también a otra división) o usa "Ordenar por…". Marca canciones para moverlas, cambiarlas de división o eliminarlas juntas. Cada división se ve con su título en Quickplay › Download.':
      'Drag by the grip (also to another division) or use "Sort by…". Tick songs to move them, change their division or delete them together. Each division shows with its title in Quickplay › Download.',
    'Elige una canción': 'Pick a song', 'Haz clic en cualquier canción de la hoja para escuchar su preview.': 'Click any song on the sheet to hear its preview.',
    'Reproducir / pausar': 'Play / pause', 'Preview': 'Preview', 'Completa': 'Full song', 'Álbum': 'Album', 'Charter': 'Charter', 'Duración': 'Length',
    'Importada': 'Imported', 'IMPORTADA': 'IMPORTED', 'Origen': 'Source', 'Nombre interno': 'Internal name', 'Eliminar canción': 'Delete song', 'Eliminar DLC': 'Delete DLC',
    'Las canciones del Setlist y Bonus son parte del juego y no se pueden borrar ni mover.': 'Setlist and Bonus songs are part of the game and cannot be deleted or moved.',
    // ---- import
    'Canciones de Clone Hero → lista Download de Guitar Hero III': "Clone Hero songs → Guitar Hero III's Download list",
    'Elige .sng, .zip, .7z, .rar, o una carpeta con canciones de Clone Hero.': 'Pick .sng, .zip, .7z, .rar, or a folder with Clone Hero songs.',
    'Agregar más canciones': 'Add more songs', 'Suelta aquí tus canciones': 'Drop your songs here',
    'Arrastra más archivos o carpetas a cualquier parte de la ventana.': 'Drag more files or folders anywhere on the window.',
    '.sng, .zip, .7z, .rar o carpetas de Clone Hero (con song.ini y notes.chart / notes.mid). También packs con muchas canciones.': '.sng, .zip, .7z, .rar or Clone Hero folders (with song.ini and notes.chart / notes.mid). Packs with many songs too.',
    'Elegir archivos': 'Choose files', 'Elegir carpeta': 'Choose folder', 'Ninguna seleccionada': 'None selected',
    'Elegir la división de la lista Download donde quedarán las marcadas': 'Choose the Download division the ticked songs go into',
    'Van a la lista Download, cada una en su división. Antes se respaldan el setlist y tu partida guardada.': 'They go to the Download list, each in its division. The setlist and your save are backed up first.',
    'Instalar 1 canción': 'Install 1 song', 'Así se llamará la división en el juego': 'This is what the division will be called in the game',
    'Las canciones sin división van aquí': 'Songs without a division go here', 'Cambiar el nombre de esta división': 'Rename this division',
    '1 canción': '1 song', 'Marcar todas': 'Tick all', 'Desmarcar': 'Untick', 'Mover a la división…': 'Move to the division…',
    'Escribe una nueva o elige una que ya exista en la lista Download. Vacío = "Lost and Cursed Songs".': 'Type a new one or pick one already in the Download list. Empty = "Lost and Cursed Songs".',
    'Sin audio': 'No audio', 'Una sola pista': 'A single track', 'Bajo/ritmo': 'Bass/rhythm', 'No se puede instalar': "Can't be installed", '(sin título)': '(untitled)',
    'Ya instalada': 'Already installed', 'Instalada': 'Installed', 'En cola': 'Queued', 'Escuchar desde el preview': 'Listen from the preview', 'Editar datos': 'Edit details',
    'Quitar de la lista': 'Remove from the list', 'según el song.ini': 'from song.ini', 'según internet (MusicBrainz)': 'from the internet (MusicBrainz)',
    'buscando en internet…': 'looking it up online…', 'elegido por ti': 'chosen by you', 'no se encontró: el de siempre': 'not found: the usual one',
    'Título': 'Title', 'Artista': 'Artist', 'Inicio del preview (m:ss)': 'Preview start (m:ss)', 'Usar / escuchar': 'Use / listen', 'Conteo inicial': 'Count-in',
    'Cantante': 'Singer', 'Hombre (el de siempre)': 'Male (the usual one)', 'Mujer': 'Female', 'División (lista Download)': 'Division (Download list)',
    'HOPO (ticks, 170 = normal)': 'HOPO (ticks, 170 = normal)', 'Es un cover: en el juego dirá "como lo hizo famoso"': 'It\'s a cover: the game will say "as made famous by"',
    'Editar canción': 'Edit song', 'Algunas ya están instaladas': 'Some are already installed', 'Instalar como copia': 'Install as a copy', 'Reemplazar': 'Replace',
    'Respaldando el setlist y tu partida…': 'Backing up the setlist and your save…', 'Convirtiendo el chart a formato GH3…': 'Converting the chart to GH3 format…',
    'Mezclando y codificando el audio (lo más lento)…': 'Mixing and encoding the audio (the slowest part)…', 'Copiando al juego y agregando a la lista Download…': 'Copying into the game and adding it to the Download list…',
    '¡Canción instalada!': 'Song installed!', 'No se instaló nada': 'Nothing was installed', 'Revisa los errores en la lista.': 'Check the errors in the list.', 'Chart': 'Chart', 'Audio': 'Audio',
    // song warnings and errors that come from the native side
    'No tiene notes.mid ni notes.chart': 'It has no notes.mid or notes.chart', 'No tiene pista de guitarra (GH3 solo toca guitarra)': 'It has no guitar part (GH3 only plays guitar)',
    'Sin pista de guitarra separada: los fallos no silenciarán la guitarra': 'No separate guitar track: misses will not mute the guitar', 'No tiene audio': 'It has no audio',
    'Cierra Guitar Hero III antes de instalar canciones.': 'Close Guitar Hero III before installing songs.', 'No hay canciones listas para instalar.': 'There are no songs ready to install.',
    'Ya hay una instalación en curso.': 'An installation is already running.', 'Cierra Guitar Hero III antes de eliminar canciones.': 'Close Guitar Hero III before deleting songs.',
    'Cierra Guitar Hero III antes de cambiar la lista.': 'Close Guitar Hero III before changing the list.', 'Cierra Guitar Hero III antes de aplicar desbloqueos.': 'Close Guitar Hero III before applying unlocks.',
    'Cierra Guitar Hero III antes de restaurar.': 'Close Guitar Hero III before restoring.', 'Cierra Guitar Hero III antes de cambiar plugins.': 'Close Guitar Hero III before changing plugins.',
    'Guitar Hero III ya está abierto.': 'Guitar Hero III is already open.', 'Lista vacía': 'Empty list', 'Nada que eliminar': 'Nothing to delete',
    // ---- options
    'Video, audio, GH3+ y plugins · se guardan solas': 'Video, audio, GH3+ and plugins · saved automatically', 'Video': 'Video', 'Resolución': 'Resolution',
    'La del juego. Con "Ventana sin bordes" lo ideal es la de tu monitor.': 'The game\'s. With "Borderless window" your monitor\'s is best.',
    'Antialiasing': 'Antialiasing', 'Suaviza los bordes (MSAA). Más alto = más carga para la GPU.': 'Smooths edges (MSAA). Higher = more GPU load.',
    'Calidad gráfica': 'Graphics quality', 'El mismo ajuste que Opciones › Gráficos dentro del juego.': 'The same setting as Options › Graphics in the game.',
    'Nivel 0': 'Level 0', 'Nivel 1': 'Level 1', 'Nivel 2': 'Level 2',
    'Público': 'Crowd', 'El público animado en los escenarios.': 'The animated crowd in the venues.', 'Física': 'Physics', 'Pelo, ropa y objetos con física.': 'Hair, clothes and objects with physics.',
    'Destellos': 'Flares', 'Luces y destellos de los escenarios.': 'Venue lights and flares.', 'Cámara de primera fila': 'Front row camera', 'Cámara fija frente al escenario.': 'Fixed camera in front of the stage.',
    'Pantalla completa exclusiva (Aspyr)': 'Exclusive fullscreen (Aspyr)', 'Ajuste interno del port de Aspyr; con BorderlessWindow normalmente no hace falta.': "An internal setting of Aspyr's port; with BorderlessWindow you normally don't need it.",
    'GH3 todavía no creó su configuración: se creará con estos valores.': "GH3 hasn't made its settings yet: they will be made with these values.",
    'Desfase de la canción (Song Skew)': 'Song offset (Song Skew)', 'Ajuste fino de sincronía audio/notas en ms. Para calibrar usa también Opciones › Calibrar en el juego.': 'Fine audio/notes sync in ms. To calibrate also use Options › Calibrate in the game.',
    'Búferes (solo si el audio se corta)': 'Buffers (only if the audio stutters)', 'DSP: largo del búfer': 'DSP: buffer length', 'DSP: número de búferes': 'DSP: number of buffers',
    'Streaming: búfer': 'Streaming: buffer', 'Streaming: búfer de decodificación': 'Streaming: decode buffer', 'Intervalo de sincronía': 'Sync interval',
    'Lo leen los plugins FastGH3 y NoteLimitFix al iniciar el juego. "Predeterminado" no escribe nada.': 'Read by the FastGH3 and NoteLimitFix plugins when the game starts. "Default" writes nothing.',
    'Modo ventana': 'Windowed mode', 'Sin bordes': 'Borderless', 'GFX.VSync: sincroniza con el monitor (sin tearing, algo más de latencia).': 'GFX.VSync: syncs with the monitor (no tearing, a bit more latency).',
    'FPS máximo': 'Max FPS', 'Máximo de notas': 'Max notes', 'Player.MaxNotes: para charts enormes.': 'Player.MaxNotes: for huge charts.', 'Plugins GH3+': 'GH3+ plugins',
    'Cierra el juego para activar o desactivar plugins.': 'Close the game to turn plugins on or off.', 'Launcher': 'Launcher', 'Al presionar Jugar': 'When pressing Play',
    'Minimizar el launcher': 'Minimize the launcher', 'Dejarlo abierto': 'Leave it open', 'Cerrar el launcher': 'Close the launcher', 'Prioridad alta para GH3': 'High priority for GH3',
    'Le da preferencia al juego en la CPU.': 'Gives the game priority on the CPU.', 'Abrir en pantalla completa': 'Open in fullscreen', 'También con F11 en cualquier momento.': 'Also with F11 at any time.',
    'Idioma del juego': "Game's language", 'Se cambia con "Arreglar problemas de inicio" (Herramientas). Esta versión de BetterGH3 está en inglés.': 'Changed with "Fix startup issues" (Tools). This version of BetterGH3 is in English.',
    'Idioma del launcher': "Launcher's language", 'Los menús, mensajes y textos de este launcher.': "This launcher's menus, messages and texts.",
    'Música del menú': 'Menu music',
    'Primero la música del menú del juego, en orden al azar hasta acabarla; después los previews de todas tus canciones, en otro orden al azar hasta acabarlas; y otra vuelta. Se calla sola mientras escuchas una canción, juegas o el launcher está minimizado.':
      "First the game's menu music, shuffled until it runs out; then the previews of all your songs, shuffled again until they run out; and around again. It goes quiet by itself while you listen to a song, play, or the launcher is minimized.",
    'Música de fondo': 'Background music', 'Música del menú del juego': "The game's menu music", 'Los 6 temas que suenan en los menús de GH3 (Paint It Black, Bulls on Parade…).': "The 6 tracks of GH3's menus (Paint It Black, Bulls on Parade…).",
    'Canciones del juego': "The game's songs", 'Previews del Setlist, Bonus y los DLC oficiales.': 'Previews of the Setlist, Bonus and official DLC.', 'Canciones importadas': 'Imported songs',
    'Previews de las que instalaste desde Importar.': 'Previews of the ones you installed from Import.',
    'Sonidos del menú': 'Menu sounds', 'Los sonidos de GH3 al elegir, volver y moverte por el launcher.': "GH3's sounds when you pick, go back and move around the launcher.",
    'Sonidos de la interfaz': 'Interface sounds',
    // plugins (native side)
    'Núcleo GH3+': 'GH3+ core', 'Carga los demás plugins. No se puede desactivar.': 'Loads the other plugins. Cannot be turned off.',
    'Del launcher: rasgueo automático para controles, Teclado 2 (segundo jugador) y whammy mejorado.': "The launcher's: auto-strum for controllers, keyboard 2 (a second player) and a better whammy.",
    'Ventana sin bordes': 'Borderless window', 'Pantalla completa en modo ventana, sin cambiar la resolución del monitor.': "Fullscreen in windowed mode, without changing the monitor's resolution.",
    'Taps, opens y acordes HOPO': 'Taps, opens and HOPO chords', 'Notas tap/open de Clone Hero en los customs.': "Clone Hero's tap/open notes in customs.",
    'WhammyBoost (anterior)': 'WhammyBoost (previous)', 'Reemplazado por InputPlus; déjalo desactivado.': 'Replaced by InputPlus; leave it off.', 'Reemplazado por InputPlus.': 'Replaced by InputPlus.',
    'Corrige el cálculo del whammy del juego.': "Fixes the game's whammy math.", 'DLL XInput alternativo; con x360ce no hace falta.': 'Alternative XInput DLL; not needed with x360ce.',
    'FPS sin límite': 'Uncapped FPS', 'Quita el tope de 60 FPS del juego.': "Removes the game's 60 FPS cap.", 'Parches de FastGH3: límite de FPS, VSync y modo ventana (settings.ini).': "FastGH3's patches: FPS limit, VSync and windowed mode (settings.ini).",
    'Sin límite de notas': 'No note limit', 'Permite charts con muchísimas notas (customs largos).': 'Allows charts with very many notes (long customs).',
    'Sin límite de canciones': 'No song limit', 'Permite más canciones en las listas del juego.': "Allows more songs in the game's lists.",
    // ---- cheats
    'Códigos del juego, desbloqueos y tu partida': "The game's codes, unlocks and your save", 'Desbloquear todo': 'Unlock everything',
    'Canciones, guitarras, bajos, personajes, trajes, estilos y videos. Ojo: también pone 5 estrellas y 1.000.000 de puntos a todas las canciones.': 'Songs, guitars, basses, characters, outfits, styles and videos. Careful: it also gives every song 5 stars and 1,000,000 points.',
    'Desbloquear todas las canciones': 'Unlock all songs', 'Quickplay y Bonus, sin tocar tus estrellas.': 'Quickplay and Bonus, without touching your stars.',
    'Sin fallar (No Fail)': 'No Fail', 'Sin fallar': 'No Fail', 'No pierdes la canción aunque falles. No funciona en Carrera.': "You don't lose the song even if you miss. Doesn't work in Career.",
    'Hiper velocidad': 'Hyperspeed', 'Las notas bajan más rápido. En el menú eliges el nivel (1 a 5).': 'The notes come down faster. You pick the level (1 to 5) in the menu.',
    'Experto fácil': 'Easy Expert', 'El doble de margen para acertar cada nota.': 'Twice the margin to hit each note.', 'Modo precisión': 'Precision Mode',
    'La mitad de margen para acertar: más difícil.': 'Half the margin to hit: harder.', 'Guitarra de aire': 'Air Guitar', 'Tu guitarrista toca sin guitarra.': 'Your guitarist plays without a guitar.',
    'El modo "Performance" del juego original.': 'The original game\'s "Performance" mode.', 'Notas grandes': 'Large Gems', 'Gemas más grandes en la pista (truco de BetterGH3).': 'Bigger gems on the highway (a BetterGH3 cheat).',
    'Bret Michaels canta en lugar del cantante de la banda.': "Bret Michaels sings instead of the band's singer.", 'Modo debug': 'Debug mode',
    'Activa el menú de depuración de BetterGH3. Solo si sabes lo que haces.': "Turns on BetterGH3's debug menu. Only if you know what you're doing.",
    'Cierra el juego primero': 'Close the game first', 'Los desbloqueos se escriben en los scripts del juego.': "The unlocks are written into the game's scripts.",
    'Desbloqueos aplicados': 'Unlocks applied', 'Desbloqueos quitados': 'Unlocks removed', 'Lo que ya se desbloqueó queda en tu partida.': 'What was already unlocked stays in your save.',
    'Leyendo trucos…': 'Reading cheats…', 'Desbloqueos automáticos': 'Automatic unlocks', 'Aplicado en el juego': 'Applied to the game', 'Sin aplicar': 'Not applied',
    'Se ejecutan cada vez que GH3 carga tu partida (también en partidas nuevas) y el juego los guarda. Si cambias canciones y GH3 te pide una partida nueva, esto la deja desbloqueada otra vez.':
      'They run every time GH3 loads your save (new saves too) and the game keeps them. If you change songs and GH3 asks you for a new save, this unlocks it again.',
    'Todas las canciones': 'All songs', 'Setlist en Quickplay y todas las Bonus, sin comprarlas. No cambia tus estrellas ni puntajes.': "Setlist in Quickplay and every Bonus song, without buying them. Doesn't change your stars or scores.",
    'Toda la tienda': 'The whole store', 'Guitarras, bajos y acabados, personajes secretos (Lou, Slash, Tom Morello…), trajes, estilos y videos.': 'Guitars, basses and finishes, secret characters (Lou, Slash, Tom Morello…), outfits, styles and videos.',
    'Todos los trucos disponibles': 'Every cheat available', 'Aparecen listos para encender en Opciones › Trucos, sin escribir códigos.': 'They show up ready to turn on in Options › Cheats, no codes to type.',
    'Banda "Complete"': '"Complete" band',
    'Una banda de Carrera con todo el juego pasado en las 4 dificultades (solo y cooperativo): todos los niveles, encores y guerras de guitarras, escenarios y canciones bonus. Así puedes repetir cualquier guerra con su intro. Se crea una vez en el primer espacio libre (si los 5 están ocupados, reemplaza la quinta banda).':
      'A Career band with the whole game beaten on all 4 difficulties (solo and co-op): every tier, encore and guitar battle, venue and bonus song. So you can replay any battle with its intro. It is made once in the first free slot (if all 5 are taken, it replaces the fifth band).',
    'Dinero de la banda': 'Band cash', 'Si tienes menos, sube a esta cantidad al cargar.': 'If you have less, it goes up to this when loading.', 'No cambiar': "Don't change",
    'Trucos encendidos al abrir el juego': 'Cheats on when the game opens', 'Margen para acertar': 'Hit margin', 'Experto fácil o Modo precisión (no los dos).': 'Easy Expert or Precision Mode (not both).',
    'Precisión': 'Precision', 'Nivel con el que arranca.': 'The level it starts with.', 'Aplicar al juego': 'Apply to the game', 'Quitar del juego': 'Remove from the game',
    'Con el juego cerrado. Antes se respaldan el setlist y tu partida.': 'With the game closed. The setlist and your save are backed up first.', 'Códigos de trucos': 'Cheat codes',
    'En el juego: Options › Cheats › Enter New Cheat. Toca cada paso con los trastes indicados y rasguea; los pasos con varias notas son acordes.': 'In the game: Options › Cheats › Enter New Cheat. Play each step with the frets shown and strum; steps with several notes are chords.',
    'Tu partida': 'Your save',
    'GH3 asocia la partida a las canciones de sus listas y al orden en que están. Al agregar, borrar o reordenar canciones, el juego dice que la partida está dañada y te pide crear una nueva. El launcher la respalda antes de cada cambio, y los desbloqueos de arriba la dejan como estaba en segundos.':
      'GH3 ties the save to the songs in its lists and their order. When you add, delete or reorder songs, the game says the save is damaged and asks you to make a new one. The launcher backs it up before every change, and the unlocks above bring it back in seconds.',
    'Partida respaldada': 'Save backed up', 'Ver y restaurar respaldos': 'See and restore backups', 'Carpeta de partidas': 'Saves folder', 'Respaldar ahora': 'Back up now',
    // ---- tools
    'Apps de la carpeta, reparaciones, respaldos': "The folder's apps, repairs, backups", 'Continuar.': 'Continue.', 'Acceso directo creado': 'Shortcut created', 'Está en tu escritorio.': "It's on your desktop.",
    'Apps incluidas': 'Included apps', 'Honeycomb': 'Honeycomb', 'El compilador de canciones que usa el launcher, con todas sus opciones avanzadas.': 'The song compiler the launcher uses, with all its advanced options.',
    'El compilador de canciones que usa el launcher, con todas sus opciones avanzadas (necesita .NET 8 Desktop Runtime).': 'The song compiler the launcher uses, with all its advanced options (needs the .NET 8 Desktop Runtime).',
    'Donde se configuran tus guitarras y controles: sus botones y quién es PAD 1, 2, 3 o 4.': 'Where your guitars and controllers are set up: their buttons and who is PAD 1, 2, 3 or 4.', 'Abrir x360ce': 'Open x360ce',
    'Cuenta online': 'Online account', 'Crea tu cuenta para jugar en línea (Warehouse).': 'Make your account to play online (Warehouse).', 'Honeycomb en GitHub': 'Honeycomb on GitHub',
    'Página de descargas de Honeycomb.': "Honeycomb's download page.", 'Reparar e instalar': 'Repair and install', 'Arreglar problemas de inicio': 'Fix startup issues',
    'Borra la config gráfica, repara el registro, instala DirectX 9 y VC++ x86.': 'Clears the graphics settings, repairs the registry, installs DirectX 9 and VC++ x86.',
    'Este script de BetterGH3 borra tu configuración gráfica (resolución, etc.), cierra la sesión online, pone el juego en inglés y abre los instaladores de DirectX y Visual C++. Tu partida guardada no se toca.':
      "This BetterGH3 script clears your graphics settings (resolution, etc.), signs you out of online play, sets the game to English and opens the DirectX and Visual C++ installers. Your save isn't touched.",
    'Permitir en el Firewall': 'Allow through the Firewall', 'Reglas para GH3.exe y el puerto 3074 (online).': 'Rules for GH3.exe and port 3074 (online).',
    'Acceso directo del launcher': "The launcher's shortcut", 'Crea "Guitar Hero III Launcher" en tu escritorio.': 'Puts "Guitar Hero III Launcher" on your desktop.',
    'Acceso directo de GH3': "GH3's shortcut", 'El script original: acceso directo a GH3.exe (sin launcher).': 'The original script: a shortcut to GH3.exe (without the launcher).',
    'Carpetas': 'Folders', 'Partidas guardadas': 'Saved games', 'Charts (DATA\\SONGS)': 'Charts (DATA\\SONGS)', 'Un _song.pak.xen por canción.': 'One _song.pak.xen per song.',
    'Audio (DATA\\MUSIC)': 'Audio (DATA\\MUSIC)', 'Un .fsb.xen por canción.': 'One .fsb.xen per song.', 'Plugins': 'Plugins', 'PLUGINS y PLUGINS\\Disabled.': 'PLUGINS and PLUGINS\\Disabled.',
    'Respaldos': 'Backups', 'Setlist y partidas que guardó el launcher.': 'Setlists and saves the launcher kept.', 'Respaldos del setlist': 'Setlist backups', 'Respaldos de la partida': 'Save backups',
    'Original (antes del launcher)': 'Original (before the launcher)', 'partida guardada': 'saved game', 'setlist': 'setlist',
    'Todavía no hay respaldos: se crean solos antes de instalar o eliminar canciones.': 'No backups yet: they are made by themselves before installing or deleting songs.',
    'Acerca de': 'About', 'Carpeta': 'Folder', 'Registros': 'Logs', 'carpeta Launcher': 'Launcher folder',
    'Desinstalar': 'Uninstall', 'Desinstalar GH3 Launcher': 'Uninstall GH3 Launcher', 'Quita el launcher y lo que agregó su instalador. El juego, tus partidas y tus canciones se quedan.': 'Removes the launcher and what its installer added. The game, your saves and your songs stay.',
    'Arte y sonidos del juego': "The game's art and sounds", 'Vuelve a leer el arte, el video de carga y los sonidos de tu copia del juego.': 'Reads the art, the loading movie and the sounds from your copy of the game again.',
    'editar lista Download': 'edit Download list', 'trucos y desbloqueos': 'cheats and unlocks', 'reordenar Download': 'reorder Download', 'manual': 'manual',
    // ---- controls
    'x360ce asigna los botones; aquí eliges cómo se toca cada control y ves lo que recibe GH3': 'x360ce assigns the buttons; here you pick how each controller is played and see what GH3 gets',
    'Los botones de cada control y quién es PAD 1, 2, 3 o 4 se configuran en x360ce; el launcher no los cambia. Aquí eliges si cada uno se toca como guitarra o como control, y ves lo que recibe GH3.':
      "Each controller's buttons and who is PAD 1, 2, 3 or 4 are set up in x360ce; the launcher doesn't change them. Here you pick whether each one is played as a guitar or as a controller, and see what GH3 gets.",
    'Volver a leer x360ce.ini': 'Read x360ce.ini again', 'x360ce no está instalado': 'x360ce is not installed', 'Falta xinput1_3.dll': 'xinput1_3.dll is missing',
    'xinput1_3.dll no es de x360ce': "xinput1_3.dll isn't x360ce's", 'x360ce abierto': 'x360ce is open',
    'GH3 abierto: lo que cambies en x360ce cuenta al reiniciar el juego': 'GH3 is open: what you change in x360ce counts when the game restarts',
    'Dispositivos': 'Devices', 'Conecta tu guitarra o control: aparecerá aquí solo.': 'Connect your guitar or controller: it will show up here by itself.',
    'Teclado 1': 'Keyboard 1', 'Teclado 2': 'Keyboard 2', 'Teclado 2 (emulado)': 'Keyboard 2 (emulated)', 'Del juego': "The game's", 'Nativo de GH3': "GH3's own",
    'Sin PAD libre': 'No free PAD', 'Sin configurar en x360ce': 'Not set up in x360ce', 'No conectado': 'Not connected', 'Sin PAD en x360ce': 'No PAD in x360ce',
    'Sin PAD': 'No PAD', 'Sin configurar': 'Not set up', 'x360ce: directo': 'x360ce: pass-through', 'x360ce: guitarra': 'x360ce: guitar', 'x360ce: mapeado': 'x360ce: mapped',
    'Cómo se toca': "How it's played", 'Rasgueas tú': 'You strum', 'Rasgueo automático': 'Auto-strum',
    'x360ce todavía no tiene este control, así que GH3 no lo verá. Ábrelo en x360ce, asígnale un PAD y guarda.': "x360ce doesn't have this controller yet, so GH3 won't see it. Open x360ce, give it a PAD and save.",
    'x360ce lo tiene, pero sin PAD: GH3 no lo verá hasta que se lo asignes en x360ce.': "x360ce has it, but with no PAD: GH3 won't see it until you give it one in x360ce.",
    'Cambiar botones en x360ce': 'Change buttons in x360ce', 'Configurar en x360ce': 'Set up in x360ce', 'Quitar de x360ce': 'Remove from x360ce', 'Así lo verá GH3': 'How GH3 will see it',
    'x360ce lo pasa tal cual: GH3 lo usa como un control de Xbox normal, con su propio esquema de botones.': 'x360ce passes it through as it is: GH3 uses it as a plain Xbox controller, with its own button layout.',
    'Es el teclado propio de Guitar Hero III: el juego lo lee directamente y sus teclas se cambian en las opciones del juego. El launcher no lo toca.':
      "It's Guitar Hero III's own keyboard: the game reads it directly and its keys are changed in the game's options. The launcher doesn't touch it.",
    'Para que otra persona toque también con teclado, activa el Teclado 2.': 'For someone else to play on the keyboard too, turn on keyboard 2.', 'Ir a Teclado 2': 'Go to keyboard 2',
    'Para un segundo jugador con teclado. El plugin InputPlus crea una guitarra virtual con estas teclas, en un PAD que x360ce deja libre (x360ce no lee teclados).':
      "For a second player on the keyboard. The InputPlus plugin makes a virtual guitar with these keys, on a PAD x360ce leaves free (x360ce doesn't read keyboards).",
    'x360ce usa los 4 PAD: quita un control para dejar uno libre.': 'x360ce uses all 4 PADs: remove a controller to free one.', 'Solo con GH3 en primer plano': 'Only with GH3 in front',
    'Ignora el teclado cuando estás en otra ventana.': 'Ignores the keyboard while you are in another window.', 'Teclas': 'Keys', 'Teclas predeterminadas': 'Default keys',
    'Usa teclas que el Teclado 1 no use: el juego deja de ver las de aquí, para que ninguna haga dos cosas. Con dos teclados físicos pasa igual, porque Windows los junta en uno. El teclado numérico funciona con Bloq Num activado.':
      "Use keys keyboard 1 doesn't use: the game stops seeing the ones here, so no key does two things. With two physical keyboards it's the same, because Windows joins them into one. The numeric keypad works with Num Lock on.",
    '¿Quitar este control de x360ce?': 'Remove this controller from x360ce?', 'Control quitado de x360ce': 'Controller removed from x360ce',
    'Se guarda solo y el juego lo aplica en ~1 s, aunque esté abierto.': 'Saved by itself; the game applies it in ~1 s, even while it is open.', 'Avanzado: rasgueo automático': 'Advanced: auto-strum',
    'Parte del D-pad (POV) completo': 'Part of the whole D-pad (POV)', 'No se detectó nada': 'Nothing was detected', 'Vuelve a intentarlo y presiona la tecla.': 'Try again and press the key.',
    'Sin asignar': 'Not assigned', 'Esa tecla no se puede usar': "That key can't be used", 'Tecla repetida': 'Key already in use', 'Entradas crudas (DirectInput)': 'Raw inputs (DirectInput)',
    'Esperando datos…': 'Waiting for data…', 'Solo teclado': 'Keyboard only', '1 control listo': '1 controller ready',
    'Clic para cambiar entre guitarra y control': 'Click to switch between guitar and controller', 'Ver controles →': 'See controls →',
    'No hay guitarras ni controles conectados con un PAD de x360ce (el teclado del juego siempre funciona).': "No guitars or controllers connected with an x360ce PAD (the game's keyboard always works).",
    'Clic en una tarjeta para cambiar entre guitarra (rasgueas tú) y control de Xbox (sin rasgueo).': 'Click a card to switch between guitar (you strum) and Xbox controller (no strumming).',
    'Sin rasgueo (control de Xbox)': 'No strumming (Xbox controller)', 'Sin rasgueo': 'No strumming', 'Control de Xbox directo': 'Xbox controller, passed through',
    'Guitarra: GH3 lo recibe como guitarra y rasgueas tú. Control: lo recibe como control de Xbox y toca sin rasguear (los trastes pasan a LT, LB, RB, RT y A).':
      'Guitar: GH3 gets it as a guitar and you strum. Controller: it gets it as an Xbox controller and plays without strumming (the frets move to LT, LB, RB, RT and A).',
    'x360ce pasa este control de Xbox tal cual: GH3 siempre lo recibe como control y toca sin rasguear.': 'x360ce passes this Xbox controller through as it is: GH3 always gets it as a controller and it plays without strumming.',
    'x360ce lo pasa tal cual: GH3 siempre lo recibe como control, sin rasguear.': 'x360ce passes it through as it is: GH3 always gets it as a controller, without strumming.',
    'Con el esquema de control de GH3: LT verde, LB rojo, RB amarillo, RT azul, A naranja.': "With GH3's controller layout: LT green, LB red, RB yellow, RT blue, A orange.",
    'Para un segundo jugador con teclado. El plugin InputPlus crea un control virtual con estas teclas (guitarra o control de Xbox, según elijas abajo), en un PAD que x360ce deja libre (x360ce no lee teclados).':
      "For a second player on the keyboard. The InputPlus plugin makes a virtual pad with these keys (a guitar or an Xbox controller, as you choose below), on a PAD x360ce leaves free (x360ce doesn't read keyboards).",
    'Sin cantante (canción sin voz)': 'No singer (no vocals)', 'Subir la división (así queda en el juego)': 'Move the division up (the game shows it this way)', 'Bajar la división (así queda en el juego)': 'Move the division down (the game shows it this way)',
    'Arrastra cada canción por su agarradera, también a otra división. Si marcas varias, se mueven juntas. Haz clic en el nombre de una división para cambiarlo; ▲ ▼ la mueven de lugar. ♂ ♀ B y el micrófono tachado: el cantante de cada canción (o ninguno). Al terminar, presiona "Guardar".':
      'Drag each song by its handle, also to another division. Ticked songs move together. Click a division\'s name to change it; ▲ ▼ move it. ♂ ♀ B and the crossed-out mic: each song\'s singer (or none). When you are done, press "Save".',
    'Es el teclado propio de Guitar Hero III: el juego lo lee directamente. Sus teclas se cambian en el juego: Options › Controller Settings › Configure Keyboard.':
      "It's Guitar Hero III's own keyboard: the game reads it directly. Its keys are changed in the game: Options › Controller Settings › Configure Keyboard.",
    'El juego solo deja cambiar: los 5 trastes, Star Power, Whammy, rasgueo arriba y rasgueo abajo. Pausa y menú izquierda/derecha son fijas.':
      'The game only lets you change: the 5 frets, Star Power, Whammy, strum up and strum down. Pause and menu left/right are fixed.',
    'Pausar la música': 'Pause the music', 'Seguir con la música': 'Resume the music',
    // ---- themes
    'Temas': 'Themes', 'Cómo se ven las notas, la autopista y el HUD en el juego': 'How the notes, the highway and the HUD look in the game', 'Tus temas': 'Your themes', 'Puesto': 'On',
    'Puesto en el juego': 'On the game', 'Poner en el juego': 'Put on the game', 'Otra canción para el preview': 'Another song for the preview', 'Cierra el juego para cambiar el tema': 'Close the game to change the theme',
    'Importar tema…': 'Import a theme…', 'Temas de la comunidad': "The community's themes", 'Descargar': 'Download', 'Tema descargado': 'Theme downloaded', '¿Quitar este tema?': 'Remove this theme?',
    'De la página de temas de FastGH3 (donnaken15.com). Se descargan aquí y no se tocan tus archivos hasta que pongas uno.': "From FastGH3's theme page (donnaken15.com). They download here, and your files aren't touched until you put one on.",
    'Se cambian las texturas que trae el tema (notas, autopista, HUD); los menús, personajes y lo que el tema no traiga quedan como en tu juego. Tu original se guarda en Launcher\\backups\\theme.':
      "The textures the theme brings change (notes, highway, HUD); the menus, characters and anything the theme doesn't have stay as in your game. Your original is kept in Launcher\\backups\\theme.",
    'Tema puesto: se verá la próxima vez que abras el juego': 'Theme on: you will see it the next time you open the game', 'Preparando el preview…': 'Getting the preview ready…', 'No hay canciones para el preview.': 'There are no songs for the preview.',
    // ---- graphics
    'Gráficos mejorados': 'Enhanced graphics', 'Ajustar cada efecto': 'Adjust each effect', 'Un momento…': 'One moment…', 'Personalizado: tus ajustes de abajo.': 'Custom: your settings below.',
    'Efectos de ReShade sobre el juego, más los ajustes de calidad de GH3. Los activas y desactivas aquí cuando quieras; se ven al abrir el juego (dentro del juego, la tecla Inicio abre el menú de ReShade).':
      "ReShade's effects over the game, plus GH3's quality settings. Turn them on and off here whenever you like; they show when the game opens (in the game, the Home key opens ReShade's menu).",
    'La primera vez se descarga ReShade (reshade.me) y sus efectos oficiales.': 'The first time, ReShade (reshade.me) and its official effects are downloaded.',
    'Ya hay otro d3d9.dll junto a GH3.exe (otro mod): quítalo para usar esto.': "There's already another d3d9.dll next to GH3.exe (another mod): remove it to use this.",
    'Cierra el juego para activar o desactivar.': 'Close the game to turn it on or off.', 'Gráficos mejorados activados': 'Enhanced graphics on', 'Gráficos mejorados desactivados': 'Enhanced graphics off',
    'Se ven la próxima vez que abras el juego.': 'You will see them the next time you open the game.',
    'Remasterizado': 'Remastered', 'Nítido': 'Sharp', 'Cinematográfico': 'Cinematic', 'Vivo': 'Vivid',
    'Luz ambiental, color, contraste, nitidez y un toque de desenfoque de movimiento, como el preset "GH3 Remastered".': 'Ambient light, color, contrast, sharpness and a touch of motion blur, like the "GH3 Remastered" preset.',
    'Más definición y algo de color, sin tocar la iluminación.': 'More definition and some color, lighting untouched.', 'Bloom, luz ambiental y movimiento suave: más espectáculo.': 'Bloom, ambient light and smooth motion: more of a show.',
    'Colores intensos y brillo.': 'Strong colors and glow.', 'Iluminación ambiental': 'Ambient lighting', 'Luz suave alrededor de las zonas brillantes.': 'Soft light around bright areas.',
    'Resplandor en luces y destellos.': 'Glow on lights and flares.', 'Saturación inteligente: aviva lo apagado sin quemar lo intenso.': 'Smart saturation: livens dull colors without burning strong ones.',
    'Contraste': 'Contrast', 'Negros más profundos y blancos limpios.': 'Deeper blacks and clean whites.', 'Nitidez': 'Sharpness', 'Bordes y texturas más definidos.': 'Sharper edges and textures.',
    'Desenfoque de movimiento': 'Motion blur', 'Movimientos más suaves.': 'Smoother motion.',
    'Whammy (guitarras y teclado)': 'Whammy (guitars and keyboard)', 'Mejoras de whammy': 'Whammy improvements', 'Si lo apagas, GH3 recibe el whammy tal cual.': 'If you turn it off, GH3 gets the whammy as it is.',
    'Corregir whammy "pegado"': 'Fix a "stuck" whammy', 'Tu guitarra Wii manda 50% tras 1–2 s quieta; esto lo trata como barra suelta.': 'Your Wii guitar sends 50% after 1–2 s still; this treats it as a released bar.',
    'Zona muerta': 'Dead zone', 'Recorrido inicial que no cuenta.': "The first bit of travel that doesn't count.", 'Exagerar el movimiento': 'Exaggerate the movement',
    'Cada subida y bajada de la barra se amplifica.': 'Every rise and fall of the bar is amplified.', 'Cuánto exagera': 'How much', 'Turbo (oscilación automática)': 'Turbo (automatic wobble)',
    'Mientras la barra esté presionada, sube y baja sola: notas largas más "vivas".': 'While the bar is pressed it goes up and down by itself: livelier long notes.',
    'Velocidad del turbo': 'Turbo speed', 'Oscilaciones por segundo.': 'Wobbles per second.', 'Profundidad del turbo': 'Turbo depth', 'Avanzado: tocar sin rasgueo': 'Advanced: play without strumming',
    'Ventana de acordes': 'Chord window', 'Espera para juntar los trastes de un acorde antes de rasguear.': 'Waits to gather the frets of a chord before strumming.',
    'Duración del rasgueo': 'Strum length', 'Cuánto dura cada rasgueo automático.': 'How long each automatic strum lasts.', 'Sensibilidad': 'Sensitivity',
    'Rasgueo arriba': 'Strum up', 'Rasgueo abajo': 'Strum down', 'Menú izquierda': 'Menu left', 'Menú derecha': 'Menu right', 'Start / pausa': 'Start / pause',
    'Inclinar (Star Power)': 'Tilt (Star Power)', 'Inclinar': 'Tilt', 'Verde': 'Green', 'Rojo': 'Red', 'Amarillo': 'Yellow', 'Azul': 'Blue', 'Naranja': 'Orange',
    'ARRIBA': 'UP', 'ABAJO': 'DOWN', 'Flecha ↑': 'Arrow ↑', 'Flecha ↓': 'Arrow ↓', 'Flecha ←': 'Arrow ←', 'Flecha →': 'Arrow →', 'Espacio': 'Space', 'Retroceso': 'Backspace',
    'Supr': 'Delete', 'Fin': 'End', 'RePág': 'Page Up', 'AvPág': 'Page Down', 'Bloq Mayús': 'Caps Lock', 'Shift izq.': 'Left Shift', 'Shift der.': 'Right Shift',
    'Ctrl izq.': 'Left Ctrl', 'Ctrl der.': 'Right Ctrl', 'Tecla': 'Key'
  };

  // texts with numbers, names or other texts in them: [regex, replacement or fn(match, ...groups)]
  const EN_RE = [
    [/^(\d+) canciones$/, '$1 songs'], [/^(\d+) resultados$/, '$1 results'], [/^(\d+) marcadas?$/, '$1 ticked'],
    [/^(\d+) de (\d+) seleccionadas$/, '$1 of $2 selected'], [/^Quitar instaladas \((\d+)\)$/, 'Remove installed ($1)'], [/^Instalar (\d+) canciones$/, 'Install $1 songs'],
    [/^Mover (\d+) canciones a la división…$/, 'Move $1 songs to the division…'], [/^Dificultad de guitarra: (\d+)\/6$/, 'Guitar difficulty: $1/6'],
    [/^\+(\d+) pistas?$/, (m, n) => `+${n} track${n > 1 ? 's' : ''}`],
    [/^Ya hay una canción con ese título y artista: (.+)$/, 'There is already a song with that title and artist: $1'],
    [/^(\d+) canción\(es\) tienen el mismo título y artista que una que ya está en el juego\.$/, '$1 song(s) have the same title and artist as one already in the game.'],
    [/^"Reemplazar" vuelve a compilar las (\d+) que importaste antes/, (m, n) => `"Replace" compiles again the ${n} you imported before, in the same place (so they get the new improvements, like the singer's mouth movement). The game's own songs and DLC are never replaced: those are installed as a copy.`],
    [/^(\d+) canción\(es\)$/, '$1 song(s)'], [/^(\d+) de (\d+) · (.*)$/, '$1 of $2 · $3'], [/^¡(\d+) canciones instaladas!$/, '$1 songs installed!'],
    [/^Están en Quickplay › Download\.\s*(?:(\d+) fallaron \(mira el detalle en la lista\)\.)?$/, (m, n) => 'They are in Quickplay › Download.' + (n ? ` ${n} failed (see the details in the list).` : '')],
    [/^(\d+) son DLC oficiales que trae BetterGH3\.$/, '$1 are official DLC that came with BetterGH3.'], [/^(\d+) fuera del juego\.$/, '$1 removed from the game.'],
    [/^Se quitarán de la lista Download y se borrarán sus archivos \((\d+)\):$/, 'They will be removed from the Download list and their files deleted ($1):'],
    [/^Nada con "(.*)"\.$/, 'Nothing for "$1".'],
    [/^La lista Download quedará así(?:, con (\d+) cantante\(s\) cambiado\(s\))?\.$/, (m, n) => 'The Download list will look like this' + (n ? `, with ${n} singer(s) changed` : '') + '.'],
    [/^(\d+) división\(es\) vacía\(s\) no se guardan\.$/, '$1 empty division(s) are not saved.'],
    [/^No se encontró el audio de esta canción \((.+)\)\.$/, "This song's audio was not found ($1)."],
    [/^como lo hizo famoso (.+)$/, 'as made famous by $1'], [/^de (.+)$/, 'by $1', '.by'],
    [/^(\d+) canciones en tu Guitar Hero III\.$/, '$1 songs in your Guitar Hero III.'],
    [/^(\d+) controles listos$/, '$1 controllers ready'], [/^Probando con: (.+)$/, 'Trying it with: $1'], [/^Descargando (.+)…$/, 'Downloading $1…'],
    [/^ReShade (.+) puesto en el juego\.$/, 'ReShade $1 is on the game.'], [/^Tocará como PAD (\d)\.$/, 'It will play as PAD $1.'],
    [/^Se borra de x360ce\.ini(?: y deja libre el PAD (\d))?\. Antes se guarda una copia en Launcher\\backups\. Para volver a usarlo, configúralo otra vez en x360ce\.$/,
      (m, pad) => `It's deleted from x360ce.ini${pad ? ' and PAD ' + pad + ' is left free' : ''}. A copy is kept in Launcher\\backups first. To use it again, set it up again in x360ce.`],
    [/^Jugando · (.+)$/, 'Playing · $1'], [/^Sesión de (.+)\.$/, 'Session: $1.'],
    [/^Música del menú: (.+?)(?: de (.+?))? \(se apaga en Opciones › Launcher\)$/, (m, t, a) => `Menu music: ${t}${a ? ' by ' + a : ''} (turn it off in Options › Launcher)`],
    [/^Paso (\d+)$/, 'Step $1'],
    [/^Se aplican cuando GH3 cargue tu partida \(al abrir el juego\)\.( La banda "Complete" estará en Carrera\.)?$/, (m, c) => 'They apply when GH3 loads your save (when the game opens).' + (c ? ' The "Complete" band will be in Career.' : '')],
    [/^Tecla (.+)$/, 'Key $1'], [/^Eje (.+)$/, 'Axis $1'], [/^Botón (\d+)$/, 'Button $1'],
    [/^(.+) también está en "(.+)"\.$/, (m, k, a) => `${k} is also on "${tr1(a)}".`],
    [/^(.+)\s+\(tu monitor\)$/, '$1  (your monitor)'],
    [/^Se restaurará (.+)\. Antes se respalda lo actual\.( Las canciones importadas después de esa fecha dejarán de aparecer \(sus archivos quedan en DATA\)\.)?$/,
      (m, what, q) => `${restoreWhat(what)} will be restored. What there is now is backed up first.` + (q ? ' Songs imported after that date will stop showing up (their files stay in DATA).' : '')],
    [/^instalar (\d+) canción\(es\)$/, 'install $1 song(s)'], [/^eliminar: (.+)$/, 'delete: $1'], [/^antes de restaurar (.+)$/, 'before restoring $1'],
    [/^Faltan: (.+)\. Reinstala o repara el juego\.$/, 'Missing: $1. Reinstall or repair the game.'],
    [/^"(.+)" no está en la lista Download: solo se pueden borrar canciones descargadas o importadas\.$/, '"$1" is not in the Download list: only downloaded or imported songs can be deleted.']
  ];

  function restoreWhat(w) {
    if (w === 'el setlist original (sin canciones importadas)') return 'The original setlist (without imported songs)';
    let m = w.match(/^el setlist del (.+)$/); if (m) return `The setlist of ${m[1]}`;
    m = w.match(/^la partida guardada del (.+)$/); if (m) return `The save of ${m[1]}`;
    return w;
  }

  /* one text in English, or null: exact, then patterns, then " · " part by part */
  function tr1(s) {
    if (Object.prototype.hasOwnProperty.call(EN, s)) return EN[s];
    return null;
  }
  function translate(text, el) {
    const k = text.trim();
    if (!k || !/[A-Za-zÁÉÍÓÚÑáéíóúñ]/.test(k)) return null;
    let out = tr1(k);
    if (out == null)
      for (const [re, rep, where] of EN_RE) {
        if (where && !(el && el.closest && el.closest(where))) continue;
        if (re.test(k)) { out = k.replace(re, rep); break; }
      }
    if (out == null && k.includes(' · ')) {
      const parts = k.split(' · ');
      const done = parts.map(p => translate(p, el));
      if (done.some(x => x != null)) out = parts.map((p, i) => done[i] != null ? done[i] : p).join(' · ');
    }
    if (out == null || out === k) return null;
    return text.replace(k, out);
  }

  const ATTRS = ['title', 'placeholder', 'aria-label'];
  function walk(n) {
    if (n.nodeType === 3) {
      const t = translate(n.nodeValue, n.parentElement);
      if (t != null) n.nodeValue = t;
      return;
    }
    if (n.nodeType !== 1 || n.tagName === 'SCRIPT' || n.tagName === 'STYLE') return;
    for (const a of ATTRS) {
      const v = n.getAttribute(a);
      if (v) { const t = translate(v, n); if (t != null) n.setAttribute(a, t); }
    }
    for (const c of n.childNodes) walk(c);
  }

  let observer = null;
  function start(l) {
    lang = l === 'es' ? 'es' : 'en';
    document.documentElement.lang = lang;
    if (lang !== 'en' || observer) return;
    walk(document.body);
    // our own changes come back here too: an English text is not in EN, so it stops there
    observer = new MutationObserver(list => {
      for (const m of list) {
        if (m.type === 'childList') m.addedNodes.forEach(walk);
        else if (m.type === 'characterData') walk(m.target);
        else if (m.type === 'attributes') walk(m.target);
      }
    });
    observer.observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ATTRS });
  }

  /* ---- time played: the three biggest units, "2 days and 35 minutes", "1 year, 3 months and 2 days" ---- */
  const NAMES = {
    en: { year: ['year', 'years'], month: ['month', 'months'], week: ['week', 'weeks'], day: ['day', 'days'], hour: ['hour', 'hours'], minute: ['minute', 'minutes'], second: ['second', 'seconds'] },
    es: { year: ['año', 'años'], month: ['mes', 'meses'], week: ['semana', 'semanas'], day: ['día', 'días'], hour: ['hora', 'horas'], minute: ['minuto', 'minutos'], second: ['segundo', 'segundos'] }
  };
  function duration(seconds, max = 3) {
    let rest = Math.max(0, Math.floor(seconds || 0));
    const D = 86400, Y = 365.2425 * D, M = Y / 12;
    // under a month: weeks and days; from a month up: years, months, days
    const units = rest >= M ? [['year', Y], ['month', M], ['day', D], ['hour', 3600], ['minute', 60], ['second', 1]]
                            : [['week', 7 * D], ['day', D], ['hour', 3600], ['minute', 60], ['second', 1]];
    const parts = units.map(([u, size]) => { const n = Math.floor(rest / size + 1e-9); rest -= n * size; return [u, n]; });
    const first = parts.findIndex(p => p[1] > 0);
    const names = NAMES[lang] || NAMES.en;
    if (first < 0) return `0 ${names.second[1]}`;
    const shown = parts.slice(first, first + max).filter(p => p[1] > 0).map(([u, n]) => `${n} ${names[u][n === 1 ? 0 : 1]}`);
    const and = lang === 'es' ? ' y ' : ' and ';
    return shown.length > 1 ? shown.slice(0, -1).join(', ') + and + shown[shown.length - 1] : shown[0];
  }

  /* "3 days ago" style, for the last play */
  function ago(date) {
    const s = (Date.now() - new Date(date).getTime()) / 1000;
    if (!isFinite(s)) return '';
    if (s < 60) return lang === 'es' ? 'hace un momento' : 'just now';
    const d = duration(s, 1);
    return lang === 'es' ? `hace ${d}` : `${d} ago`;
  }

  return { start, duration, ago, get lang() { return lang; } };
})();
