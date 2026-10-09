using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.UI
{
    /// <summary>
    /// Import settings for <c>Assets/_Game/Art/UI</c>: sprites (9-slice borders from Sprites/borders.json, written by
    /// tools/art/gen_ui_sprites.py), icons (256 px sprites) and home backdrops (2048 max, no mips). High-quality
    /// compression: UI gradients band badly at ASTC 6x6.
    /// </summary>
    public sealed class UiArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/_Game/Art/UI/";
        private const string BordersFile = "Assets/_Game/Art/UI/Sprites/borders.json";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (path.StartsWith(Root + "Backdrops/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = true;
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = path.StartsWith(Root + "Icons/") ? 256 : 512;
            float border = Border(Path.GetFileNameWithoutExtension(path));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = new Vector4(border, border, border, border);
            importer.SetTextureSettings(settings);
        }

        private static float Border(string name)
        {
            string file = Path.Combine(Directory.GetCurrentDirectory(), BordersFile);
            if (!File.Exists(file))
            {
                return 0f;
            }

            // {"name": 36, ...} (flat JSON written by the generator).
            string json = File.ReadAllText(file);
            string key = "\"" + name + "\":";
            int i = json.IndexOf(key, System.StringComparison.Ordinal);
            if (i < 0)
            {
                return 0f;
            }

            i += key.Length;
            int end = i;
            while (end < json.Length && (char.IsWhiteSpace(json[end]) || char.IsDigit(json[end]) || json[end] == '.'))
            {
                end++;
            }

            return float.TryParse(json.Substring(i, end - i).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        /// <summary>Paths this importer handles (tests).</summary>
        public static IEnumerable<string> Folders()
        {
            yield return Root + "Sprites";
            yield return Root + "Icons";
            yield return Root + "Backdrops";
            yield return Root + "Fonts";
        }
    }
}
