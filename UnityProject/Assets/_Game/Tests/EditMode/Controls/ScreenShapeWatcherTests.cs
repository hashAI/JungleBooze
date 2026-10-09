using JungleBooze.Gameplay.Controls;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Controls
{
    /// <summary>Spec 101 §5 / review S4: any orientation or size change cancels touches, including a 180° flip.</summary>
    public sealed class ScreenShapeWatcherTests
    {
        private const int LandscapeLeft = 3;
        private const int LandscapeRight = 4;
        private const int Portrait = 1;

        [Test]
        public void FirstCallOnlyRecords()
        {
            var w = new ScreenShapeWatcher();
            Assert.IsFalse(w.Update(2532, 1170, LandscapeLeft));
            Assert.IsFalse(w.Update(2532, 1170, LandscapeLeft));
        }

        [Test]
        public void LandscapeFlipWithTheSameSizeIsAChange()
        {
            var w = new ScreenShapeWatcher();
            w.Update(2532, 1170, LandscapeLeft);
            Assert.IsTrue(w.Update(2532, 1170, LandscapeRight), "180° flip mirrors touch coordinates");
            Assert.IsFalse(w.Update(2532, 1170, LandscapeRight));
        }

        [Test]
        public void RotationAndResizeAreChanges()
        {
            var w = new ScreenShapeWatcher();
            w.Update(2532, 1170, LandscapeLeft);
            Assert.IsTrue(w.Update(1170, 2532, Portrait));
            Assert.IsTrue(w.Update(1000, 2532, Portrait), "size change (split view, editor resize)");
        }
    }
}
