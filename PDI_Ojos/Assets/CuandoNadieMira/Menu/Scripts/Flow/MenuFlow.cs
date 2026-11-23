using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>Por qué se abrió Calibración: para jugar después o solo para calibrar y volver al menú.</summary>
    public enum FlowIntent { Play, CalibrateOnly }

    /// <summary>Recorrido entre las pantallas del menú y nombres de escenas.</summary>
    public static class MenuFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string CalibrationScene = "Calibracion";
        public const string LoadingScene = "Carga";
        public const string DefaultGameScene = "Juego_Estatuas";

        /// <summary>Lo fija el menú principal antes de abrir Calibración.</summary>
        public static FlowIntent Intent = FlowIntent.Play;
    }

    /// <summary>Navegación con teclado y mando compartida por las pantallas del menú (orden de foco con wrap).</summary>
    public static class MenuNav
    {
        /// <summary>Enlaza el foco entre los botones visibles y activos, en el orden dado.</summary>
        public static void Wire(IEnumerable<Selectable> buttons, bool horizontal = false)
        {
            var list = buttons.Where(b => b != null && b.isActiveAndEnabled).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var prev = list[(i - 1 + list.Count) % list.Count];
                var next = list[(i + 1) % list.Count];
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (horizontal) { nav.selectOnLeft = prev; nav.selectOnRight = next; }
                nav.selectOnUp = prev;
                nav.selectOnDown = next;
                list[i].navigation = nav;
            }
        }

        public static void Focus(Selectable s)
        {
            if (s == null || EventSystem.current == null || !s.isActiveAndEnabled) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        public static GameObject Selected => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        /// <summary>Esc o botón Este del mando.</summary>
        public static bool BackPressed =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        static bool NavPressed =>
            (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame
                || Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame
                || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame
                || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
            || (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.3f
                || Gamepad.current.buttonSouth.wasPressedThisFrame));

        /// <summary>Si se perdió la selección (clic en el vacío) o el botón enfocado se ocultó, la recupera.</summary>
        public static void KeepSelection(Selectable fallback)
        {
            var sel = Selected;
            if (sel != null && sel.activeInHierarchy) return;
            if (sel != null || NavPressed) Focus(fallback);
        }
    }
}
