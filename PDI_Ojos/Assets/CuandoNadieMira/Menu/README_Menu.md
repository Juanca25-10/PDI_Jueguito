# Menú — *Cuando nadie mira*

Sala de museo en 3D con la UI dentro del espacio. Las piezas del fondo se disuelven en destellos sutiles, desaparecen unos segundos y vuelven (a veces más cerca).

El menú tiene tres pantallas, todas en el mismo salón:

| Escena | Qué hace | ¿Usa la cámara? |
|---|---|---|
| `MainMenu` | Menú principal, controles, accesibilidad, créditos, salir | No (solo mira si hay alguna cámara conectada) |
| `Calibracion` | Calibra los ojos del jugador con la red (`ojos_server.exe`) | Sí |
| `Carga` | *Preparando la experiencia*: comprueba cámara, calibración y seguimiento, y carga el juego | Sí, y la suelta antes de abrir el juego |

Después de `Carga` viene `Assets/Scenes/Juego_Estatuas.unity`, que abre su propio detector (`LanzadorPython` + `OjosReceiver`).

## Cómo abrir y probar

1. Descomprime `ojos_server.zip` en `Assets/StreamingAssets/` (queda `Assets/StreamingAssets/ojos_server/ojos_server.exe`). Esa carpeta no se sube a GitHub.
2. Abre `Assets/CuandoNadieMira/Menu/Scenes/MainMenu.unity` y dale **Play**.
3. *Build Settings* ya tiene el orden correcto: `MainMenu`, `Calibracion`, `Carga`, `Juego_Estatuas`.

Las tres escenas se generan por código:

- **Cuando Nadie Mira → Construir menú completo (todas las pantallas)**: regenera las tres.
- **Cuando Nadie Mira → Construir solo Calibración y Carga**: deja `MainMenu` como está.
- **Cuando Nadie Mira → Borrar calibración guardada**: vuelve al estado "sin calibrar".

Ojo: construir **reemplaza** las escenas, así que cualquier cambio hecho a mano en ellas se pierde (los scripts, materiales y texturas se conservan). `Calibracion` y `Carga` parten de una copia de `MainMenu`, así que se construyen después de él.

### Probar sin cámara web

Si el PC no tiene cámara, el detector puede leer un video en su lugar. Antes de abrir Unity, define la variable de entorno `CNM_VIDEO_PRUEBA` con la ruta del video (o pasa `-cnmVideo "ruta.mp4"` al ejecutable):

```powershell
$env:CNM_VIDEO_PRUEBA = "C:\ruta\video_ojos.mp4"
& "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -projectPath "C:\ruta\PDI_Ojos"
```

El video se repite solo cuando termina, y el indicador de arriba a la derecha dice **Cámara: video de prueba** para que nadie lo confunda con una cámara real. Con un video donde la persona no cierra los ojos, la calibración termina en *No pudimos calibrar tus ojos*. Es lo esperado, porque nunca se inventan datos.

## Controles

| Acción | Teclado / ratón | Mando |
|---|---|---|
| Moverse entre botones | Flechas, W/S, ratón | D-pad, stick izquierdo |
| Elegir | Enter, clic | Botón Sur (A) |
| Volver / foco en "Iniciar" | Esc | Botón Este (B) |
| Mover la mirada de la cámara | Ratón | Stick derecho |

## Estructura

```
Assets/CuandoNadieMira/Menu/
  Scenes/MainMenu.unity, Calibracion.unity, Carga.unity
  Scripts/        (runtime, namespace CuandoNadieMira.Menu)
    Tracking/     servicio del detector, calibración guardada, textos de estado
    Flow/         Calibración, Carga, "Se perdió el seguimiento" y piezas de UI
  Editor/         MenuSceneBuilder (menú principal), MenuFlowBuilder (Calibración y Carga),
                  generadores de texturas/materiales
  Shaders/        CNM_SilhouetteDissolve, CNM_GlassDissolve, CNM_UIGrayscale (imagen de la cámara)
  Materials/, Settings/MainMenu_PostProcess.asset
  Models/         modelos descargados (+ Generated/: arcos y tunjo)
  Textures/       texturas descargadas (+ Generated/: iconos, ruido, grietas...)
  Fonts/          TTF + licencias OFL + fuentes TMP generadas
```

| Script | Qué hace |
|---|---|
| `MenuController` | Estados (Intro, Idle, Panel, Transition), navegación con *wrap*, paneles, opciones, avisos |
| `MenuButton` | Hover/foco/presionado, brasas, sonido, pieza asociada |
| `MuseumExhibit` | Luz de la pieza y su microanimación (vitrina, espejo, placa, libro, puerta) |
| `MenuCameraRig` | Respiración, paralaje, profundidad de campo hacia la pieza activa |
| `CursorLight` / `CursorEye` | Luz que sigue al cursor y cursor de ojo (mira hacia donde mueves el ratón, parpadea y se cierra si nadie lo mueve) |
| `FadeableProp` / `FadeDirector` / `SparkleEmitter` | Ciclo de desvanecimiento, límite de 2 simultáneos, huellas y pool de destellos |
| `TitleAnimator` | Ramas que crecen, brillo cada 12 s con relámpago y trueno |
| `PanelFlipper` | Giro de 180° de la placa para Controles, Créditos, Accesibilidad y Salir |
| `SceneTransition` | Velas que se apagan, dolly, fundido, latido y carga asíncrona |
| `MenuAudio` / `AmbientSound` | Efectos suaves sintetizados por código, subtítulos, audio 3D con paso bajo |
| `MenuMusic` | Música ambiental en re menor generada en tiempo real (bordón, acordes lentos, caja de música con eco) |
| `OptionRow` | Fila de opción con interruptor, barra de volumen o selector |
| `SettingsStore` | Opciones guardadas en `PlayerPrefs` |
| `EyeTrackingService` | Abre `ojos_server.exe`, recibe por UDP la probabilidad (puerto 5005) y la imagen (5006), guarda el historial y detecta fallos (sin exe, puerto ocupado, sin cámara, detector cerrado). Sobrevive entre Calibración y Carga y se apaga solo al entrar a una escena sin `EyeTrackingClient` |
| `EyeTrackingClient` | Se pone en una escena que necesita el detector; lo enciende si hace falta |
| `CalibrationStore` | Calcula y guarda los umbrales personales (ver abajo) |
| `TrackingText` | Mensajes en español para cada estado y cada `motivo` del detector |
| `CalibrationController` | Pantalla de Calibración completa (fases, prueba de parpadeo, resultados, elección de cámara) |
| `LoadingController` | *Preparando la experiencia*: comprobaciones reales, carga asíncrona del juego con progreso real |
| `TrackingLostPanel` | Pantalla "Se perdió el seguimiento", compartida por Calibración y Carga |
| `CameraFeedView`, `EyeMeter`, `StatusChip`, `WebcamStatusIndicator` | Imagen de la cámara con guía de cabeza, barra de lectura de los ojos, indicadores de estado y de cámara |
| `MenuFlow` / `MenuNav` | Nombres de escena, intención (jugar o solo calibrar) y navegación con teclado/mando |

## Parámetros que conviene ajustar

| Qué | Dónde |
|---|---|
| Escenas de destino (`Calibracion`) | `_Systems → MenuController → Start Scene / Calibration Scene` |
| Ritmo de desaparición | Cada pieza en `FadeableProps`: `Visible Delay` (8–16 s), `Hidden Duration` (3–8 s), `Relocate Chance` (25 %) |
| Máximo simultáneo y primera desaparición | `_Systems → FadeDirector` |
| Cantidad de destellos | `_Systems → SparkleEmitter → Particles Per Object` |
| Aspecto de silueta | Materiales `SIL_*`: `Silueta` (0 = material original, 1 = negro), `Color de borde`, `Potencia de borde` |
| Color y ancho del borde de disolución | Materiales `SIL_*`: `Color del borde`, `Ancho de borde`, `Escala del ruido` |
| Movimiento de cámara | `CameraRig → MenuCameraRig` |
| Postproceso | `Settings/MainMenu_PostProcess.asset` |
| Luces de cada pieza | `Environment/Exhibits/*` |

**ColorAccent:** la escena es monocroma porque `Color Adjustments → Saturation = -100` en el perfil de postproceso. Para dejar entrar color (por ejemplo, el oro del tunjo), sube ese valor (−70 a −40 da un acento tenue).

## Accesibilidad

Desde **Controles → Accesibilidad**, agrupado en tres secciones: reducir movimiento (la cámara se mueve al 20 %), desactivar destellos (la disolución pasa a ser un desvanecido simple y el relámpago se suaviza), volumen general, música y efectos, subtítulos de sonidos, tamaño de texto (100 % / 125 %) y calidad baja. Cada opción muestra su control (interruptor, barra o selector) y una línea de ayuda; se ajusta con izquierda/derecha, Enter o clic. Todo se guarda entre sesiones.

Todas las pantallas secundarias tienen un botón **Inicio** arriba a la derecha (atajo: Esc).

**Calidad baja** apaga la niebla, el polvo, los haces de luz y la sonda de reflejos en tiempo real, y reduce los destellos a la mitad.

## Flujo entre pantallas

```
MainMenu ─ Iniciar experiencia ─┬─ (sin cámara) aviso en la placa, foco en Calibración
                                ├─ (sin calibrar) → Calibracion → Carga → Juego_Estatuas
                                └─ (calibrado)    → Carga → Juego_Estatuas
MainMenu ─ Calibración ─→ Calibracion → (al terminar) Volver al menú
```

Si una escena no existe o no está en *Build Settings*, la pantalla muestra un aviso y se queda donde está.

## Calibración

1. **Posición**: el jugador se sienta frente a la cámara hasta que la red ve su rostro de forma estable 2 s (con el motivo `ok`).
2. **Ojos abiertos**: cuenta regresiva y luego 3 s de muestras mirando al centro.
3. **Ojos cerrados**: suena una campana, 3 s con los ojos cerrados y otra campana para abrirlos.
4. **Prueba**: se pide parpadear 2 veces con los umbrales nuevos (se puede omitir).

Con las muestras se calcula, para la probabilidad P(cerrados) que entrega la red:

- `umbralCierre` está al 55 % del camino entre "abiertos" (percentil 85) y "cerrados" (percentil 20).
- `umbralApertura` está al 30 % de ese mismo camino, siempre por debajo del de cierre (histéresis).
- La calidad (Excelente / Buena / Aceptable) depende de la separación entre las dos medias. Si hay muy pocas muestras o la separación es muy pequeña, la calibración **no se guarda** y se explica por qué.

Si el rostro se pierde más de 2,5 s durante una fase, aparece **Se perdió el seguimiento**. Desde ahí se puede reintentar la calibración, continuar cuando el rostro vuelve a estar estable, o volver al menú. Esc vuelve atrás o cancela.

Si hay más de una cámara, aparece un botón para elegirla. La elección también la usa el juego (`LanzadorPython` añade `--camara N`).

### Claves en PlayerPrefs

| Clave | Uso |
|---|---|
| `cnm.calib.ok` | 1 si hay calibración válida |
| `cnm.calib.umbralCierre`, `cnm.calib.umbralApertura` | Umbrales que lee `OjosReceiver` al iniciar el juego (`usarCalibracion`, activo por defecto) |
| `cnm.calib.pAbiertos`, `cnm.calib.pCerrados`, `cnm.calib.calidad`, `cnm.calib.fecha`, `cnm.calib.parpadeos` | Resultados que se muestran en Calibración |
| `cnm.camara` | Índice de cámara elegido |

## Preparando la experiencia

Comprueba tres cosas reales: cámara (según el detector), calibración guardada y seguimiento estable durante 1 s. Cuando las tres llevan 1,2 s listas, apaga el detector del menú (para que el juego pueda abrir la cámara y los puertos) y carga `Juego_Estatuas` con progreso real. Si falta la calibración, o si pasan 8 s sin que todo esté listo, ofrece **Entrar de todos modos**, **Calibrar** y **Volver al menú**.

## Decisiones técnicas

- **Shaders en HLSL (URP)** en lugar de Shader Graph: mismas propiedades (`_Dissolve`, `_EdgeWidth`, `_EdgeColor`, `_NoiseScale`, `_NoiseSeed`), con variante transparente para el vidrio.
- **Destellos con Shuriken** (pool de 4 sistemas, máx. 150 partículas cada uno) en lugar de VFX Graph: es más liviano para la GPU integrada mientras corre el detector facial.
- **Cámara propia** (`MenuCameraRig`) en lugar de Cinemachine: no agrega paquetes.
- **Profundidad de campo gaussiana** y **UI en una cámara superpuesta**: el texto queda siempre nítido y legible; el fondo se desenfoca detrás del foco.
- **Sin lightmaps horneados**: solo luces en tiempo real (Forward+). Si se quiere hornear la arquitectura, marca `Environment/Architecture` como *Static* y genera la iluminación en *Window → Rendering → Lighting*.
- Luces con sombra: solo el ventanal y la luz cenital de la vitrina.

## Assets externos y licencias

Solo CC0 y OFL.

**Modelos y texturas — Poly Haven (CC0)**, https://polyhaven.com

| Asset | Autor(es) |
|---|---|
| Gothic Statue | Benny Weimer |
| Marble Bust 01, Horse Statue 01 | Rico Cilliers |
| Lion Head, Large Castle Door | Tina |
| Fancy Picture Frame 01 y 02 | Rob Tuytel, Rico Cilliers |
| Ornate Mirror 01, Antique Ceramic Vase 01 | James Ray Cock |
| Wooden Candlestick | Josh Dean |
| Chandelier 01, Painted Wooden Bench, Gothic Cabinet 01, Arm Chair 01 | Kirill Sannikov |
| Vintage Grandfather Clock 01 | Yann Kervran, James Ray Cock |
| Marble Tiles (textura) | Charlotte Baglioni |
| Plastered Wall 04 (textura) | Rob Tuytel |
| Velour Velvet (textura) | colormass, Rico Cilliers |

**Fuentes — SIL Open Font License 1.1** (licencias en `Fonts/*_OFL.txt`)

- IM Fell English — Igino Marini
- Cormorant Garamond — Christian Thalmann
- Cinzel — Natanael Gama

**Propios del proyecto:** la textura del cuadro de La Llorona sale del mockup del equipo. Los iconos, texturas de ruido, grietas, sprites de UI, el tunjo, los arcos y todos los sonidos se generan por código.

## Créditos

Los nombres del equipo, el curso y la universidad están como `[Nombre]`, `[Curso]` y `[Universidad]` en `MenuSceneBuilder.CreditsText()`. Cámbialos ahí y regenera la escena, o edita directamente el texto `UI/Canvas_WorldPlate/Back/Panel_Credits/Viewport/Content`.
