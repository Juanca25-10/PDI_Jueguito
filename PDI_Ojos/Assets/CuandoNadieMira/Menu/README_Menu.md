# Menú principal — *Cuando nadie mira*

Sala de museo en 3D con la UI dentro del espacio. Las piezas del fondo se disuelven en destellos sutiles, desaparecen unos segundos y vuelven (a veces más cerca). El menú **no usa la cámara web**: la detección de ojos (`OjosReceiver`, `ojos_server.exe`) queda para las escenas de juego.

## Cómo abrir y probar

1. Abre `Assets/CuandoNadieMira/Menu/Scenes/MainMenu.unity`.
2. Dale **Play**. No hay pasos manuales.
3. La escena ya está en *Build Settings*. Para que sea la primera escena del juego, arrástrala a la posición 0 en *File → Build Profiles*.

La escena se genera por código. Si cambias el constructor o los assets, puedes regenerarla desde **Cuando Nadie Mira → Construir menú principal**. Ojo: eso **reemplaza** `MainMenu.unity`, así que cualquier cambio hecho a mano en esa escena se pierde (los scripts, materiales y texturas se conservan).

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
  Scenes/MainMenu.unity
  Scripts/        (runtime, namespace CuandoNadieMira.Menu)
  Editor/         (constructor de escena y generadores de texturas/materiales)
  Shaders/        CNM_SilhouetteDissolve.shader, CNM_GlassDissolve.shader
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

## Escenas de destino

"Iniciar experiencia" y "Calibración" cargan `Calibracion` de forma asíncrona. Si la escena todavía no existe o no está en *Build Settings*, la placa muestra un aviso y el menú vuelve a su estado. En el editor basta con que la escena exista en el proyecto con ese nombre.

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
