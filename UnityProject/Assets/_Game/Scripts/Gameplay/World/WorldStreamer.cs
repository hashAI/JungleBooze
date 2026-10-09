using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Deterministic chunk streaming (spec 102 §6.2): keeps at least <see cref="WorldDirectorConfig.StreamAheadChunks"/>
    /// planned chunks and <see cref="WorldDirectorConfig.StreamAheadDistance"/> metres of path ahead of the runner,
    /// and retires chunks that ended <see cref="WorldDirectorConfig.RetireBehindDistance"/> behind her. Driven by
    /// the runner's s once per simulation tick; allocation-free (AC-102-10).
    /// </summary>
    public sealed class WorldStreamer
    {
        private readonly WorldPath _path;
        private readonly WorldDirector _director;

        public WorldStreamer(WorldPath path, WorldDirector director)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _director = director ?? throw new ArgumentNullException(nameof(director));
        }

        public WorldPath Path => _path;

        public WorldDirector Director => _director;

        /// <summary>Clears the path, starts the director's run and streams the opening chunks.</summary>
        public void BeginRun(in DirectorRunSetup setup)
        {
            _path.Reset();
            _director.BeginRun(setup);
            Update(0f);
        }

        public void Update(float runnerS)
        {
            WorldDirectorConfig cfg = _director.Config;
            _path.Retire(runnerS - cfg.RetireBehindDistance);
            for (int guard = 0; guard < WorldPath.ChunkCapacity; guard++)
            {
                int current = _path.ChunkCount > 0 ? _path.ChunkAt(runnerS) : -1;
                int ahead = current < 0 ? 0 : _path.NextChunkSerial - 1 - current;
                if (_path.ChunkCount > 0 && _path.EndS >= runnerS + cfg.StreamAheadDistance && ahead >= cfg.StreamAheadChunks)
                {
                    return;
                }

                AppendNext();
            }
        }

        private void AppendNext()
        {
            ChunkPick pick = _director.PlanNext(_path.EndS);
            _path.Append(_director.Library.GetEntry(pick.Entry), pick);
        }
    }
}
