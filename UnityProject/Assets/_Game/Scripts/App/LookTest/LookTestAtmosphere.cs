using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Pushes the look-test atmosphere to the shaders as globals (ENVIRONMENT_STRATEGY 4.4): height fog with a warm
    /// in-scatter toward the sun and a cool haze away from it (JBAtmosphere.hlsl, used by every project shader and
    /// the sky), and the canopy light: under the canopy roof the sun reaches the ground only through a dappled
    /// pattern and the ambient light drops and turns green. How much canopy covers a vertex is baked by the scene
    /// builder into vertex color B. Runs in the editor too, so scene view, screenshots and Play agree.
    /// Values come from <see cref="LookTestConfigAsset"/>; nothing is computed per frame except the sun direction.
    /// </summary>
    [ExecuteAlways]
    public sealed class LookTestAtmosphere : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_JBFogColor");
        private static readonly int FogSunColorId = Shader.PropertyToID("_JBFogSunColor");
        private static readonly int FogParamsId = Shader.PropertyToID("_JBFogParams");
        private static readonly int FogParams2Id = Shader.PropertyToID("_JBFogParams2");
        private static readonly int CanopyParamsId = Shader.PropertyToID("_JBCanopyParams");
        private static readonly int CanopyTintId = Shader.PropertyToID("_JBCanopyTint");
        private static readonly int DappleTexId = Shader.PropertyToID("_JBDappleTex");
        private static readonly int ShaftColorId = Shader.PropertyToID("_JBShaftColor");
        private static readonly int MistColorId = Shader.PropertyToID("_JBMistColor");
        private static readonly int SunDirectionId = Shader.PropertyToID("_JBSunDirection");

        [SerializeField] private LookTestConfigAsset _config;
        [SerializeField] private Texture2D _dapple;
        [SerializeField] private Light _sun;

        public void Configure(LookTestConfigAsset config, Texture2D dapple, Light sun)
        {
            _config = config;
            _dapple = dapple;
            _sun = sun;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void LateUpdate()
        {
            UpdateSun();
        }

        /// <summary>Writes every global. Call after changing the config.</summary>
        public void Apply()
        {
            if (_config == null)
            {
                return;
            }

            LookTestConfigAsset c = _config;
            Shader.SetGlobalColor(FogColorId, c.FogColor.linear);
            Shader.SetGlobalColor(FogSunColorId, c.FogSunColor.linear);
            Shader.SetGlobalVector(FogParamsId, new Vector4(c.FogDensity, c.FogHeightFalloff, c.FogBaseHeightM, c.FogStartM));
            Shader.SetGlobalVector(FogParams2Id, new Vector4(c.FogSunPower, c.FogMaxOpacity, c.SkyFogDistanceM, 0f));
            Shader.SetGlobalVector(CanopyParamsId, new Vector4(1f / Mathf.Max(0.5f, c.DappleSizeM), c.DappleLitFraction, c.CanopyAmbient, 0.015f));
            Shader.SetGlobalColor(CanopyTintId, c.CanopyTint.linear);
            Color shaft = c.ShaftColor.linear * c.ShaftIntensity;
            Shader.SetGlobalColor(ShaftColorId, shaft);
            Color mist = c.MistColor.linear;
            mist.a = c.MistOpacity;
            Shader.SetGlobalColor(MistColorId, mist);
            if (_dapple != null)
            {
                Shader.SetGlobalTexture(DappleTexId, _dapple);
            }

            UpdateSun();
        }

        private void UpdateSun()
        {
            Vector3 toSun = _sun != null ? -_sun.transform.forward : (_config != null ? -_config.SunLightDirection : Vector3.up);
            Shader.SetGlobalVector(SunDirectionId, new Vector4(toSun.x, toSun.y, toSun.z, 0f));
        }
    }
}
