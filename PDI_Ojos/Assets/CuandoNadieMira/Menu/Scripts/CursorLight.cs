using UnityEngine;
using UnityEngine.InputSystem;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Luz suave que nace en la cámara y apunta a donde está el cursor, como una vela que tiembla.
    /// Solo es ambientación: no decide qué piezas desaparecen.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class CursorLight : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float maxDistance = 40f;
        [SerializeField] float aimDamping = 0.12f;
        [SerializeField] float baseIntensity = 6f;
        [SerializeField] float flicker = 0.18f;

        Light spot;
        Vector3 aimPoint, aimVel;
        Vector2 virtualCursor;

        /// <summary>Punto de la escena que ilumina ahora mismo.</summary>
        public Vector3 AimPoint => aimPoint;
        public Vector3 Origin => transform.position;

        void Awake()
        {
            spot = GetComponent<Light>();
            if (cam == null) cam = Camera.main;
            virtualCursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            aimPoint = cam.transform.position + cam.transform.forward * 10f;
        }

        void LateUpdate()
        {
            Vector2 screen = virtualCursor;
            if (Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.01f)
            {
                // Con mando, el stick derecho mueve un cursor virtual
                virtualCursor += Gamepad.current.rightStick.ReadValue() * (Screen.height * 0.9f * Time.deltaTime);
                virtualCursor.x = Mathf.Clamp(virtualCursor.x, 0, Screen.width);
                virtualCursor.y = Mathf.Clamp(virtualCursor.y, 0, Screen.height);
                screen = virtualCursor;
            }
            else if (Mouse.current != null)
            {
                screen = Mouse.current.position.ReadValue();
                virtualCursor = screen;
            }

            Ray ray = cam.ScreenPointToRay(screen);
            Vector3 target = Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(12f);
            aimPoint = Vector3.SmoothDamp(aimPoint, target, ref aimVel, aimDamping);
            transform.rotation = Quaternion.LookRotation(aimPoint - transform.position);

            float n = Mathf.PerlinNoise(Time.time * 3.1f, 0.37f) * 0.7f + Mathf.PerlinNoise(Time.time * 11f, 4.2f) * 0.3f;
            spot.intensity = baseIntensity * (1f - flicker + n * flicker * 2f);
        }

        /// <summary>True si el punto está dentro del cono de luz (lo usan los sonidos ambientales).</summary>
        public bool IsInCone(Vector3 worldPoint, float extraDegrees = 0f)
        {
            Vector3 dir = worldPoint - transform.position;
            return Vector3.Angle(transform.forward, dir) <= spot.spotAngle * 0.5f + extraDegrees;
        }
    }
}
