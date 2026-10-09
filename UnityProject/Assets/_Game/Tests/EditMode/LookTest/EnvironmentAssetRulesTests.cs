using JungleBooze.Editor.Scenery;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// The asset-pipeline → scene builder contract for Assets/_Game/Art/Environment (ADR 0008): roles from folder
    /// and name prefix, texture maps from suffixes, backdrop order, and the placement helpers.
    /// </summary>
    public sealed class EnvironmentAssetRulesTests
    {
        private const string Root = EnvironmentAssetRules.Root;

        [TestCase(Root + "Rootstone/pillar_twisted_a.fbx", EnvironmentRole.Pillar)]
        [TestCase(Root + "Rootstone/arch_small.fbx", EnvironmentRole.Arch)]
        [TestCase(Root + "Rootstone/RS_ArchSmall.fbx", EnvironmentRole.Arch)]
        [TestCase(Root + "Rootstone/RS_HeroArch.fbx", EnvironmentRole.HeroArch)]
        [TestCase(Root + "Rootstone/RS_PillarB.fbx", EnvironmentRole.Pillar)]
        [TestCase(Root + "Rootstone/RS_Outcrop.fbx", EnvironmentRole.Outcrop)]
        [TestCase(Root + "Rootstone/Bridge_01.fbx", EnvironmentRole.Bridge)]
        [TestCase(Root + "Rootstone/outcrop_02.fbx", EnvironmentRole.Outcrop)]
        [TestCase(Root + "Rootstone/cliff_wall.fbx", EnvironmentRole.Cliff)]
        [TestCase(Root + "Rootstone/random.fbx", EnvironmentRole.Unknown)]
        [TestCase(Root + "Rootstone/RS_TravertineTiers.fbx", EnvironmentRole.Travertine)]
        [TestCase(Root + "Rootstone/RS_TravertineTiers_Water.fbx", EnvironmentRole.TravertineWater)]
        [TestCase(Root + "Plants/FP_CanopyCrown_B.fbx", EnvironmentRole.CanopyCrown)]
        [TestCase(Root + "Plants/FP_ArchVines.fbx", EnvironmentRole.ArchVines)]
        [TestCase(Root + "Plants/FP_FrameLeft_Clump.fbx", EnvironmentRole.PlantClump)]
        [TestCase(Root + "Plants/FP_Notes.fbx", EnvironmentRole.Unknown)]
        [TestCase(Root + "Stiltwoods/stiltwood_b.fbx", EnvironmentRole.Stiltwood)]
        [TestCase(Root + "Stiltwoods/leafcards_canopy.fbx", EnvironmentRole.LeafCards)]
        [TestCase(Root + "Backdrops/backdrop_02_mist_valley.png", EnvironmentRole.Backdrop)]
        [TestCase(Root + "Backdrops/mist_valley.png", EnvironmentRole.Unknown)]
        [TestCase("Assets/_Game/Art/CC0/Models/pillar_x.fbx", EnvironmentRole.Unknown)]
        public void RoleComesFromFolderAndPrefix(string path, EnvironmentRole role)
        {
            Assert.AreEqual(role, EnvironmentAssetRules.RoleOf(path));
        }

        [TestCase(Root + "Rootstone/rootstone_diff_2k.png", EnvironmentTextureKind.Albedo)]
        [TestCase(Root + "Rootstone/arch_hero_nor_gl.png", EnvironmentTextureKind.Normal)]
        [TestCase(Root + "Rootstone/arch_hero_arm.png", EnvironmentTextureKind.Arm)]
        [TestCase(Root + "Stiltwoods/leafcards_alpha.png", EnvironmentTextureKind.Alpha)]
        [TestCase(Root + "Backdrops/backdrop_01_range.png", EnvironmentTextureKind.Backdrop)]
        [TestCase(Root + "Rootstone/Textures/RS_Outcrop_BaseColor.png", EnvironmentTextureKind.Albedo)]
        [TestCase(Root + "Rootstone/Textures/RS_Outcrop_Normal.png", EnvironmentTextureKind.Normal)]
        [TestCase(Root + "Rootstone/Textures/RS_Outcrop_ARM.png", EnvironmentTextureKind.Arm)]
        [TestCase(Root + "Rootstone/notes.png", EnvironmentTextureKind.Unknown)]
        public void TextureKindComesFromTheSuffix(string path, EnvironmentTextureKind kind)
        {
            Assert.AreEqual(kind, EnvironmentAssetRules.TextureKindOf(path));
        }

        [TestCase("arch_hero_diff_2k.png", "arch_hero")]
        [TestCase("rootstone_nor_gl.png", "rootstone")]
        [TestCase("plain.png", "plain")]
        [TestCase("RS_Outcrop_BaseColor.png", "rs_outcrop")]
        public void TextureSetNameDropsTheMapSuffix(string file, string set)
        {
            Assert.AreEqual(set, EnvironmentAssetRules.TextureSetName(file));
        }

        [TestCase(Root + "Rootstone/Textures/RS_Outcrop_ARM.png", Root + "Rootstone")]
        [TestCase(Root + "Rootstone/RS_Outcrop.fbx", Root + "Rootstone")]
        public void TexturesSubfolderBelongsToTheKitFolder(string path, string folder)
        {
            Assert.AreEqual(folder, EnvironmentAssetRules.SetFolder(path));
        }

        [TestCase("backdrop_01_range", 1)]
        [TestCase("backdrop_12_far_falls.png", 12)]
        [TestCase("backdrop_range", -1)]
        [TestCase("range_01", -1)]
        public void BackdropOrderFromTheName(string name, int order)
        {
            Assert.AreEqual(order, EnvironmentAssetRules.BackdropOrder(name));
        }

        [Test]
        public void StandPutsTheBaseOnTheFootAtTheRequestedHeight()
        {
            var piece = new EnvironmentKit.Piece { Bounds = new Bounds(new Vector3(1f, 2f, -1f), new Vector3(2f, 4f, 2f)) };
            Matrix4x4 m = EnvironmentKit.Stand(piece, new Vector3(10f, 5f, 20f), 0f, 40f, 0f);
            Vector3 baseCenter = m.MultiplyPoint3x4(new Vector3(1f, 0f, -1f));
            Vector3 topCenter = m.MultiplyPoint3x4(new Vector3(1f, 4f, -1f));
            Assert.That(Vector3.Distance(baseCenter, new Vector3(10f, 5f, 20f)), Is.LessThan(1e-3f));
            Assert.AreEqual(45f, topCenter.y, 1e-3f);
        }

        [Test]
        public void SpanPutsTheArchFeetOnBothFeet()
        {
            var piece = new EnvironmentKit.Piece { Bounds = new Bounds(new Vector3(0f, 5f, 0f), new Vector3(20f, 10f, 4f)) };
            var a = new Vector3(0f, -40f, 0f);
            var b = new Vector3(60f, -40f, 80f);
            Matrix4x4 m = EnvironmentKit.Span(piece, a, b, 30f);
            Assert.That(Vector3.Distance(m.MultiplyPoint3x4(new Vector3(-10f, 0f, 0f)), a), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(m.MultiplyPoint3x4(new Vector3(10f, 0f, 0f)), b), Is.LessThan(1e-3f));
            Assert.AreEqual(30f, m.MultiplyPoint3x4(new Vector3(0f, 10f, 0f)).y, 1e-3f);
        }

        [TestCase("MI_FP_Canopy", "FP_Canopy")]
        [TestCase("M_RS_Outcrop", "RS_Outcrop")]
        [TestCase("FP_Fronds", "FP_Fronds")]
        public void MaterialSetNameDropsTheMaterialPrefix(string material, string set)
        {
            Assert.AreEqual(set, EnvironmentAssetRules.MaterialSetName(material));
        }

        [TestCase("RS_HeroArch_P.fbx", true)]
        [TestCase("FP_Fern_P_Clump.fbx", true)]
        [TestCase("RS_HeroArch_P_A_ARM.png", true)]
        [TestCase("fp_canopy_p", true)]
        [TestCase("RS_HeroArch.fbx", false)]
        [TestCase("RS_PillarA.fbx", false)]
        [TestCase("RS_PoolTerrace_A.fbx", false)]
        [TestCase("FP_Fern_Clump.fbx", false)]
        public void PaintedVariantsCarryAPToken(string name, bool painted)
        {
            Assert.AreEqual(painted, EnvironmentAssetRules.IsPaintedVariant(name));
        }

        [TestCase("FP_Fern_P_Clump", "FP_Fern_Clump")]
        [TestCase("RS_HeroArch_P", "RS_HeroArch")]
        [TestCase("rs_heroarch_p_a", "rs_heroarch_a")]
        [TestCase("FP_Canopy", "FP_Canopy")]
        public void PaintedTokenIsRemovedForTheBaseName(string name, string baseName)
        {
            Assert.AreEqual(baseName, EnvironmentAssetRules.WithoutPaintedToken(name));
        }
    }
}
