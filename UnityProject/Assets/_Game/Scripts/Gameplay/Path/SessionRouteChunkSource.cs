using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// <see cref="IRouteChunkSource"/> over the generated track (<see cref="TrackSimulation.PeekChunk"/>). Built with
    /// a session it follows the session's current world, so one instance serves every run; built with a track it is
    /// fixed to that track (tests, tools). With no track yet it reports no chunks.
    /// </summary>
    public sealed class SessionRouteChunkSource : IRouteChunkSource
    {
        private readonly TrackSimulation _fixedTrack;
        private GameSession _session;

        /// <summary>No session yet: reports no chunks until <see cref="Bind"/> is called (the session is created after the route).</summary>
        public SessionRouteChunkSource()
        {
        }

        public SessionRouteChunkSource(GameSession session)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
        }

        public SessionRouteChunkSource(TrackSimulation track)
        {
            _fixedTrack = track ?? throw new System.ArgumentNullException(nameof(track));
        }

        /// <summary>Follows the world of <paramref name="session"/> from now on.</summary>
        public void Bind(GameSession session)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
        }

        private TrackSimulation CurrentTrack
        {
            get
            {
                if (_fixedTrack != null)
                {
                    return _fixedTrack;
                }

                if (_session == null)
                {
                    return null;
                }

                var world = _session.World as TrackRunWorld;
                return world != null ? world.Track : null;
            }
        }

        public double CommittedEndS
        {
            get
            {
                TrackSimulation track = CurrentTrack;
                return track != null ? track.GeneratedEndZ : 0.0;
            }
        }

        public bool TryFindZone(double s, double lookaheadM, out RouteZone zone)
        {
            zone = default(RouteZone);
            TrackSimulation track = CurrentTrack;
            if (track == null)
            {
                return false;
            }

            int last = track.GeneratedChunkCount;
            for (int serial = track.OldestChunkSerial; serial >= 1 && serial <= last; serial++)
            {
                ChunkPeek chunk = track.PeekChunk(serial);
                if (!chunk.IsValid || chunk.EndZ <= s)
                {
                    continue;
                }

                if (chunk.StartZ > s + lookaheadM)
                {
                    break;
                }

                if (chunk.Kind == ChunkKind.Vine || chunk.Kind == ChunkKind.Gateway)
                {
                    zone.Kind = chunk.Kind == ChunkKind.Vine ? RouteBeatKind.SwingZone : RouteBeatKind.Gateway;
                    zone.StartS = chunk.StartZ;
                    zone.EndS = chunk.EndZ;
                    zone.Serial = chunk.Serial;
                    return true;
                }
            }

            return false;
        }

        public WorldKind WorldKindAt(double s)
        {
            TrackSimulation track = CurrentTrack;
            return track != null ? track.WorldKindAt(s) : WorldKind.Jungle;
        }
    }
}
