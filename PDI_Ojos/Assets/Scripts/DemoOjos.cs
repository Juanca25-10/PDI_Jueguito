using UnityEngine;

/// <summary>Muestra en pantalla lo que detecta la cámara. No necesita Canvas ni UI.</summary>
public class DemoOjos : MonoBehaviour
{
    public OjosReceiver ojos;

    void OnGUI()
    {
        GUI.skin.label.fontSize = 28;
        string estado = !ojos.HayRostro ? "SIN ROSTRO" : ojos.Cerrados ? "OJOS CERRADOS" : "OJOS ABIERTOS";
        GUI.Label(new Rect(20, 20, 900, 40), estado);
        GUI.Label(new Rect(20, 60, 900, 40),
            $"P(cerrados): {ojos.PCerrados:F2}   Parpadeos: {ojos.Parpadeos}   Cerrados hace: {ojos.SegCerrados:F1}s");
    }

    // Ejemplo para conectar a un evento desde el Inspector
    public void AvisarCierreLargo() { Debug.Log("¡Cerraste los ojos demasiado tiempo!"); }
}
