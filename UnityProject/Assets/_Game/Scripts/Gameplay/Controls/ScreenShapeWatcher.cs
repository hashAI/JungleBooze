namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Detects any change of screen size or orientation, including a 180° landscape flip (LandscapeLeft ↔
    /// LandscapeRight), which keeps the size but mirrors touch coordinates. Active touches must then be cancelled
    /// (spec 101 §5, AC-101-39). Plain C#, no allocation.
    /// </summary>
    public sealed class ScreenShapeWatcher
    {
        private bool _primed;
        private int _width;
        private int _height;
        private int _orientation;

        /// <summary>Returns true when the shape changed since the last call (the first call only records it).</summary>
        public bool Update(int width, int height, int orientation)
        {
            bool changed = _primed && (width != _width || height != _height || orientation != _orientation);
            _primed = true;
            _width = width;
            _height = height;
            _orientation = orientation;
            return changed;
        }
    }
}
