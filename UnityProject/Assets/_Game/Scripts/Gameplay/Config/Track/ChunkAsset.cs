using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// One chunk (spec 002 section 4.1), saved in <c>Assets/_Game/Config/Chunks/</c>. <see cref="ToChunkData"/>
    /// builds the immutable runtime <see cref="ChunkData"/>. Format rules are checked by the chunk validator
    /// (stage B3), not here.
    /// </summary>
    [CreateAssetMenu(fileName = "Chunk", menuName = "JungleBooze/Config/Chunk")]
    public sealed class ChunkAsset : ScriptableObject
    {
        [SerializeField] private string _id = "T1-00";
        [SerializeField] private int _formatVersion = ChunkData.CurrentFormatVersion;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _designNote = string.Empty;
        [SerializeField] private ChunkKind _kind = ChunkKind.Normal;
        [SerializeField] private float _lengthM = 40f;
        [SerializeField] private int _minTier = 1;
        [SerializeField] private int _maxTier = 6;
        [SerializeField] private WorldMask _worldMask = WorldMask.All;
        [SerializeField] private bool _allowMirror = true;
        [SerializeField] private ObstaclePlacement[] _obstacles = new ObstaclePlacement[0];
        [SerializeField] private CoinPattern[] _coins = new CoinPattern[0];

        public string Id => _id;

        public ChunkData ToChunkData()
        {
            return new ChunkData(
                _id,
                _kind,
                _lengthM,
                _minTier,
                _maxTier,
                _obstacles,
                _coins,
                _allowMirror,
                _worldMask,
                string.IsNullOrEmpty(_displayName) ? _id : _displayName,
                _designNote,
                _formatVersion);
        }

        /// <summary>Overwrites every serialized field from <paramref name="chunk"/>. For editor tooling and tests.</summary>
        internal void SetFrom(ChunkData chunk)
        {
            _id = chunk.Id;
            _formatVersion = chunk.FormatVersion;
            _displayName = chunk.DisplayName;
            _designNote = chunk.DesignNote;
            _kind = chunk.Kind;
            _lengthM = chunk.LengthM;
            _minTier = chunk.MinTier;
            _maxTier = chunk.MaxTier;
            _worldMask = chunk.WorldMask;
            _allowMirror = chunk.AllowMirror;
            _obstacles = new ObstaclePlacement[chunk.ObstacleCount];
            for (int i = 0; i < _obstacles.Length; i++)
            {
                _obstacles[i] = chunk.GetObstacle(i);
            }

            _coins = new CoinPattern[chunk.CoinPatternCount];
            for (int i = 0; i < _coins.Length; i++)
            {
                _coins[i] = chunk.GetCoinPattern(i);
            }
        }
    }
}
