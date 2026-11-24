using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Controles según lo que puedes hacer ahora (explorar o el turno). Se ven al empezar cada fase y se recogen
    /// solos en una etiqueta "H  Controles"; H los vuelve a abrir o cerrar.
    /// </summary>
    public class HudControles : MonoBehaviour
    {
        [SerializeField] CanvasGroup explore, play, compact;
        [SerializeField] GameObject keyboardRow, cameraRow;
        [SerializeField] TMP_Text interactKey, closeKey;
        [SerializeField] Key toggleKey = Key.H;
        [SerializeField] float visibleSeconds = 16f;

        bool expanded = true;
        float timer;
        GameManager.Fase last = (GameManager.Fase)(-1);

        void Start()
        {
            HudUtil.Set(explore, 0f);
            HudUtil.Set(play, 0f);
            HudUtil.Set(compact, 0f);
        }

        void Update()
        {
            var hud = HudMuseo.Instance;
            var gm = hud != null ? hud.Game : null;
            if (gm == null) return;

            if (hud.Phase != last) { last = hud.Phase; expanded = true; timer = 0f; }
            if (hud.Ready && !HudPausa.Paused && HudUtil.Down(toggleKey)) { expanded = !expanded; timer = 0f; }
            if (hud.Ready) timer += Time.unscaledDeltaTime;
            if (expanded && timer > visibleSeconds) expanded = false;

            bool exploring = hud.Phase == GameManager.Fase.Explorando, playing = hud.Phase == GameManager.Fase.Jugando;
            HudUtil.Fade(explore, exploring && expanded ? 1f : 0f, 0.35f);
            HudUtil.Fade(play, playing && expanded ? 1f : 0f, 0.35f);
            HudUtil.Fade(compact, (exploring || playing) && !expanded ? 0.8f : 0f, 0.35f);

            if (interactKey != null) interactKey.text = HudUtil.KeyName(gm.teclaInteractuar);
            if (closeKey != null) closeKey.text = HudUtil.KeyName(gm.teclaCerrarOjos);
            if (keyboardRow != null && keyboardRow.activeSelf != gm.modoTeclado) keyboardRow.SetActive(gm.modoTeclado);
            if (cameraRow != null && cameraRow.activeSelf == gm.modoTeclado) cameraRow.SetActive(!gm.modoTeclado);
        }
    }
}
