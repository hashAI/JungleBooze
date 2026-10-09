using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Maps Pista's (interpolated) state on the expedition path to the camera rig's input: beat modifier (spec 103
    /// §11), vine-air look-ahead, canopy fall hold. Shared by the scene and the framing tests so both use one mapping.
    /// </summary>
    public static class ExpeditionCameraTarget
    {
        public static CameraTargetInput From(in RunnerState s, WorldPath path)
        {
            CameraMode mode = CameraMode.Run;
            switch (s.Mode)
            {
                case MoveMode.Swim:
                    mode = CameraMode.Swim;
                    break;
                case MoveMode.DeepDive:
                    mode = CameraMode.DeepDive;
                    break;
                case MoveMode.Swing:
                    mode = CameraMode.Swing;
                    break;
                default:
                    if (s.VineAir)
                    {
                        mode = CameraMode.Swing;
                    }
                    else if (path != null && path.IsCanopy(s.S))
                    {
                        mode = CameraMode.Canopy;
                    }

                    break;
            }

            bool canopyFall = s.Dead && s.Cause == DeathCause.Fall && path != null && path.IsCanopy(s.S);
            return new CameraTargetInput
            {
                S = s.S,
                X = s.X,
                Y = s.Y,
                Vy = s.Vy,
                GroundY = s.Mode == MoveMode.Swing ? s.LastGroundY : s.GroundY,
                VLat = s.VLat,
                Speed = s.Speed,
                Sliding = s.Sliding,
                PathYawDeg = path != null ? path.GetFrame(s.S).HeadingDeg : 0f,
                Mode = mode,
                VineAir = s.VineAir,
                FallHold = canopyFall,
            };
        }
    }
}
