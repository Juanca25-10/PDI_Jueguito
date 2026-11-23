using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Marca una escena del menú que usa el detector (Calibración, Preparando la experiencia) y lo arranca al entrar.
    /// Al cargar una escena sin este componente, <see cref="EyeTrackingService"/> se apaga solo.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EyeTrackingClient : MonoBehaviour
    {
        [Tooltip("Dibujar la caja de la cara y la banda de ojos sobre la imagen de la cámara.")]
        [SerializeField] bool drawBoxes = true;

        [Tooltip("Solo para probar sin cámara: ruta a un video (por ejemplo video_ojos.mp4). Vacío = cámara real.\n" +
                 "También sirve la variable de entorno CNM_VIDEO_PRUEBA o el argumento -cnmVideo <ruta>.")]
        [SerializeField] string testVideo = "";

        public EyeTrackingService Service { get; private set; }

        void Awake() => Service = EyeTrackingService.Ensure(testVideo, drawBoxes);

        /// <summary>Vuelve a abrir el detector (por ejemplo, después de conectar la cámara).</summary>
        public void Restart()
        {
            if (Service == null) Service = EyeTrackingService.Ensure(testVideo, drawBoxes);
            else Service.Restart(testVideo, drawBoxes);
        }
    }
}
