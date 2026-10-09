using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Feedback
{
    /// <summary>
    /// Presentation hooks for audio (audio-director wires SFX here) and other feedback. Called by
    /// <see cref="RunFeedbackRouter"/> from simulation events, at most once per event, throttled where events repeat.
    /// </summary>
    public interface IRunFeedbackListener
    {
        /// <summary>Pista brushes the path edge (foliage). <paramref name="side"/> −1 left, +1 right; <paramref name="started"/> on first contact.</summary>
        void OnEdgeBrush(int side, bool started);

        void OnJump();

        void OnLand(LandingKind kind, float fallHeight);

        void OnSlide();

        void OnDodge(int direction);

        void OnHit(HitKind kind);

        void OnDied(DeathCause cause);
    }
}
