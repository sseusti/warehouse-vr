using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Warehouse
{
    /// <summary>
    /// Sound, particles and controller vibration for remote actions.
    /// Sounds are synthesized at startup, so the project needs no audio files.
    /// </summary>
    public sealed class RemoteFeedback : MonoBehaviour
    {
        [SerializeField] private RemoteFeedbackSettings settings;
        [SerializeField] private HapticImpulsePlayer haptics;
        [SerializeField] private Material particleMaterial;

        const int SampleRate = 44100;
        const int SourceCount = 4;
        const int SparkCapacity = 96;

        AudioClip zap;
        AudioClip click;
        AudioClip buzz;
        AudioClip thud;
        AudioClip rewind;
        AudioSource[] sources;
        int nextSource;
        SparkBurst sparks;

        public void Configure(RemoteFeedbackSettings values, HapticImpulsePlayer player, Material material)
        {
            settings = values;
            haptics = player;
            particleMaterial = material;
        }

        void Awake()
        {
            zap = Synthesize("Remote Zap", 0.28f, Zap);
            click = Synthesize("Remote Click", 0.06f, Click);
            buzz = Synthesize("Remote Deny", 0.26f, Buzz);
            thud = Synthesize("Remote Success", 0.45f, Thud);
            rewind = Synthesize("Remote Undo", 0.3f, Rewind);

            sources = new AudioSource[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                var source = new GameObject("Remote Sound " + i).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f;
                source.maxDistance = 15f;
                sources[i] = source;
            }

            sparks = new GameObject("Remote Sparks").AddComponent<SparkBurst>();
            sparks.transform.SetParent(transform, false);
            sparks.Create(SparkCapacity, particleMaterial);
        }

        void OnDestroy()
        {
            Destroy(zap);
            Destroy(click);
            Destroy(buzz);
            Destroy(thud);
            Destroy(rewind);
        }

        /// <summary>Pitch for a size change: smaller objects sound higher.</summary>
        public float PitchForScale(float scale) => ClampPitch(1f / Mathf.Sqrt(Mathf.Max(scale, 0.01f)));

        /// <summary>Pitch for a gravity preset: weaker or upward gravity sounds higher.</summary>
        public float PitchForGravity(GravityPresets.Preset preset) =>
            ClampPitch(1f + (9.81f - preset.acceleration) / 25f + (preset.upward ? 0.6f : 0f));

        public void Hover() => Vibrate(settings.Hover);

        public void ModeSwitched(Vector3 position)
        {
            Play(click, position, 1f);
            Vibrate(settings.ModeSwitch);
        }

        public void Pressed(Vector3 position)
        {
            Play(click, position, 1.3f);
            Vibrate(settings.ModeSwitch);
        }

        public void Applied(Vector3 point, float pitch)
        {
            Play(zap, point, pitch);
            Burst(point, settings.ParticleColor, settings.BurstCount);
            Vibrate(settings.Apply);
        }

        public void Denied(Vector3 point)
        {
            Play(buzz, point, 1f);
            Burst(point, settings.DenyColor, settings.BurstCount / 3);
            Vibrate(settings.Deny);
        }

        public void Succeeded(Vector3 point)
        {
            Play(thud, point, 1f);
            Burst(point, settings.ParticleColor, settings.BurstCount * 2);
            Vibrate(settings.Success);
        }

        /// <summary>The last remote action was taken back.</summary>
        public void Undone(Vector3 position)
        {
            Play(rewind, position, 1f);
            Vibrate(settings.Undo);
        }

        float ClampPitch(float pitch) => Mathf.Clamp(pitch, settings.PitchRange.x, settings.PitchRange.y);

        void Play(AudioClip clip, Vector3 position, float pitch)
        {
            AudioSource source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            source.transform.position = position;
            source.pitch = pitch;
            source.volume = settings.Volume;
            source.clip = clip;
            source.Play();
        }

        void Vibrate(RemoteFeedbackSettings.Pulse pulse)
        {
            if (haptics != null && pulse.amplitude > 0f && pulse.duration > 0f)
                haptics.SendHapticImpulse(pulse.amplitude, pulse.duration);
        }

        void Burst(Vector3 point, Color color, int count) => sparks.Emit(point, color, Mathf.Max(count, 1));

        delegate float Wave(float time, float length);

        static AudioClip Synthesize(string name, float length, Wave wave)
        {
            int count = Mathf.CeilToInt(length * SampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = wave(i / (float)SampleRate, length);

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Descending sweep with a fast attack: the remote "fires".
        static float Zap(float t, float length)
        {
            float progress = t / length;
            // Phase of a linear sweep from 1100 Hz to 280 Hz.
            float phase = 2f * Mathf.PI * (1100f * t - (1100f - 280f) * t * t / (2f * length));
            float envelope = Mathf.Min(1f, t * 400f) * Mathf.Exp(-4f * progress);
            return 0.5f * envelope * (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase));
        }

        // Rising sweep with a fast flutter, like a tape rewinding: the zap played backwards.
        static float Rewind(float t, float length)
        {
            float progress = t / length;
            // Phase of a linear sweep from 300 Hz to 1000 Hz.
            float phase = 2f * Mathf.PI * (300f * t + (1000f - 300f) * t * t / (2f * length));
            float flutter = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 32f * t);
            float envelope = Mathf.Min(1f, t * 60f) * Mathf.Min(1f, (length - t) * 40f) * (0.5f + 0.5f * progress);
            return 0.4f * envelope * flutter * Mathf.Sin(phase);
        }

        static float Click(float t, float length)
        {
            return 0.45f * Mathf.Exp(-t * 90f) * Mathf.Sin(2f * Mathf.PI * 1800f * t);
        }

        // Two low square-ish pulses: "not allowed".
        static float Buzz(float t, float length)
        {
            bool on = t < 0.1f || (t > 0.14f && t < 0.24f);
            if (!on)
                return 0f;

            float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 140f * t));
            return 0.22f * tone * (1f - Mathf.Exp(-((t % 0.14f) * 300f)));
        }

        // Low thump with a bright chime on top: "done".
        static float Thud(float t, float length)
        {
            float low = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 70f, t / length) * t) * Mathf.Exp(-t * 9f);
            float chime = (Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1320f * t)) *
                Mathf.Exp(-t * 7f) * Mathf.Min(1f, t * 200f);
            return 0.45f * low + 0.18f * chime;
        }
    }
}
