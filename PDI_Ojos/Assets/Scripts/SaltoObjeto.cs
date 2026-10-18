using System.Collections;
using UnityEngine;

public class SaltoObjeto : MonoBehaviour
{
    [Header("Configuración del Salto")]
    [SerializeField] private float alturaSalto = 1.5f; // Qué tan alto sube
    [SerializeField] private float duracionSalto = 0.4f; // Cuánto tarda en total (subir y bajar)

    private bool estaSaltando = false;
    private Vector3 posicionInicial;

    void Start()
    {
        // Guardamos la posición original del objeto para regresar exactamente ahí
        posicionInicial = transform.localPosition;
    }

    // --- MÈTODO PÚBLICO LISTO PARA SER LLAMADO ---
    public void DarSaltito()
    {
        // Evita que se vuelva a activar si ya está saltando
        if (!estaSaltando)
        {
            StartCoroutine(RutinaSalto());
        }
    }

    private IEnumerator RutinaSalto()
    {
        estaSaltando = true;
        float tiempoPasado = 0f;

        while (tiempoPasado < duracionSalto)
        {
            tiempoPasado += Time.deltaTime;

            // Calculamos el progreso (va de 0 a 1)
            float progreso = tiempoPasado / duracionSalto;

            // Usamos una función matemática (Seno) para recrear una curva de salto fluida (sube y baja)
            float alturaActual = Mathf.Sin(progreso * Mathf.PI) * alturaSalto;

            // Aplicamos la nueva posición manteniendo el eje X y Z originales
            transform.localPosition = new Vector3(posicionInicial.x, posicionInicial.y + alturaActual, posicionInicial.z);

            yield return null; // Espera al siguiente frame
        }

        // Aseguramos que vuelva exactamente a su posición inicial al terminar
        transform.localPosition = posicionInicial;
        estaSaltando = false;
    }
}
