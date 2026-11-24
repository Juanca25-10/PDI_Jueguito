using System.Collections;
using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CuandoNadieMira.Juego
{
    /// <summary>Utilidades compartidas por las piezas del HUD del juego.</summary>
    public static class HudUtil
    {
        /// <summary>Lleva el alfa de un grupo hacia el objetivo. Usa tiempo real: funciona en pausa.</summary>
        public static void Fade(CanvasGroup g, float target, float seconds)
        {
            if (g == null) return;
            g.alpha = Mathf.MoveTowards(g.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
            bool on = target > 0.5f && g.alpha > 0.5f;
            if (g.blocksRaycasts != on) g.blocksRaycasts = on;
            if (g.interactable != on) g.interactable = on;
        }

        public static void Set(CanvasGroup g, float alpha)
        {
            if (g == null) return;
            g.alpha = alpha;
            g.blocksRaycasts = g.interactable = alpha > 0.5f;
        }

        public static bool Pressed(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;
        public static bool Down(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;

        /// <summary>Nombre corto de una tecla para mostrarla en una tecla dibujada.</summary>
        public static string KeyName(Key k) => k switch
        {
            Key.Space => "Espacio",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.Escape => "Esc",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            _ => k.ToString().ToUpperInvariant(),
        };

        /// <summary>La barra 0-100 convertida en la hora del museo: de 00:00 a 06:00.</summary>
        public static string Clock(float progress)
        {
            int minutes = Mathf.Clamp(Mathf.FloorToInt(progress / 100f * 360f), 0, 360);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        public static string Duration(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        public static void FreeCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Funde a negro y carga otra escena (menú principal o calibración).</summary>
        public static IEnumerator LoadScene(string scene, CanvasGroup black)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (black != null)
            {
                black.blocksRaycasts = true;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
                {
                    black.alpha = t;
                    AudioListener.volume = Mathf.Lerp(SettingsStore.MasterVolume, 0f, t);
                    yield return null;
                }
                black.alpha = 1f;
            }
            AudioListener.volume = SettingsStore.MasterVolume;
            SceneTransition.LoadAsync(scene);
        }
    }
}
