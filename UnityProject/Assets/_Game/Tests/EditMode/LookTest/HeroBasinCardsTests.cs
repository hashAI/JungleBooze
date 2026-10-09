using JungleBooze.App.HeroBasin;
using JungleBooze.Core.Perf;
using JungleBooze.Editor.HeroBasin;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Painterly v4 cheap tricks (ADR 0011): projection-matched card placement, the arch-window test, cascade sheet
    /// splitting and High-tier-only content.
    /// </summary>
    public sealed class HeroBasinCardsTests
    {
        private GameObject _cameraObject;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("CardsTestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.transform.SetPositionAndRotation(new Vector3(1.3f, 2f, -4.3f), Quaternion.Euler(1f, 0f, 0f));
            _camera.fieldOfView = 52f;
            _camera.aspect = 2532f / 1170f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 4200f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void AtDepthLandsOnThePlaneAtThatForwardDepth()
        {
            foreach (Vector2 uv in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.05f), new Vector2(0.93f, 0.9f) })
            {
                Vector3 p = HeroBasinCards.AtDepth(_camera, uv.x, uv.y, 215f);
                float depth = Vector3.Dot(p - _camera.transform.position, _camera.transform.forward);
                Assert.That(depth, Is.EqualTo(215f).Within(1e-2f));
                Vector3 viewport = _camera.WorldToViewportPoint(p);
                Assert.That(viewport.x, Is.EqualTo(uv.x).Within(1e-3f));
                Assert.That(1f - viewport.y, Is.EqualTo(uv.y).Within(1e-3f));
            }
        }

        [Test]
        public void InRectMatchesTheScreenRectangleAndIgnoresPointsBehind()
        {
            Matrix4x4 vp = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            var rect = new Vector4(0.5f, 0.22f, 0.73f, 0.62f);
            Assert.That(HeroBasinCards.InRect(vp, HeroBasinCards.AtDepth(_camera, 0.6f, 0.4f, 120f), rect), Is.True);
            Assert.That(HeroBasinCards.InRect(vp, HeroBasinCards.AtDepth(_camera, 0.3f, 0.4f, 120f), rect), Is.False);
            Assert.That(HeroBasinCards.InRect(vp, HeroBasinCards.AtDepth(_camera, 0.6f, 0.7f, 120f), rect), Is.False);
            Vector3 behind = _camera.transform.position - _camera.transform.forward * 50f;
            Assert.That(HeroBasinCards.InRect(vp, behind, new Vector4(-10f, -10f, 10f, 10f)), Is.False);
        }

        [Test]
        public void WideCascadesSplitIntoUnstretchedSheets()
        {
            Assert.That(HeroBasinCards.CascadePieces(3f, 2f), Is.EqualTo(1));
            Assert.That(HeroBasinCards.CascadePieces(27f, 6.5f), Is.EqualTo(1));
            Assert.That(HeroBasinCards.CascadePieces(27f, 3f), Is.EqualTo(3));
            Assert.That(HeroBasinCards.CascadePieces(500f, 1f), Is.EqualTo(6));
            for (float w = 1f; w < 60f; w += 0.7f)
            {
                int n = HeroBasinCards.CascadePieces(w, 4f);
                // Each sheet's width / height stays within ~1.6x of the painted row's 3.2 (rounding plus the clamp at 1).
                Assert.That(w / n / 4f, Is.LessThan(3.2f * 1.6f));
            }
        }

        [Test]
        public void HighTierContentHidesOnlyOnLow()
        {
            Assert.That(HeroTierContent.IsShown(DeviceTier.High, DeviceTier.High), Is.True);
            Assert.That(HeroTierContent.IsShown(DeviceTier.Low, DeviceTier.High), Is.False);
            Assert.That(HeroTierContent.IsShown(DeviceTier.Low, DeviceTier.Low), Is.True);
        }

        [Test]
        public void CardDefaultsKeepThePaintingsAspect()
        {
            var c = new HeroCards();
            const float frame = 2532f / 1170f;
            float arch = (c.ArchScreenRect.z - c.ArchScreenRect.x) * frame / (c.ArchScreenRect.w - c.ArchScreenRect.y);
            Assert.That(arch, Is.EqualTo(1536f / 987f).Within(0.02f));
            float fall = (c.FallScreenRect.z - c.FallScreenRect.x) * frame / (c.FallScreenRect.w - c.FallScreenRect.y);
            Assert.That(fall, Is.EqualTo(795f / 1453f).Within(0.02f));
        }
    }
}
