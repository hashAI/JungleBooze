using System;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Layer volumes of the adaptive music (plain C#): the intensity level picks target volumes per layer from
    /// <see cref="MusicSetup"/>; layers move toward them at their fade speed. The danger section (layer 2) only
    /// starts moving on a bar line, so the switch lands on the beat. <see cref="Master"/> carries the soft stop and
    /// sting ducking.
    /// </summary>
    public sealed class MusicMix
    {
        private readonly MusicSetup _setup;
        private readonly float[] _current = new float[MusicSetup.LayerCount];
        private readonly float[] _target = new float[MusicSetup.LayerCount];
        private double _sectionGateTime = double.NegativeInfinity;
        private float _duck = 1f;
        private float _duckTarget = 1f;
        private float _stop = 1f;
        private float _stopTarget = 1f;

        public MusicMix(MusicSetup setup)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Level = 1;
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                _target[i] = _setup.Volume(Level, i);
                _current[i] = _target[i];
            }
        }

        public int Level { get; private set; }

        /// <summary>DSP time of the loops' first downbeat.</summary>
        public double LoopStart { get; set; }

        /// <summary>Overall gain: soft stop × sting duck.</summary>
        public float Master => _duck * _stop;

        public float Layer(int layer) => _current[layer];

        public float Target(int layer) => _target[layer];

        /// <summary>Next bar line at or after <paramref name="now"/> (DSP seconds).</summary>
        public double NextBar(double now)
        {
            double bar = _setup.BarSeconds;
            if (now <= LoopStart)
            {
                return LoopStart;
            }

            double bars = Math.Ceiling(((now - LoopStart) / bar) - 1e-9);
            return LoopStart + (bars * bar);
        }

        /// <summary>Sets the intensity level (clamped 0…3). Crossing to or from danger waits for the next bar line.</summary>
        public void SetLevel(int level, double now)
        {
            level = level < 0 ? 0 : level >= MusicSetup.LevelCount ? MusicSetup.LevelCount - 1 : level;
            if (level == Level)
            {
                return;
            }

            bool sectionChange = (level == MusicSetup.LevelCount - 1) != (Level == MusicSetup.LevelCount - 1);
            Level = level;
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                _target[i] = _setup.Volume(level, i);
            }

            if (sectionChange)
            {
                _sectionGateTime = NextBar(now);
            }
        }

        /// <summary>Snaps every layer to the level's volumes (run start).</summary>
        public void Snap(int level)
        {
            Level = level < 0 ? 0 : level >= MusicSetup.LevelCount ? MusicSetup.LevelCount - 1 : level;
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                _target[i] = _setup.Volume(Level, i);
                _current[i] = _target[i];
            }

            _sectionGateTime = double.NegativeInfinity;
            _stop = _stopTarget = 1f;
            _duck = _duckTarget = 1f;
        }

        public void Duck(bool on)
        {
            _duckTarget = on ? _setup.StingDuck : 1f;
        }

        public void SoftStop()
        {
            _stopTarget = 0f;
        }

        public void Resume()
        {
            _stopTarget = 1f;
        }

        public bool Stopped => _stop <= 0.0001f && _stopTarget <= 0f;

        public void Update(double now, float dt)
        {
            bool sectionOpen = now >= _sectionGateTime;
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                // Explore layers and the danger layer swap at the bar line with the short section fade; other
                // changes use the slow layer fade.
                bool sectional = _sectionGateTime > double.NegativeInfinity && (i != 3);
                if (sectional && !sectionOpen)
                {
                    continue;
                }

                float fade = sectional ? _setup.SectionFade : _setup.LayerFade;
                _current[i] = Approach(_current[i], _target[i], fade > 0f ? dt / fade : 1f);
            }

            if (sectionOpen && _sectionGateTime > double.NegativeInfinity && Settled())
            {
                _sectionGateTime = double.NegativeInfinity;
            }

            _duck = Approach(_duck, _duckTarget, _setup.StingDuckFade > 0f ? dt / _setup.StingDuckFade : 1f);
            _stop = Approach(_stop, _stopTarget, _setup.StopFade > 0f ? dt / _setup.StopFade : 1f);
        }

        private bool Settled()
        {
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                if (Math.Abs(_current[i] - _target[i]) > 1e-4f)
                {
                    return false;
                }
            }

            return true;
        }

        private static float Approach(float v, float target, float step)
        {
            return v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);
        }
    }
}
