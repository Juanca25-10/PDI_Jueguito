using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Sonido ambiental 3D casi inaudible (goteo, crujido, respiración). Sube de volumen y se abre
    /// el filtro paso bajo cuando la luz del cursor apunta hacia la pieza.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientSound : MonoBehaviour
    {
        [SerializeField] MenuSound sound = MenuSound.Drip;
        [SerializeField] Vector2 interval = new(2.5f, 7f);
        [SerializeField] float quietVolume = 0.02f;
        [SerializeField] float litVolume = 0.08f;
        [SerializeField] CursorLight cursorLight;

        AudioSource source;
        AudioLowPassFilter lowPass;
        float next, volume, volVel;

        public void Configure(MenuSound s, CursorLight light)
        {
            sound = s;
            cursorLight = light;
        }

        void Start()
        {
            source = GetComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            source.playOnAwake = false;
            source.clip = MenuAudio.Instance != null ? MenuAudio.Instance.Get(sound) : null;
            lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            next = Time.time + Random.Range(0f, interval.y);
            volume = quietVolume;
        }

        void Update()
        {
            if (source.clip == null) return;
            bool lit = cursorLight != null && cursorLight.IsInCone(transform.position, 8f);
            volume = Mathf.SmoothDamp(volume, lit ? litVolume : quietVolume, ref volVel, 0.4f);
            source.volume = volume * SettingsStore.SfxVolume;
            lowPass.cutoffFrequency = Mathf.Lerp(700f, 12000f, Mathf.InverseLerp(quietVolume, litVolume, volume));

            if (Time.time >= next && !source.isPlaying)
            {
                source.pitch = Random.Range(0.92f, 1.08f);
                source.Play();
                next = Time.time + source.clip.length + Random.Range(interval.x, interval.y);
            }
        }
    }
}
