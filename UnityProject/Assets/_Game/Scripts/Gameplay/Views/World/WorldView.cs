using System;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Views.World
{
    /// <summary>
    /// Gray-box view of the streamed <see cref="WorldPath"/>: one pooled renderer per placed chunk showing the
    /// variant's pre-baked mesh (<see cref="ChunkMeshBuilder"/>, built once at setup), pooled coins, crystals,
    /// Shield pickups and discovery posts bound to the ids of placed chunks, chunk signs (Part B placeholders say so),
    /// and a forest floor that follows the runner. Polls the path's revision once per frame; binding and unbinding
    /// reuse pooled objects, so a run allocates nothing after warm-up.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public const int CoinPool = 1100;
        public const int CrystalPool = 48;
        public const int PowerUpPool = 12;
        public const int DiscoveryPool = 16;
        private const float CoinRadius = 0.28f;
        private const float SpinDegPerSecond = 180f;

        private readonly int[] _boundSerial = new int[WorldPath.ChunkCapacity];
        private readonly GameObject[] _coinBySlot = new GameObject[WorldPath.CoinCapacity];
        private readonly GameObject[] _crystalBySlot = new GameObject[WorldPath.CrystalCapacity];
        private readonly GameObject[] _powerUpBySlot = new GameObject[WorldPath.PowerUpCapacity];
        private readonly GameObject[] _discoveryBySlot = new GameObject[WorldPath.DiscoveryCapacity];

        private WorldPath _path;
        private ChunkLibrary _library;
        private Mesh[] _meshes;
        private string[] _signTexts;
        private MeshFilter[] _chunkFilters;
        private TextMesh[] _signs;
        private Pool _coins;
        private Pool _crystals;
        private Pool _powerUps;
        private Pool _discoveries;
        private Transform _ground;
        private int _revision = -1;
        private float _spin;
        private int _activeCoins;

        public int ActiveCoinViews => _activeCoins;

        public int BoundChunks
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _boundSerial.Length; i++)
                {
                    n += _boundSerial[i] >= 0 ? 1 : 0;
                }

                return n;
            }
        }

        public void Build(ChunkLibrary library, WorldPath path, WorldPalette palette, Font font)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _meshes = new Mesh[library.EntryCount];
            _signTexts = new string[library.EntryCount];
            for (int i = 0; i < library.EntryCount; i++)
            {
                _meshes[i] = ChunkMeshBuilder.Build(library.GetEntry(i));
                _signTexts[i] = SignText(library.GetEntry(i));
            }

            Material[] materials = palette.ChunkMaterials();
            var chunksRoot = new GameObject("Chunks").transform;
            chunksRoot.SetParent(transform, false);
            _chunkFilters = new MeshFilter[WorldPath.ChunkCapacity];
            _signs = new TextMesh[WorldPath.ChunkCapacity];
            for (int i = 0; i < WorldPath.ChunkCapacity; i++)
            {
                var go = new GameObject("Chunk" + i);
                go.transform.SetParent(chunksRoot, false);
                _chunkFilters[i] = go.AddComponent<MeshFilter>();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                if (font != null)
                {
                    var label = new GameObject("Sign");
                    label.transform.SetParent(go.transform, false);
                    TextMesh text = label.AddComponent<TextMesh>();
                    text.font = font;
                    text.fontSize = 48;
                    text.characterSize = 0.06f;
                    text.anchor = TextAnchor.LowerLeft;
                    text.color = new Color(0.12f, 0.1f, 0.14f);
                    label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    _signs[i] = text;
                }

                go.SetActive(false);
                _boundSerial[i] = -1;
            }

            _coins = new Pool(transform, "Coins", CoinPool, () =>
            {
                Transform t = ViewUtil.Primitive(PrimitiveType.Cylinder, "Coin", null, palette.Coin, false);
                t.localScale = new Vector3(CoinRadius * 2f, 0.03f, CoinRadius * 2f);
                return t.gameObject;
            });
            _crystals = new Pool(transform, "Crystals", CrystalPool, () =>
            {
                Transform t = ViewUtil.Primitive(PrimitiveType.Cube, "Crystal", null, palette.Crystal, false);
                t.localScale = new Vector3(0.32f, 0.32f, 0.32f);
                return t.gameObject;
            });
            _powerUps = new Pool(transform, "PowerUps", PowerUpPool, () =>
            {
                Transform t = ViewUtil.Primitive(PrimitiveType.Sphere, "Shield", null, palette.Shield, false);
                t.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                return t.gameObject;
            });
            _discoveries = new Pool(transform, "DiscoveryPosts", DiscoveryPool, () =>
            {
                Transform t = ViewUtil.Primitive(PrimitiveType.Cylinder, "Discovery", null, palette.Discovery, false);
                t.localScale = new Vector3(0.18f, 1.6f, 0.18f);
                return t.gameObject;
            });

            _ground = ViewUtil.Box("ForestFloor", transform, palette.Ground, new Vector3(-90f, -4.2f, -150f), new Vector3(90f, -4f, 450f), false);
            _ground.GetComponent<Renderer>().receiveShadows = true;
        }

        /// <summary>Unbinds everything (new run).</summary>
        public void ResetRun()
        {
            for (int i = 0; i < WorldPath.ChunkCapacity; i++)
            {
                if (_boundSerial[i] >= 0)
                {
                    Unbind(i);
                }
            }

            _revision = -1;
        }

        public void OnRunEvent(in RunEvent e)
        {
            switch (e.Type)
            {
                case RunEventType.Coin:
                    Release(_coinBySlot, e.Id % _coinBySlot.Length, _coins, true);
                    break;
                case RunEventType.Crystal:
                    Release(_crystalBySlot, e.Id % _crystalBySlot.Length, _crystals, false);
                    break;
                case RunEventType.PowerUp:
                    Release(_powerUpBySlot, e.Id % _powerUpBySlot.Length, _powerUps, false);
                    break;
            }
        }

        /// <summary>Per frame: bind newly placed chunks, unbind retired ones, follow the runner, spin pickups.</summary>
        public void Sync(RunnerSimulation sim, RunTracker tracker, float runnerS, float frameSeconds)
        {
            if (_path.Revision != _revision)
            {
                _revision = _path.Revision;
                for (int i = 0; i < WorldPath.ChunkCapacity; i++)
                {
                    int serial = _boundSerial[i];
                    if (serial >= 0 && !_path.IsLive(serial))
                    {
                        Unbind(i);
                    }
                }

                for (int serial = _path.FirstChunkSerial; serial < _path.NextChunkSerial; serial++)
                {
                    int slot = serial % WorldPath.ChunkCapacity;
                    if (_boundSerial[slot] != serial)
                    {
                        if (_boundSerial[slot] >= 0)
                        {
                            Unbind(slot);
                        }

                        Bind(slot, serial, sim, tracker);
                    }
                }
            }

            _ground.localPosition = new Vector3(0f, -4.1f, runnerS + 150f);
            if (frameSeconds > 0f)
            {
                _spin = Mathf.Repeat(_spin + (frameSeconds * SpinDegPerSecond), 360f);
                Quaternion coin = Quaternion.Euler(90f, _spin, 0f);
                Quaternion crystal = Quaternion.Euler(45f, _spin, 45f);
                _coins.SetRotation(coin);
                _crystals.SetRotation(crystal);
            }
        }

        private void Bind(int slot, int serial, RunnerSimulation sim, RunTracker tracker)
        {
            ref readonly PlacedChunk p = ref _path.Chunk(serial);
            _boundSerial[slot] = serial;
            MeshFilter filter = _chunkFilters[slot];
            filter.sharedMesh = _meshes[p.Chunk.LibraryIndex];
            filter.transform.localPosition = new Vector3(0f, 0f, p.StartS);
            filter.gameObject.SetActive(true);
            if (_signs[slot] != null)
            {
                TextMesh sign = _signs[slot];
                p.Chunk.GetOuterBounds(2f, out float x0, out _);
                p.Chunk.TryGetFloor(2f, x0, out float y);
                sign.transform.localPosition = new Vector3(x0 - 0.6f, y + 2.6f, 2f);
                sign.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                sign.text = _signTexts[p.Chunk.LibraryIndex];
            }

            for (int id = p.CoinBase; id < p.CoinBase + p.CoinCount; id++)
            {
                if (sim.IsCoinCollected(id))
                {
                    continue;
                }

                CoinPoint c = _path.GetCoin(id);
                GameObject go = _coins.Take();
                if (go == null)
                {
                    break;
                }

                go.transform.localPosition = new Vector3(c.X, c.Y, c.S);
                _coinBySlot[id % _coinBySlot.Length] = go;
                _activeCoins++;
            }

            for (int id = p.CrystalBase; id < p.CrystalBase + p.CrystalCount; id++)
            {
                if (tracker != null && tracker.IsCrystalCollected(id))
                {
                    continue;
                }

                CoinPoint c = _path.GetCrystal(id);
                GameObject go = _crystals.Take();
                if (go != null)
                {
                    go.transform.localPosition = new Vector3(c.X, c.Y, c.S);
                    _crystalBySlot[id % _crystalBySlot.Length] = go;
                }
            }

            for (int id = p.PowerUpBase; id < p.PowerUpBase + p.PowerUpCount; id++)
            {
                PowerUpPoint u = _path.GetPowerUp(id);
                GameObject go = _powerUps.Take();
                if (go != null)
                {
                    go.transform.localPosition = new Vector3(u.X, u.Y, u.S);
                    _powerUpBySlot[id % _powerUpBySlot.Length] = go;
                }
            }

            for (int id = p.DiscoveryBase; id < p.DiscoveryBase + p.Chunk.DiscoveryCount; id++)
            {
                DiscoveryPoint d = _path.GetDiscovery(id);
                GameObject go = _discoveries.Take();
                if (go != null)
                {
                    // A glowing post beside the path on the trigger's side (full-width triggers: right edge).
                    float local = d.S - p.StartS;
                    p.Chunk.GetOuterBounds(local, out float x0, out float x1);
                    bool full = d.XMax - d.XMin >= 20f;
                    float x = full ? x1 + 0.8f : (d.XMin + d.XMax) >= 0f ? Mathf.Max(x1, d.XMax) + 0.8f : Mathf.Min(x0, d.XMin) - 0.8f;
                    p.Chunk.TryGetFloor(local, Mathf.Clamp(x, x0, x1), out float y);
                    go.transform.localPosition = new Vector3(x, y + 1.6f, d.S);
                    _discoveryBySlot[id % _discoveryBySlot.Length] = go;
                }
            }
        }

        private void Unbind(int slot)
        {
            int serial = _boundSerial[slot];
            _boundSerial[slot] = -1;
            _chunkFilters[slot].gameObject.SetActive(false);
            if (serial < 0)
            {
                return;
            }

            ref readonly PlacedChunk p = ref _path.Chunk(serial);
            if (p.Serial != serial)
            {
                // Overwritten in the path ring already: release whatever this slot range holds.
                ReleaseAll();
                return;
            }

            for (int id = p.CoinBase; id < p.CoinBase + p.CoinCount; id++)
            {
                Release(_coinBySlot, id % _coinBySlot.Length, _coins, true);
            }

            for (int id = p.CrystalBase; id < p.CrystalBase + p.CrystalCount; id++)
            {
                Release(_crystalBySlot, id % _crystalBySlot.Length, _crystals, false);
            }

            for (int id = p.PowerUpBase; id < p.PowerUpBase + p.PowerUpCount; id++)
            {
                Release(_powerUpBySlot, id % _powerUpBySlot.Length, _powerUps, false);
            }

            for (int id = p.DiscoveryBase; id < p.DiscoveryBase + p.Chunk.DiscoveryCount; id++)
            {
                Release(_discoveryBySlot, id % _discoveryBySlot.Length, _discoveries, false);
            }
        }

        private void ReleaseAll()
        {
            for (int i = 0; i < _coinBySlot.Length; i++)
            {
                Release(_coinBySlot, i, _coins, true);
            }

            for (int i = 0; i < _crystalBySlot.Length; i++)
            {
                Release(_crystalBySlot, i, _crystals, false);
            }

            for (int i = 0; i < _powerUpBySlot.Length; i++)
            {
                Release(_powerUpBySlot, i, _powerUps, false);
            }

            for (int i = 0; i < _discoveryBySlot.Length; i++)
            {
                Release(_discoveryBySlot, i, _discoveries, false);
            }
        }

        private void Release(GameObject[] map, int slot, Pool pool, bool coin)
        {
            GameObject go = map[slot];
            if (go == null)
            {
                return;
            }

            map[slot] = null;
            pool.Return(go);
            if (coin)
            {
                _activeCoins--;
            }
        }

        private static string SignText(ChunkRuntime chunk)
        {
            ChunkDefinition d = chunk.Definition;
            string label = string.IsNullOrEmpty(d.Label) ? string.Empty : d.Label + " ";
            return chunk.Placeholder ? label + d.Id + " · " + chunk.VariantName + "\n[PLACEHOLDER: Part B traversal]" : label + d.Id + " · " + chunk.VariantName;
        }

        /// <summary>A fixed pool of inactive GameObjects under one parent.</summary>
        private sealed class Pool
        {
            private readonly GameObject[] _items;
            private readonly GameObject[] _active;
            private int _free;
            private int _activeCount;

            public Pool(Transform parent, string name, int size, Func<GameObject> create)
            {
                var root = new GameObject(name).transform;
                root.SetParent(parent, false);
                _items = new GameObject[size];
                _active = new GameObject[size];
                for (int i = 0; i < size; i++)
                {
                    GameObject go = create();
                    go.transform.SetParent(root, false);
                    go.SetActive(false);
                    _items[i] = go;
                }

                _free = size;
            }

            public GameObject Take()
            {
                if (_free == 0)
                {
                    return null;
                }

                GameObject go = _items[--_free];
                go.SetActive(true);
                _active[_activeCount++] = go;
                return go;
            }

            public void Return(GameObject go)
            {
                go.SetActive(false);
                _items[_free++] = go;
                for (int i = 0; i < _activeCount; i++)
                {
                    if (_active[i] == go)
                    {
                        _active[i] = _active[--_activeCount];
                        break;
                    }
                }
            }

            public void SetRotation(Quaternion rotation)
            {
                for (int i = 0; i < _activeCount; i++)
                {
                    _active[i].transform.localRotation = rotation;
                }
            }
        }
    }
}
