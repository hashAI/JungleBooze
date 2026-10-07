using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// One pooled obstacle rig (spec 005 3.1): a root at the middle of the row's footprint on the ground, with a
    /// <see cref="Body"/> group (the models that fill the hitbox), a <see cref="Context"/> group (supports and origin
    /// pieces), a <see cref="Decals"/> group (flat contact pads and marks) and a pool of generic primitive parts that
    /// <see cref="ObstacleRigBuilder"/> sets up when the rig is bound to an obstacle (at spawn, far in the fog). A rig
    /// can become any archetype, so a few rigs serve every obstacle. Per frame only transforms move; nothing
    /// allocates after construction.
    /// </summary>
    public sealed class ObstacleRig
    {
        /// <summary>Sway pivots and similar pooled empty transforms per rig.</summary>
        public const int PivotCapacity = 6;

        // Binding.
        public GameObject Root;
        public Transform Body;
        public Transform Context;
        public Transform Decals;
        public bool Live;
        public int Stamp;
        public int ObstacleId = -1;
        public ulong RunSeed;
        public ObstacleArchetype Kind;
        public byte LaneMask;
        public int Skin;
        public double CenterS;
        public float DepthM;
        public float EmbedM;
        public float PhaseSeed;
        public ObstacleShape Shape;

        // Pooled parts.
        private readonly Transform[] _parts;
        private readonly MeshFilter[] _filters;
        private readonly MeshRenderer[] _renderers;
        private readonly Transform[] _pivots;
        private int _used;
        private int _active;
        private int _pivotsUsed;

        // Art instances borrowed for this binding (returned by ArtPool.Release).
        public readonly Transform[] ArtTaken;
        public readonly int[] ArtTakenSlot;
        public int ArtTakenCount;

        // Sway (high barrier mats, thorn canes): lane -> pivot.
        public readonly Transform[] SwayPivot = new Transform[3];
        public readonly float[] SwayPhase = new float[3];
        public float SwayFullDeg;
        public float SwayNearDeg;
        public float SwayHz;

        // Mover.
        public Transform BarrelRoot;
        public Transform BarrelSpin;
        public Transform Chock;
        public Vector3 ChockRest;
        public float StartX;
        public float EndX;
        public float TravelSign;
        public float BarrelRestY;
        public MoverPhase PrevPhase;
        public float PopClockS = -1f;
        public float SettleClockS = -1f;
        public float SpinOffsetDeg;

        // Dust puffs (positions are local to Context).
        public readonly Transform[] Puffs;
        public readonly Vector3[] PuffPos;
        public readonly float[] PuffAge;
        public readonly float[] PuffSize;
        public int PuffCursor;
        public float PuffAccumulator;

        public ObstacleRig(Transform parent, string name, int partCapacity, int puffCapacity, Material placeholder, Material puffMaterial)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            Root = root.gameObject;
            Body = NewGroup("Body", root);
            Context = NewGroup("Context", root);
            Decals = NewGroup("Decals", root);

            _parts = new Transform[partCapacity];
            _filters = new MeshFilter[partCapacity];
            _renderers = new MeshRenderer[partCapacity];
            for (int i = 0; i < partCapacity; i++)
            {
                var go = new GameObject("P" + i);
                go.transform.SetParent(Context, false);
                _filters[i] = go.AddComponent<MeshFilter>();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = placeholder;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _renderers[i] = renderer;
                _parts[i] = go.transform;
                go.SetActive(false);
            }

            _pivots = new Transform[PivotCapacity];
            for (int i = 0; i < PivotCapacity; i++)
            {
                var go = new GameObject("Pivot" + i);
                go.transform.SetParent(Context, false);
                _pivots[i] = go.transform;
                go.SetActive(false);
            }

            ArtTaken = new Transform[16];
            ArtTakenSlot = new int[16];

            Puffs = new Transform[puffCapacity];
            PuffPos = new Vector3[puffCapacity];
            PuffAge = new float[puffCapacity];
            PuffSize = new float[puffCapacity];
            for (int i = 0; i < puffCapacity; i++)
            {
                var puff = new GameObject("Puff" + i);
                puff.transform.SetParent(Context, false);
                puff.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Quad);
                MeshRenderer puffRenderer = puff.AddComponent<MeshRenderer>();
                puffRenderer.sharedMaterial = puffMaterial;
                puffRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                puffRenderer.receiveShadows = false;
                Puffs[i] = puff.transform;
                puff.SetActive(false);
            }

            Root.SetActive(false);
        }

        /// <summary>Number of generic parts in use.</summary>
        public int PartsUsed => _used;

        /// <summary>Capacity of the part pool.</summary>
        public int PartCapacity => _parts.Length;

        private static Transform NewGroup(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>Clears the previous binding so the builder can lay out the next obstacle.</summary>
        public void BeginBuild()
        {
            _used = 0;
            _pivotsUsed = 0;
            for (int i = 0; i < PivotCapacity; i++)
            {
                _pivots[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < SwayPivot.Length; i++)
            {
                SwayPivot[i] = null;
                SwayPhase[i] = 0f;
            }

            BarrelRoot = null;
            BarrelSpin = null;
            Chock = null;
            PrevPhase = MoverPhase.Idle;
            PopClockS = -1f;
            SettleClockS = -1f;
            SpinOffsetDeg = 0f;
            PuffCursor = 0;
            PuffAccumulator = 0f;
            for (int i = 0; i < Puffs.Length; i++)
            {
                PuffAge[i] = -1f;
                if (Puffs[i] != null)
                {
                    Puffs[i].gameObject.SetActive(false);
                }
            }

            Body.localPosition = Vector3.zero;
            Body.localRotation = Quaternion.identity;
            Context.localPosition = Vector3.zero;
            Context.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Takes the next free generic part, makes it a primitive of <paramref name="type"/> with
        /// <paramref name="material"/> under <paramref name="parent"/> at the given local pose. Null when the pool is full.
        /// </summary>
        public Transform Add(
            Transform parent, PrimitiveType type, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (_used >= _parts.Length)
            {
                return null;
            }

            int i = _used++;
            Transform t = _parts[i];
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
            _filters[i].sharedMesh = PrimitiveMeshes.Get(type);
            _renderers[i].sharedMaterial = material;
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
            }

            return t;
        }

        /// <summary>Takes the next pooled empty transform (a hinge for sway or spin) under <paramref name="parent"/>. Null when none is left.</summary>
        public Transform AddPivot(Transform parent, Vector3 position)
        {
            if (_pivotsUsed >= _pivots.Length)
            {
                return null;
            }

            Transform t = _pivots[_pivotsUsed++];
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
            }

            return t;
        }

        /// <summary>Hides the parts the new binding did not use.</summary>
        public void EndBuild()
        {
            for (int i = _used; i < _active; i++)
            {
                _parts[i].gameObject.SetActive(false);
            }

            _active = _used;
        }

        /// <summary>Hides the rig (it left the view).</summary>
        public void Release()
        {
            Live = false;
            ObstacleId = -1;
            Root.SetActive(false);
        }
    }
}
