using JungleBooze.Editor.FeelTest;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;
using UnityEditor;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Loads the shipped movement assets (Assets/_Game/Config/Movement).</summary>
    internal static class ShippedAssets
    {
        public static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, "Missing " + path + " (run JungleBooze > Feel Test > Build Scene).");
            return asset;
        }

        public static MovementConfigAssets Set()
        {
            return new MovementConfigAssets
            {
                Speed = Load<RunSpeedConfigAsset>(FeelTestPaths.RunSpeed),
                Lateral = Load<LateralMovementConfigAsset>(FeelTestPaths.Lateral),
                JumpSlide = Load<JumpSlideConfigAsset>(FeelTestPaths.JumpSlide),
                Hitbox = Load<HitboxConfigAsset>(FeelTestPaths.Hitbox),
                Health = Load<HealthConfigAsset>(FeelTestPaths.Health),
                Flow = Load<RunFlowConfigAsset>(FeelTestPaths.RunFlow),
                Swim = AssetDatabase.LoadAssetAtPath<SwimConfigAsset>(JungleBooze.Editor.Expedition.ExpeditionPaths.Swim),
                Vine = AssetDatabase.LoadAssetAtPath<VineConfigAsset>(JungleBooze.Editor.Expedition.ExpeditionPaths.Vine),
                Canopy = AssetDatabase.LoadAssetAtPath<CanopyConfigAsset>(JungleBooze.Editor.Expedition.ExpeditionPaths.Canopy),
            };
        }

        public static MovementConfig Config()
        {
            return Set().Build();
        }

        public static CourseData Course()
        {
            return Load<FeelCourseAsset>(FeelTestPaths.FeelCourse).Course;
        }

        public static GestureConfig Gestures()
        {
            return Load<GestureConfigAsset>(FeelTestPaths.Gesture).Values;
        }
    }
}
