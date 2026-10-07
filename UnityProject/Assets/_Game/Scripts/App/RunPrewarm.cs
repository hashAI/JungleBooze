using System.Collections;
using System.Collections.Generic;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.App
{
    /// <summary>
    /// First-use hitch removal (performance audit #3 and #4). Two frames after the run scene is built, while the main
    /// menu is up: (1) every font glyph the HUD, menus, gateway title and vine stamp use is put into the dynamic
    /// font texture at the size and style it is drawn with, and (2) every hidden piece of the pooled views is drawn
    /// once into a tiny off-screen texture, so meshes, textures and shader variants are uploaded now instead of the
    /// first time a coin, obstacle, vine or power-up appears. Nothing is visible on screen and the hidden pieces are
    /// hidden again right away (editor only for now: skipped in player builds). Setup-time only (it allocates); a no-op once a run has started. [UNVERIFIED: not
    /// compiled or measured, see docs/PLAY_FIRST_BUILD.md.]
    /// </summary>
    public sealed class RunPrewarm : MonoBehaviour
    {
        private const string CommonChars =
            "0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;!?%+-x/()'&";

        private const int OffscreenSize = 64;

        private GameSession _session;
        private Camera _runCamera;
        private Transform[] _poolRoots;
        private WorldThemeView _worldView;
        private VineView _vineView;

        /// <summary>Starts the prewarm. <paramref name="poolRoots"/>: the views whose hidden pieces are drawn once.</summary>
        public void Begin(GameSession session, Camera runCamera, Transform[] poolRoots, WorldThemeView worldView, VineView vineView)
        {
            _session = session;
            _runCamera = runCamera;
            _poolRoots = poolRoots;
            _worldView = worldView;
            _vineView = vineView;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Let the canvas scalers run first, so the pixel size of every text is final.
            yield return null;
            yield return null;

            WarmFonts();

            if (_session != null && (_session.Phase == SessionPhase.Menu || _session.Phase == SessionPhase.Ready))
            {
                WarmPools();
            }
        }

        private void WarmFonts()
        {
            if (_worldView != null)
            {
                _worldView.PrewarmFont();
            }

            if (_vineView != null)
            {
                _vineView.PrewarmFont();
            }

            var warmed = new HashSet<int>();
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                Font font = text.font;
                if (font == null)
                {
                    continue;
                }

                int size = Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit);
                if (size < 1)
                {
                    continue;
                }

                // One key per (font, size, style): the common characters once, the text's own characters every time.
                int key = (font.GetInstanceID() * 31 + size) * 8 + (int)text.fontStyle;
                if (warmed.Add(key))
                {
                    font.RequestCharactersInTexture(CommonChars, size, text.fontStyle);
                }

                if (!string.IsNullOrEmpty(text.text))
                {
                    font.RequestCharactersInTexture(text.text, size, text.fontStyle);
                }
            }
        }

        private void WarmPools()
        {
            if (_runCamera == null || _poolRoots == null)
            {
                return;
            }

            // [ASSUMED] The off-screen Camera.Render is editor-only until it is verified on a device under URP: it is the one
            // piece that could render pink or warn there. Glyph prewarm (WarmFonts) still runs on device.
            if (!Application.isEditor)
            {
                return;
            }

            var woken = new List<GameObject>(256);
            for (int r = 0; r < _poolRoots.Length; r++)
            {
                Transform pool = _poolRoots[r];
                if (pool == null)
                {
                    continue;
                }

                Transform[] all = pool.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    GameObject go = all[i].gameObject;
                    if (all[i] != pool && !go.activeSelf)
                    {
                        go.SetActive(true);
                        woken.Add(go);
                    }
                }
            }

            var cameraObject = new GameObject("PrewarmCamera");
            cameraObject.transform.SetPositionAndRotation(_runCamera.transform.position, _runCamera.transform.rotation);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(_runCamera);
            camera.enabled = false;
            RenderTexture target = RenderTexture.GetTemporary(OffscreenSize, OffscreenSize, 16);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Destroy(cameraObject);

            // Back to how the views left them (hidden), before any real camera renders this frame.
            for (int i = 0; i < woken.Count; i++)
            {
                if (woken[i] != null)
                {
                    woken[i].SetActive(false);
                }
            }
        }
    }
}
