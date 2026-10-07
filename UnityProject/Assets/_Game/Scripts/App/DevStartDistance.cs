using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// Dev aid for the editor and development builds only: "start the next run N metres into the track" so bends,
    /// canopy and vines can be looked at quickly. The value is remembered in PlayerPrefs. In release builds
    /// <see cref="Meters"/> is always 0 and <see cref="Toggle"/> does nothing. The run driver does the fast-forward
    /// (see <c>RunDriver.TryDevFastForward</c>).
    /// </summary>
    public static class DevStartDistance
    {
        public const string PrefKey = "JungleBooze.DevStartM";
        public const int ShortM = 300;
        public const int LongM = 1000;

        private static int _meters = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _meters = -1;
        }

        /// <summary>Distance the next run starts at (m); 0 = the beginning of the track.</summary>
        public static int Meters
        {
            get
            {
                if (!Debug.isDebugBuild)
                {
                    return 0;
                }

                if (_meters < 0)
                {
                    _meters = Mathf.Max(0, PlayerPrefs.GetInt(PrefKey, 0));
                }

                return _meters;
            }
        }

        /// <summary>Selects <paramref name="meters"/>, or goes back to 0 if it is already selected.</summary>
        public static void Toggle(int meters)
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            _meters = Meters == meters ? 0 : meters;
            PlayerPrefs.SetInt(PrefKey, _meters);
            Debug.Log("[JungleBooze] Dev start distance for the next run: " + _meters + " m.");
        }

        /// <summary>Index of the current setting for label tables: 0 none, 1 short, 2 long, 3 anything else.</summary>
        public static int OptionIndex
        {
            get
            {
                int m = Meters;
                return m == 0 ? 0 : (m == ShortM ? 1 : (m == LongM ? 2 : 3));
            }
        }
    }
}
