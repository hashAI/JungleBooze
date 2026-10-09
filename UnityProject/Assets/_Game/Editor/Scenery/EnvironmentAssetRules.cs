using System;
using System.IO;

namespace JungleBooze.Editor.Scenery
{
    /// <summary>What an environment piece is for (from its folder and file-name prefix; ADR 0008).</summary>
    public enum EnvironmentRole
    {
        Unknown,
        Pillar,
        HeroArch,
        Arch,
        Bridge,
        Outcrop,
        Cliff,
        Stiltwood,
        LeafCards,
        Backdrop,
        PoolTerrace,
        Ledge,
        PlantClump,
        Travertine,
        TravertineWater,
        CanopyCrown,
        ArchVines,
    }

    /// <summary>Which map of a texture set a file is (Poly Haven style suffixes).</summary>
    public enum EnvironmentTextureKind
    {
        Unknown,
        Albedo,
        Normal,
        Arm,
        Alpha,
        Backdrop,
    }

    /// <summary>
    /// The contract between asset-pipeline and the scene builders for <see cref="Root"/> (ADR 0008). Pure string
    /// rules, no editor API, unit-tested.
    ///
    ///   Rootstone/   [RS_]Pillar*, [RS_]HeroArch*, [RS_]Arch*, Bridge*, Outcrop*, Cliff* (.fbx; +Y up, 1 unit = 1 m;
    ///                arches span along local X with a foot at each end of the X bounds). Meshes named *_LOD1.. are
    ///                skipped (the builder merges LOD0). An empty named *_WaterfallMouth marks where a fall starts.
    ///   Stiltwoods/  [SW_]Stiltwood* (trunk + stilt-root fan, base at the origin), [SW_]LeafCards* (alpha cards)
    ///   Rootstone/   also [RS_]PoolTerrace* (terraced pool rims) and [RS_]Ledge* (lookout ledges).
    ///   Rootstone/   also [RS_]TravertineTiers* (terraced travertine pools with Pool/Lip/Cascade anchors) and its
    ///                *_Water mesh (the pool surfaces, drawn with the water material).
    ///   Plants/      [FP_]*_Clump.fbx: alpha-cut card clumps; atlas set = the name without "_Clump" (FP_Broadleaf).
    ///                [FP_]CanopyCrown* (single crowns) and [FP_]ArchVines* (curtains in the hero arch's space).
    ///                A model without a set of its own uses the set named like its FBX material; a mesh with several
    ///                material slots gets one set per slot (FBX material order = slot order).
    ///   Backdrops/   backdrop_NN_name.png (RGBA matte-painting layers; NN = order, 01 = farthest)
    ///   Textures:    &lt;model or kit name&gt;_BaseColor|_diff, _Normal|_nor_gl (OpenGL), _ARM, _alpha, next to the
    ///                model or in a Textures/ subfolder. A model uses the set that shares its name, else the kit set
    ///                (rootstone_*, stiltwood_*), else the look test's CC0 stand-in material (rock_face_03
    ///                rootstone, bark_brown_02 bark), so it merges with the procedural pieces of the segment.
    /// </summary>
    public static class EnvironmentAssetRules
    {
        public const string Root = "Assets/_Game/Art/Environment/";
        public const string RootstoneFolder = Root + "Rootstone";
        public const string StiltwoodsFolder = Root + "Stiltwoods";
        public const string BackdropsFolder = Root + "Backdrops";
        public const string PlantsFolder = Root + "Plants";

        public static bool IsEnvironmentAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && assetPath.Replace('\\', '/').StartsWith(Root, StringComparison.Ordinal);
        }

        /// <summary>Role of a model or backdrop image from its folder and name prefix.</summary>
        public static EnvironmentRole RoleOf(string assetPath)
        {
            if (!IsEnvironmentAsset(assetPath))
            {
                return EnvironmentRole.Unknown;
            }

            string path = assetPath.Replace('\\', '/');
            string name = StripKitPrefix(Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
            if (path.StartsWith(BackdropsFolder + "/", StringComparison.Ordinal))
            {
                return BackdropOrder(name) >= 0 ? EnvironmentRole.Backdrop : EnvironmentRole.Unknown;
            }

            if (path.StartsWith(StiltwoodsFolder + "/", StringComparison.Ordinal))
            {
                if (name.StartsWith("leafcard", StringComparison.Ordinal) || name.StartsWith("leaf_card", StringComparison.Ordinal))
                {
                    return EnvironmentRole.LeafCards;
                }

                return name.StartsWith("stiltwood", StringComparison.Ordinal) ? EnvironmentRole.Stiltwood : EnvironmentRole.Unknown;
            }

            if (path.StartsWith(PlantsFolder + "/", StringComparison.Ordinal))
            {
                if (name.StartsWith("canopycrown", StringComparison.Ordinal))
                {
                    return EnvironmentRole.CanopyCrown;
                }

                if (name.StartsWith("archvines", StringComparison.Ordinal))
                {
                    return EnvironmentRole.ArchVines;
                }

                return name.EndsWith("_clump", StringComparison.Ordinal) ? EnvironmentRole.PlantClump : EnvironmentRole.Unknown;
            }

            if (path.StartsWith(RootstoneFolder + "/", StringComparison.Ordinal))
            {
                if (name.StartsWith("poolterrace", StringComparison.Ordinal) || name.StartsWith("pool_terrace", StringComparison.Ordinal))
                {
                    return EnvironmentRole.PoolTerrace;
                }

                if (name.StartsWith("travertine", StringComparison.Ordinal))
                {
                    return name.EndsWith("_water", StringComparison.Ordinal) ? EnvironmentRole.TravertineWater : EnvironmentRole.Travertine;
                }

                if (name.StartsWith("ledge", StringComparison.Ordinal))
                {
                    return EnvironmentRole.Ledge;
                }

                if (name.StartsWith("pillar", StringComparison.Ordinal))
                {
                    return EnvironmentRole.Pillar;
                }

                if (name.StartsWith("heroarch", StringComparison.Ordinal) || name.StartsWith("arch_hero", StringComparison.Ordinal) || name.StartsWith("hero_arch", StringComparison.Ordinal))
                {
                    return EnvironmentRole.HeroArch;
                }

                if (name.StartsWith("arch", StringComparison.Ordinal))
                {
                    return EnvironmentRole.Arch;
                }

                if (name.StartsWith("bridge", StringComparison.Ordinal))
                {
                    return EnvironmentRole.Bridge;
                }

                if (name.StartsWith("outcrop", StringComparison.Ordinal))
                {
                    return EnvironmentRole.Outcrop;
                }

                if (name.StartsWith("cliff", StringComparison.Ordinal))
                {
                    return EnvironmentRole.Cliff;
                }
            }

            return EnvironmentRole.Unknown;
        }

        /// <summary>Texture set name of an FBX material name: drops a material prefix ("MI_FP_Canopy" → "FP_Canopy").</summary>
        public static string MaterialSetName(string materialName)
        {
            string[] prefixes = { "MI_", "M_", "MAT_", "Mat_" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (materialName.StartsWith(prefixes[i], StringComparison.Ordinal) && materialName.Length > prefixes[i].Length)
                {
                    return materialName.Substring(prefixes[i].Length);
                }
            }

            return materialName;
        }

        /// <summary>Drops a kit prefix ("rs_", "sw_", "bd_", "env_", "fp_") from a lower-case name.</summary>
        public static string StripKitPrefix(string name)
        {
            string[] prefixes = { "rs_", "sw_", "bd_", "env_", "fp_" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (name.StartsWith(prefixes[i], StringComparison.Ordinal))
                {
                    return name.Substring(prefixes[i].Length);
                }
            }

            return name;
        }

        /// <summary>Folder whose texture sets a model or texture belongs to (a trailing Textures/ folder is the kit's).</summary>
        public static string SetFolder(string assetPath)
        {
            string folder = (Path.GetDirectoryName(assetPath) ?? string.Empty).Replace('\\', '/');
            const string textures = "/Textures";
            return folder.EndsWith(textures, StringComparison.OrdinalIgnoreCase) ? folder.Substring(0, folder.Length - textures.Length) : folder;
        }

        /// <summary>Texture map kind from the file name (and the Backdrops folder).</summary>
        public static EnvironmentTextureKind TextureKindOf(string assetPath)
        {
            if (!IsEnvironmentAsset(assetPath))
            {
                return EnvironmentTextureKind.Unknown;
            }

            string path = assetPath.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (path.StartsWith(BackdropsFolder + "/", StringComparison.Ordinal))
            {
                return EnvironmentTextureKind.Backdrop;
            }

            if (name.Contains("_nor_gl") || name.EndsWith("_normal", StringComparison.Ordinal))
            {
                return EnvironmentTextureKind.Normal;
            }

            if (name.EndsWith("_arm", StringComparison.Ordinal) || name.Contains("_arm_"))
            {
                return EnvironmentTextureKind.Arm;
            }

            if (name.Contains("_alpha"))
            {
                return EnvironmentTextureKind.Alpha;
            }

            if (name.Contains("_diff") || name.Contains("_albedo") || name.Contains("_basecolor"))
            {
                return EnvironmentTextureKind.Albedo;
            }

            return EnvironmentTextureKind.Unknown;
        }

        /// <summary>Base name of a texture set ("arch_hero_diff_2k" → "arch_hero"), or the name itself.</summary>
        public static string TextureSetName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            string[] markers = { "_diff", "_albedo", "_basecolor", "_nor_gl", "_normal", "_arm", "_alpha" };
            int cut = -1;
            for (int i = 0; i < markers.Length; i++)
            {
                int index = name.IndexOf(markers[i], StringComparison.Ordinal);
                if (index > 0 && (cut < 0 || index < cut))
                {
                    cut = index;
                }
            }

            return cut > 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>Kit texture set name for a role ("rootstone" or "stiltwood"), or null.</summary>
        public static string KitSetName(EnvironmentRole role)
        {
            switch (role)
            {
                case EnvironmentRole.Pillar:
                case EnvironmentRole.PoolTerrace:
                case EnvironmentRole.Ledge:
                case EnvironmentRole.HeroArch:
                case EnvironmentRole.Arch:
                case EnvironmentRole.Bridge:
                case EnvironmentRole.Outcrop:
                case EnvironmentRole.Cliff:
                case EnvironmentRole.Travertine:
                    return "rootstone";
                case EnvironmentRole.Stiltwood:
                    return "stiltwood";
                case EnvironmentRole.LeafCards:
                    return "leafcards";
                default:
                    return null;
            }
        }

        /// <summary>Order of a backdrop layer ("backdrop_02_mist_valley" → 2), or −1 if the name has no order.</summary>
        public static int BackdropOrder(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            const string prefix = "backdrop_";
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return -1;
            }

            int end = prefix.Length;
            while (end < name.Length && char.IsDigit(name[end]))
            {
                end++;
            }

            if (end == prefix.Length)
            {
                return -1;
            }

            return int.Parse(name.Substring(prefix.Length, end - prefix.Length), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
