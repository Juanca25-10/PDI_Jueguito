using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Cursor en forma de ojo. La pupila mira hacia donde se mueve el ratón, parpadea de vez en cuando,
    /// se abre más sobre un botón y, si el ratón se queda quieto, el ojo se va cerrando: nadie mira.
    /// Deja una estela de motas tenues.
    /// </summary>
    public class CursorEye : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform root;     // sigue al ratón
        [SerializeField] RectTransform lids;     // se escala en Y para parpadear
        [SerializeField] RectTransform iris;
        [SerializeField] RectTransform pupil;
        [SerializeField] Image[] trail;

        [SerializeField] float lookRange = 6f;
        [SerializeField] float idleToClose = 2.5f;
        [SerializeField] Vector2 blinkInterval = new(3.5f, 7f);

        public static bool OverButton { get; set; }

        Vector2 lastMouse, lookDir, lookVel;
        float idle, open = 1f, openVel, hover, hoverVel, nextBlink, blinkT = -1f;
        bool usingMouse = true;
        Vector2[] trailPos;
        float[] trailAge;
        int trailIndex;
        float trailTimer;

        public void Configure(Canvas c, RectTransform r, RectTransform l, RectTransform i, RectTransform p, Image[] motes)
        {
            canvas = c; root = r; lids = l; iris = i; pupil = p; trail = motes;
        }

        void OnEnable() => Cursor.visible = false;
        void OnDisable() => Cursor.visible = true;

        void Start()
        {
            trailPos = new Vector2[trail.Length];
            trailAge = new float[trail.Length];
            for (int i = 0; i < trail.Length; i++) trailAge[i] = 99f;
            nextBlink = Time.unscaledTime + Random.Range(blinkInterval.x, blinkInterval.y);
        }

        void Update()
        {
            if (Mouse.current == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector2 m = Mouse.current.position.ReadValue();
            Vector2 delta = m - lastMouse;
            lastMouse = m;

            // Con teclado o mando el ojo se esconde hasta que el ratón vuelva a moverse
            if (delta.sqrMagnitude > 1f) usingMouse = true;
            else if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                     || (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f))
                usingMouse = false;
            root.gameObject.SetActive(usingMouse);
            if (!usingMouse) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, m, null, out var local);
            root.anchoredPosition = local;

            // Mirada: la pupila sigue la dirección del movimiento y vuelve al centro despacio
            Vector2 target = delta.sqrMagnitude > 0.5f ? Vector2.ClampMagnitude(delta * 0.35f, 1f) : Vector2.zero;
            lookDir = Vector2.SmoothDamp(lookDir, target, ref lookVel, delta.sqrMagnitude > 0.5f ? 0.05f : 0.6f, Mathf.Infinity, dt);
            iris.anchoredPosition = lookDir * lookRange;
            pupil.anchoredPosition = lookDir * (lookRange * 1.2f);

            // Si nadie mueve el ratón, el ojo se va cerrando
            idle = delta.sqrMagnitude > 0.5f ? 0f : idle + dt;
            float openTarget = idle > idleToClose ? 0.12f : 1f;
            open = Mathf.SmoothDamp(open, openTarget, ref openVel, idle > idleToClose ? 1.2f : 0.08f, Mathf.Infinity, dt);

            // Sobre un botón el ojo se abre más y la pupila se dilata
            hover = Mathf.SmoothDamp(hover, OverButton ? 1f : 0f, ref hoverVel, 0.12f, Mathf.Infinity, dt);

            // Parpadeo ocasional y al hacer clic
            if (Mouse.current.leftButton.wasPressedThisFrame || Time.unscaledTime >= nextBlink)
            {
                blinkT = 0f;
                nextBlink = Time.unscaledTime + Random.Range(blinkInterval.x, blinkInterval.y);
            }
            float blink = 1f;
            if (blinkT >= 0f)
            {
                blinkT += dt;
                blink = 1f - Mathf.Sin(Mathf.Clamp01(blinkT / 0.16f) * Mathf.PI) * 0.92f;
                if (blinkT > 0.16f) blinkT = -1f;
            }

            lids.localScale = new Vector3(1f + hover * 0.08f, Mathf.Max(0.06f, open * blink * (1f + hover * 0.25f)), 1f);
            pupil.localScale = Vector3.one * (1f + hover * 0.45f);

            UpdateTrail(local, delta, dt);
        }

        void UpdateTrail(Vector2 pos, Vector2 delta, float dt)
        {
            trailTimer += dt;
            if (delta.sqrMagnitude > 4f && trailTimer > 0.03f)
            {
                trailTimer = 0f;
                trailIndex = (trailIndex + 1) % trail.Length;
                trailPos[trailIndex] = pos + Random.insideUnitCircle * 4f;
                trailAge[trailIndex] = 0f;
            }
            for (int i = 0; i < trail.Length; i++)
            {
                trailAge[i] += dt;
                float k = Mathf.Clamp01(1f - trailAge[i] / 0.6f);
                trail[i].rectTransform.anchoredPosition = trailPos[i] + Vector2.up * (trailAge[i] * 14f);
                var c = trail[i].color;
                c.a = k * 0.5f;
                trail[i].color = c;
                trail[i].rectTransform.localScale = Vector3.one * (0.5f + k * 0.5f);
            }
        }
    }
}
