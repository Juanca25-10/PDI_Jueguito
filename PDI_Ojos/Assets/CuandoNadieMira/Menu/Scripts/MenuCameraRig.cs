using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Cámara con vida: respiración (ruido Perlin), paralaje con el cursor o el stick derecho
    /// y cambio de foco de la profundidad de campo hacia la pieza activa.
    /// </summary>
    public class MenuCameraRig : MonoBehaviour
    {
        [Header("Respiración")]
        [SerializeField] float breathPosition = 0.01f;
        [SerializeField] float breathRotation = 0.15f;
        [SerializeField] float breathFrequency = 0.1f;

        [Header("Paralaje")]
        [SerializeField] float parallaxDegrees = 2.5f;
        [SerializeField] float parallaxTranslation = 0.06f;
        [SerializeField] float parallaxDamping = 0.6f;

        [Header("Placa de UI (se mueve menos que el fondo)")]
        [SerializeField] Transform uiPlate;
        [SerializeField, Range(0f, 1f)] float plateFollow = 0.7f;

        [Header("Profundidad de campo")]
        [SerializeField] Volume volume;
        [SerializeField] float defaultFocusDistance = 7f;
        [SerializeField] float focusTransition = 0.5f;
        [SerializeField] float blurRange = 9f;

        Vector3 basePos;
        Quaternion baseRot;
        Vector2 look, lookVel;
        Vector3 plateLocalToCam;
        Quaternion plateRotToCam;
        Vector3 plateWorldPos;
        Quaternion plateWorldRot;
        DepthOfField dof;
        Transform focusTarget;
        float focusDistance, focusVel;

        /// <summary>Desplazamiento extra en espacio local (lo usa la transición para el dolly).</summary>
        public Vector3 DollyOffset { get; set; }

        void Start()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
            focusDistance = defaultFocusDistance;
            if (uiPlate != null)
            {
                plateWorldPos = uiPlate.position;
                plateWorldRot = uiPlate.rotation;
                plateLocalToCam = Quaternion.Inverse(baseRot) * (plateWorldPos - basePos);
                plateRotToCam = Quaternion.Inverse(baseRot) * plateWorldRot;
            }
            if (volume != null && volume.profile.TryGet(out dof))
            {
                dof.mode.overrideState = true;
                dof.mode.value = DepthOfFieldMode.Gaussian;
            }
        }

        public void SetFocus(Transform target) => focusTarget = target;

        void LateUpdate()
        {
            float motion = SettingsStore.MotionScale;
            float t = Time.time * breathFrequency;

            // Entrada normalizada en [-1, 1]
            Vector2 input = Vector2.zero;
            if (Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.01f)
                input = Gamepad.current.rightStick.ReadValue();
            else if (Mouse.current != null)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                input = new Vector2(m.x / Mathf.Max(1, Screen.width) * 2f - 1f, m.y / Mathf.Max(1, Screen.height) * 2f - 1f);
                input = Vector2.ClampMagnitude(input, 1.4f);
            }
            look = Vector2.SmoothDamp(look, input, ref lookVel, parallaxDamping);

            // Ruido Perlin de baja amplitud: la cámara "respira"
            Vector3 breathPos = new Vector3(
                Mathf.PerlinNoise(t, 0.1f) - 0.5f,
                Mathf.PerlinNoise(0.3f, t) - 0.5f,
                Mathf.PerlinNoise(t, 0.7f) - 0.5f) * (2f * breathPosition * motion);
            Vector3 breathRot = new Vector3(
                Mathf.PerlinNoise(t + 3f, 1.1f) - 0.5f,
                Mathf.PerlinNoise(1.7f, t + 5f) - 0.5f,
                Mathf.PerlinNoise(t + 9f, 2.3f) - 0.5f) * (2f * breathRotation * motion);

            Quaternion parallax = Quaternion.Euler(-look.y * parallaxDegrees * motion, look.x * parallaxDegrees * motion, 0f);
            Vector3 shift = baseRot * new Vector3(look.x, look.y, 0f) * (parallaxTranslation * motion);

            transform.SetPositionAndRotation(
                basePos + breathPos + shift + baseRot * DollyOffset,
                baseRot * parallax * Quaternion.Euler(breathRot));

            // La placa sigue parcialmente a la cámara: se desplaza menos que el fondo
            if (uiPlate != null)
            {
                Vector3 followPos = transform.position + transform.rotation * plateLocalToCam;
                Quaternion followRot = transform.rotation * plateRotToCam;
                uiPlate.SetPositionAndRotation(
                    Vector3.Lerp(plateWorldPos + baseRot * DollyOffset, followPos, plateFollow),
                    Quaternion.Slerp(plateWorldRot, followRot, plateFollow) * uiPlateLocalRotation);
            }

            UpdateFocus();
        }

        /// <summary>Rotación extra de la placa (giro de paneles), la aplica <see cref="PanelFlipper"/>.</summary>
        public Quaternion uiPlateLocalRotation = Quaternion.identity;

        void UpdateFocus()
        {
            if (dof == null) return;
            float target = focusTarget != null ? Vector3.Distance(transform.position, focusTarget.position) : defaultFocusDistance;
            focusDistance = Mathf.SmoothDamp(focusDistance, target, ref focusVel, focusTransition * 0.4f);
            // Gaussiano: la placa cercana queda nítida y el fondo se desenfoca a partir del foco
            dof.gaussianStart.overrideState = true;
            dof.gaussianEnd.overrideState = true;
            dof.gaussianStart.value = focusDistance + 0.5f;
            dof.gaussianEnd.value = focusDistance + blurRange;
        }
    }
}
