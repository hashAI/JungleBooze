using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box coins (style guide 7.2): pooled gold discs with a darker rim and a turquoise gem, 0.5 m across,
    /// spinning about the vertical axis at one turn per second. Polls the <see cref="TrackSimulation"/> coin ring
    /// each frame, plus the vine bonus coins; collected coins are hidden. No allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class CoinView : MonoBehaviour, IRunView
    {
        private const int MaxPool = 256;
        private const float BehindM = 6f;
        private const float DiameterM = 0.5f;
        private const float FaceScale = 0.84f;
        private const float RimThicknessM = 0.06f;
        private const float FaceThicknessM = 0.07f;
        private const float GemSizeM = 0.18f;
        private const float TurnsPerSecond = 1f;
        private const float PhasePerIdDeg = 47f;

        private Transform[] _coins;
        private int _shown;
        private float _spinDeg;
        private TrackSimulation _track;

        /// <summary>Coins shown last frame (tests).</summary>
        public int ShownCoinCount => _shown;

        private float _viewDistanceM;

        public void Init(GrayBoxKit kit, int poolSize, float viewDistanceM)
        {
            _viewDistanceM = viewDistanceM;
            int size = Mathf.Clamp(poolSize, 1, MaxPool);
            _coins = new Transform[size];
            Quaternion faceUp = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < size; i++)
            {
                Transform root = new GameObject("Coin" + i).transform;
                root.SetParent(transform, false);

                // Real art (0.5 m across, face toward the camera) replaces the three primitives.
                if (EnvironmentArt.Attach(root, EnvironmentArt.Coin) != null)
                {
                    root.gameObject.SetActive(false);
                    _coins[i] = root;
                    continue;
                }

                // Cylinders are 2 m tall at scale 1, so half the thickness goes into the Y scale.
                Transform rim = kit.Create(
                    PrimitiveType.Cylinder, "Rim", root, StylePalette.CoinRim, Vector3.zero,
                    new Vector3(DiameterM, RimThicknessM * 0.5f, DiameterM));
                rim.localRotation = faceUp;
                Transform face = kit.Create(
                    PrimitiveType.Cylinder, "Face", root, StylePalette.CoinGold, Vector3.zero,
                    new Vector3(DiameterM * FaceScale, FaceThicknessM * 0.5f, DiameterM * FaceScale));
                face.localRotation = faceUp;
                kit.Create(
                    PrimitiveType.Sphere, "Gem", root, StylePalette.CoinGem, Vector3.zero,
                    new Vector3(GemSizeM, GemSizeM, GemSizeM));

                root.gameObject.SetActive(false);
                _coins[i] = root;
            }
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_coins == null)
            {
                return;
            }

            _spinDeg = (_spinDeg + 360f * TurnsPerSecond * realDeltaSeconds) % 360f;

            int used = 0;
            if (_track != null)
            {
                RunnerSimulation runner = session.Runner;
                RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + _viewDistanceM;
                int count = _track.CoinCount;
                for (int i = 0; i < count && used < _coins.Length; i++)
                {
                    ref readonly CoinInstance c = ref _track.GetCoin(i);
                    if (c.Z > maxZ)
                    {
                        break;
                    }

                    if (c.Collected || c.Z < minZ)
                    {
                        continue;
                    }

                    Place(used++, in c);
                }

                // Vine coin shower and Perfect ring (GDD 7.3 step 4).
                int bonus = _track.BonusCoinCount;
                for (int i = 0; i < bonus && used < _coins.Length; i++)
                {
                    ref readonly CoinInstance c = ref _track.GetBonusCoin(i);
                    if (c.Collected || c.Z < minZ || c.Z > maxZ)
                    {
                        continue;
                    }

                    Place(used++, in c);
                }
            }

            for (int i = used; i < _shown; i++)
            {
                _coins[i].gameObject.SetActive(false);
            }

            _shown = used;
        }

        private void Place(int slot, in CoinInstance c)
        {
            Transform t = _coins[slot];
            t.localPosition = new Vector3(c.X, c.Y, (float)c.Z);
            t.localRotation = Quaternion.Euler(0f, _spinDeg + c.Id * PhasePerIdDeg, 0f);
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
            }
        }
    }
}
