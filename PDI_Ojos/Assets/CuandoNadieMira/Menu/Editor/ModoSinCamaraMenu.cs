using UnityEditor;

namespace CuandoNadieMira.Menu.EditorTools
{
    /// <summary>Activa o desactiva el modo sin cámara (C = cerrar los ojos, F = sin rostro) para probar en el editor.</summary>
    static class ModoSinCamaraMenu
    {
        const string Item = "Cuando Nadie Mira/Modo sin cámara (teclado)";

        [MenuItem(Item, false, 200)]
        static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(SimuladorOjos.PrefKey, false);
            EditorPrefs.SetBool(SimuladorOjos.PrefKey, on);
            UnityEngine.Debug.Log(on
                ? "[Cuando Nadie Mira] Modo sin cámara ACTIVADO. Dale a Play: mantén C para cerrar los ojos, F para simular que no hay rostro."
                : "[Cuando Nadie Mira] Modo sin cámara desactivado: se usará la cámara (o el video de prueba).");
        }

        [MenuItem(Item, true)]
        static bool Validate()
        {
            UnityEditor.Menu.SetChecked(Item, EditorPrefs.GetBool(SimuladorOjos.PrefKey, false));
            return !EditorApplication.isPlaying;
        }
    }
}
