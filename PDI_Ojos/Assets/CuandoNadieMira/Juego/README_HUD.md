# HUD del juego (Cuando Nadie Mira)

Interfaz en partida con la misma estética del menú: fuentes IM Fell / Cormorant / Cinzel, paneles negros translúcidos con borde fino, ornamentos e iconos del menú.

## Cómo se construye

- Menú **Cuando Nadie Mira → Construir HUD del juego**.
- Crea el prefab `Prefabs/HUD_Museo.prefab` y lo coloca en `Assets/Scenes/Juego_Estatuas.unity`.
- Oculta, sin borrarlos, el HUD anterior (`CanvasHUD`) y el panel `CanvasParpados/PanelContenedor`. Los párpados siguen funcionando.
- Para volver al HUD anterior: activa esos dos objetos y quita `HUD_Museo`.
- El HUD es un Canvas **Overlay** con Sort Order 10. Se dibuja encima de los párpados y no pasa por el efecto de película.
- Lee `_Barras` del material `Cuando Nadie Mira_Pelicula Antigua 1` y se queda dentro de la imagen, sin montarse sobre las franjas negras.

No modifica `GameManager` ni `HudJuego`: solo **lee** el estado del juego.

## Qué muestra (todo es dato real)

| Pieza | Dónde | De dónde sale |
|---|---|---|
| **Objetivo** | arriba a la izquierda | Fase del `GameManager`. Se atenúa a los 12 s. |
| **Hasta el amanecer** | arriba al centro | `GameManager.progreso` como reloj del museo (00:00 → 06:00). Las marcas en la barra indican dónde despierta cada estatua (`activaDesdeProgreso`). Debajo dice hacia dónde va la noche, según el cambio real de la barra. |
| **Presencias** | arriba a la derecha | Una fila por estatua de `Estatua.Todas`: nombre, pose actual / última, y estado (dormida, en su sitio, se acercó, retrocede, muy cerca). Se puede apagar con `showPresences` en `HudAmanecer`. |
| **Lectura de ojos** | abajo a la izquierda | Tu cámara (puerto 5006); un ojo que se cierra según P(cerrados) de la red; el estado (Abiertos / Parpadeo / Cerrados / Sin rostro / Sin señal) con el mismo criterio del `GameManager`; la barra P con tus umbrales; si hay calibración personal; los parpadeos, y un punto "en vivo" que se enciende con cada paquete de la red. En modo teclado muestra la tecla. |
| **Controles** | abajo a la derecha | Cambian al explorar y durante el turno. Se recogen solos a los 16 s; **H** los abre o los cierra. |
| **Comenzar el turno** | abajo al centro | Aparece cerca del escritorio (`puntoInicio` / `radioInicio`) con la tecla real (`teclaInteractuar`). |
| **Carteles** | centro | "El museo cierra", "Comienza el turno", "Otra noche". |
| **Te perdimos de vista** | centro | Durante el turno sin rostro: el motivo que manda la red (rostro pequeño, cortado, varios rostros...) y qué hacer. La partida ya la pausa el `GameManager`. |
| **Pausa (Esc)** | pantalla completa | Continuar, Controles, Recalibrar los ojos (va a Calibración y luego vuelve al juego) y Menú principal. Congela el tiempo y el audio y suelta el ratón. La lectura de ojos sigue en vivo. |
| **Victoria / Derrota** | pantalla completa | Quién te alcanzó (o "La noche se hizo eterna"), su imagen de derrota, la duración del turno, el tiempo con los ojos cerrados y los parpadeos. **R** o el botón para reintentar; también se puede volver al menú. |

## Scripts (`Scripts/`)

- `HudMuseo`: coordina las fases, el objetivo, el aviso de inicio, los carteles, la pérdida de rostro y las estadísticas.
- `HudOjos`: la lectura de ojos.
- `HudAmanecer`: la barra del amanecer y las presencias.
- `HudControles`: los controles contextuales.
- `HudPausa`: la pausa.
- `HudFinal`: la victoria y la derrota.
- `HudUtil`: los fundidos, los nombres de las teclas, el reloj y el cambio de escena.

Los textos (objetivos y mensajes de derrota) se pueden editar en el Inspector de `HudMuseo` y `HudFinal`.

## Probar sin cámara

- **Video de prueba:** define la variable de entorno `CNM_VIDEO_PRUEBA` con la ruta de un video, o lanza el juego con `-cnmVideo <ruta>`. `LanzadorPython` se lo pasa a `ojos_server` y lo repite en bucle. El HUD muestra "LECTURA · VIDEO DE PRUEBA".
- **Modo teclado:** marca `modoTeclado` en el `GameManager` y mantén **C** para cerrar los ojos.
