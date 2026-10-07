using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Lays out one <see cref="ObstacleRig"/> for one obstacle row (spec 005 sections 5 to 9, Jungle floor skins): the
    /// body that fills the hitbox (embedded in the ground), the supports and origin pieces, the contact decal and the
    /// debris. Art slots (<see cref="ObstacleArtSlot"/>) are used where the model exists; every piece has a procedural
    /// gray-box fallback that is already grounded. Runs once when a rig is bound, 90 m or more ahead in the fog, never
    /// per frame. Everything is a pure function of <see cref="RigBuildInfo"/> (a seeded hash, never a random source).
    /// Rig-local frame: x lateral, y up from the ground, z along the path, origin at the middle of the footprint.
    /// </summary>
    public sealed class ObstacleRigBuilder
    {
        private const float Overhang = 0.15f;
        private const float DecalY = 0.012f;

        private static readonly Quaternion FlatQuad = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Quaternion AlongX = Quaternion.Euler(0f, 0f, 90f);

        private readonly RigMaterials _m;
        private readonly ObstacleArtPool _art;
        private readonly ObstacleGroundingTuning _t;

        private ObstacleRig _rig;
        private RigBuildInfo _i;

        public ObstacleRigBuilder(RigMaterials materials, ObstacleArtPool art, ObstacleGroundingTuning tuning)
        {
            _m = materials;
            _art = art;
            _t = tuning;
        }

        /// <summary>Binds <paramref name="rig"/> to the obstacle described by <paramref name="info"/> and builds its pieces.</summary>
        public void Build(ObstacleRig rig, RigBuildInfo info)
        {
            _art.Release(rig);
            rig.BeginBuild();
            _rig = rig;
            _i = info;
            rig.Kind = info.Kind;
            rig.ObstacleId = info.ObstacleId;
            rig.RunSeed = info.RunSeed;
            rig.LaneMask = info.LaneMask;
            rig.Skin = info.Skin;
            rig.DepthM = info.DepthM;
            rig.EmbedM = info.EmbedM;
            rig.Shape = info.Shape;
            rig.PhaseSeed = 6.2831853f * GroundingMath.Roll(info.RunSeed, info.ObstacleId, 7);

            switch (info.Kind)
            {
                case ObstacleArchetype.LowBarrier:
                    BuildLow();
                    break;
                case ObstacleArchetype.HighBarrier:
                    BuildHigh();
                    break;
                case ObstacleArchetype.FullBlock:
                    BuildFull();
                    break;
                case ObstacleArchetype.Mover:
                    BuildMover();
                    break;
                case ObstacleArchetype.LaneDenial:
                    BuildThorn();
                    break;
            }

            rig.EndBuild();
            _rig = null;
            _i = null;
        }

        // ---------------------------------------------------------------- helpers

        private float LaneX(int lane)
        {
            return (lane - ((LaneMasks.LaneCount - 1) * 0.5f)) * _i.LaneWidthM;
        }

        private float Roll(uint salt)
        {
            return GroundingMath.Roll(_i.RunSeed, _i.ObstacleId, salt);
        }

        private Material Mat(Color color)
        {
            return _m.Opaque(color);
        }

        private int AnchorIndex(ContextKind kind)
        {
            for (int a = 0; a < _i.AnchorCount; a++)
            {
                if (_i.Anchors[a].Kind == kind)
                {
                    return a;
                }
            }

            return -1;
        }

        private Transform Box(Transform parent, Material material, float x, float y, float z, float sx, float sy, float sz)
        {
            return _rig.Add(parent, PrimitiveType.Cube, material, new Vector3(x, y, z), Quaternion.identity, new Vector3(sx, sy, sz));
        }

        private Transform BoxR(Transform parent, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return _rig.Add(parent, PrimitiveType.Cube, material, position, rotation, scale);
        }

        /// <summary>A standing cylinder: <paramref name="y"/> is its center.</summary>
        private Transform Pole(Transform parent, Material material, float x, float y, float z, float diameter, float height)
        {
            return _rig.Add(
                parent, PrimitiveType.Cylinder, material, new Vector3(x, y, z), Quaternion.identity,
                new Vector3(diameter, height * 0.5f, diameter));
        }

        private Transform Ball(Transform parent, Material material, float x, float y, float z, float sx, float sy, float sz)
        {
            return _rig.Add(parent, PrimitiveType.Sphere, material, new Vector3(x, y, z), Quaternion.identity, new Vector3(sx, sy, sz));
        }

        /// <summary>A flat quad lying on the ground, <paramref name="sx"/> across and <paramref name="sz"/> along the path.</summary>
        private void Decal(Material material, float x, float z, float sx, float sz, float y)
        {
            _rig.Add(_rig.Decals, PrimitiveType.Quad, material, new Vector3(x, y, z), FlatQuad, new Vector3(sx, sz, 1f));
        }

        private bool TryArt(ObstacleArtSlot slot, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return _art.Take(_rig, slot, parent, position, rotation, scale) != null;
        }

        private bool TryLegacy(ObstacleArtSlot slot, float x, float y, float z, float sx, float sy, float sz)
        {
            return _t.UseLegacyArt && _art.Take(_rig, slot, _rig.Body, new Vector3(x, y, z), Quaternion.identity, new Vector3(sx, sy, sz)) != null;
        }

        /// <summary>Red-ochre band flanked by ink bands above and below (spec 005 G8), wrapping a box of the given size.</summary>
        private void OchreBand(Transform parent, float x, float y, float z, float sx, float height, float sz, bool inkAbove)
        {
            Box(parent, Mat(GroundingPalette.Ochre), x, y, z, sx + 0.01f, height, sz + 0.01f);
            Box(parent, Mat(GroundingPalette.Ink), x, y - (height * 0.5f) - 0.04f, z, sx + 0.014f, 0.08f, sz + 0.014f);
            if (inkAbove)
            {
                Box(parent, Mat(GroundingPalette.Ink), x, y + (height * 0.5f) + 0.04f, z, sx + 0.014f, 0.08f, sz + 0.014f);
            }
        }

        /// <summary>Soft contact pad under a footprint (spec 005 G2).</summary>
        private void ContactPad(float minX, float maxX, float depthM)
        {
            float sx = (maxX - minX) * _t.ContactDecalScale;
            float sz = Mathf.Max(depthM * _t.ContactDecalScale, depthM + 0.5f);
            Decal(_m.ContactShadow, (minX + maxX) * 0.5f, 0f, sx, sz, DecalY);
        }

        /// <summary>A few pebbles, bark flakes and leaves (each at most 0.15 m high) around a footprint (spec 005 G2).</summary>
        private void Debris(float minX, float maxX, float depthM, uint salt)
        {
            int n = _t.DebrisPerObject;
            for (int k = 0; k < n; k++)
            {
                uint s = 100u + (salt * 32u) + (uint)(k * 3);
                float u0 = Roll(s);
                float u1 = Roll(s + 1u);
                float u2 = Roll(s + 2u);
                float size = 0.06f + (0.08f * u2);
                Color color = (k % 3) == 0 ? GroundingPalette.Bark : ((k % 3) == 1 ? GroundingPalette.Leaf : GroundingPalette.Stone);
                float x = Mathf.Lerp(minX - 0.3f, maxX + 0.3f, u0);
                float z = (u1 - 0.5f) * (depthM + 1.2f);
                BoxR(
                    _rig.Context, Mat(color), new Vector3(x, size * 0.35f, z), Quaternion.Euler(0f, 360f * u2, 0f),
                    new Vector3(size, size * 0.7f, size * 1.3f));
            }
        }

        // ---------------------------------------------------------------- A1 low barrier: the fallen trunk

        private void BuildLow()
        {
            float w = _i.Shape.WidthM;
            float d = _i.DepthM;
            float h = _i.Shape.TopM;
            float e = _i.EmbedM;
            int plateIndex = AnchorIndex(ContextKind.RootPlate);
            float plateX = plateIndex >= 0 ? _i.Anchors[plateIndex].X : 0f;
            int plateSide = plateIndex < 0 ? 0 : (plateX < 0f ? -1 : 1);
            bool mirror = _i.Variant.Mirror;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            Material wood = _m.Body;
            Material ridge = Mat(GroundingPalette.BarkDark);
            Material heart = Mat(GroundingPalette.Heartwood);
            Material soil = Mat(GroundingPalette.Soil);

            int lane = 0;
            while (lane < LaneMasks.LaneCount)
            {
                if (!LaneMasks.Contains(_i.LaneMask, lane))
                {
                    lane++;
                    continue;
                }

                int first = lane;
                while (lane + 1 < LaneMasks.LaneCount && LaneMasks.Contains(_i.LaneMask, lane + 1))
                {
                    lane++;
                }

                int last = lane;
                lane++;
                int runLanes = (last - first) + 1;
                float hitL = LaneX(first) - (w * 0.5f);
                float hitR = LaneX(last) + (w * 0.5f);
                float xl = hitL - Overhang;
                float xr = hitR + Overhang;
                bool plateLeft = plateSide < 0 && first == 0;
                bool plateRight = plateSide > 0 && last == LaneMasks.LaneCount - 1;
                if (plateLeft)
                {
                    xl = plateX + 0.3f;
                }

                if (plateRight)
                {
                    xr = plateX - 0.3f;
                }

                minX = Mathf.Min(minX, xl);
                maxX = Mathf.Max(maxX, xr);
                float len = xr - xl;
                float cx = (xl + xr) * 0.5f;
                float hitCx = (hitL + hitR) * 0.5f;

                ObstacleArtSlot slot = runLanes == 1 ? ObstacleArtSlot.LogTrunk : (runLanes == 2 ? ObstacleArtSlot.LogTrunkB : ObstacleArtSlot.LogTrunkC);
                var artScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
                bool art = TryArt(slot, _rig.Body, new Vector3(hitCx, 0f, 0f), Quaternion.identity, artScale);
                bool legacy = !art && TryLegacy(ObstacleArtSlot.LegacyLow, hitCx, (h - e) * 0.5f, 0f, hitR - hitL, h + e, d);
                if (!art && !legacy)
                {
                    // The log: a squarish trunk that fills the box, sunk by the embed, with a bark ridge on top and
                    // a soil lip where it displaces the ground.
                    Box(_rig.Body, wood, cx, (h - e) * 0.5f, 0f, len, h + e, d);
                    Box(_rig.Body, ridge, cx, h + 0.02f, 0f, len * 0.9f, 0.04f, d * 0.55f);
                    Box(_rig.Body, soil, cx, (0.08f - e) * 0.5f, 0f, len + 0.3f, 0.08f + e, d + 0.3f);
                    if (!plateLeft)
                    {
                        Box(_rig.Body, heart, xl - 0.02f, h * 0.5f, 0f, 0.05f, h * 0.8f, d * 0.8f);
                        Box(_rig.Body, ridge, xl + 0.2f, h + 0.1f, mirror ? 0.1f : -0.1f, 0.12f, 0.25f, 0.12f);
                    }

                    if (!plateRight)
                    {
                        Box(_rig.Body, heart, xr + 0.02f, h * 0.5f, 0f, 0.05f, h * 0.8f, d * 0.8f);
                        Box(_rig.Body, ridge, xr - 0.2f, h + 0.1f, mirror ? -0.1f : 0.1f, 0.12f, 0.25f, 0.12f);
                    }

                    OchreBand(_rig.Body, cx, h - 0.1f, 0f, len, 0.2f, d, false);
                }

                if (_i.Skin == 2)
                {
                    BuildLogRest(xl, xr, d, first == 0 ? -1f : (last == LaneMasks.LaneCount - 1 ? 1f : (Roll(30u) < 0.5f ? -1f : 1f)));
                }
            }

            if (minX > maxX)
            {
                return;
            }

            // Origin: the root plate and what the trunk fell from (context zone), and the leaf litter on the up-path side.
            for (int a = 0; a < _i.AnchorCount; a++)
            {
                ContextAnchor anchor = _i.Anchors[a];
                float z = anchor.ZOffsetM;
                float side = anchor.X < 0f ? -1f : 1f;
                switch (anchor.Kind)
                {
                    case ContextKind.RootPlate:
                    {
                        ObstacleArtSlot plateSlot = Roll(31u) < 0.5f ? ObstacleArtSlot.RootPlate : ObstacleArtSlot.RootPlateB;
                        if (!TryArt(plateSlot, _rig.Context, new Vector3(anchor.X, 0f, 0f), side < 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity, Vector3.one))
                        {
                            // An upturned root disc: 0.6 thick, 2.6 wide, 2.0 tall, sunk 0.1 m, with soil clumps.
                            _rig.Add(
                                _rig.Context, PrimitiveType.Cylinder, Mat(GroundingPalette.BarkDark), new Vector3(anchor.X, 0.9f, 0f),
                                AlongX, new Vector3(2.0f, 0.3f, 2.6f));
                            Ball(_rig.Context, Mat(GroundingPalette.Soil), anchor.X - (side * 0.5f), 0.12f, -0.9f, 0.7f, 0.4f, 0.6f);
                            Ball(_rig.Context, Mat(GroundingPalette.Soil), anchor.X - (side * 0.45f), 0.1f, 0.8f, 0.6f, 0.35f, 0.7f);
                            Ball(_rig.Context, Mat(GroundingPalette.Soil), anchor.X + (side * 0.4f), 0.1f, 1.2f, 0.5f, 0.3f, 0.5f);
                        }

                        break;
                    }

                    case ContextKind.Stump:
                        if (!TryArt(ObstacleArtSlot.Stump, _rig.Context, new Vector3(anchor.X, 0f, z), Quaternion.identity, Vector3.one))
                        {
                            Pole(_rig.Context, Mat(GroundingPalette.Bark), anchor.X, 0.4f, z, 1.1f, 0.9f);
                            Pole(_rig.Context, Mat(GroundingPalette.Heartwood), anchor.X, 0.86f, z, 0.95f, 0.04f);
                        }

                        break;
                    case ContextKind.Crown:
                        Ball(_rig.Context, Mat(GroundingPalette.Leaf), anchor.X, 0.7f, z, 1.8f, 1.3f, 1.6f);
                        Ball(_rig.Context, Mat(GroundingPalette.Moss), anchor.X - (side * 0.6f), 0.5f, z + 0.5f, 1.2f, 0.9f, 1.1f);
                        break;
                }
            }

            Box(_rig.Context, Mat(GroundingPalette.Leaf), (minX + maxX) * 0.5f, 0.03f, -(d * 0.5f) - 0.35f, (maxX - minX) * 0.7f, 0.06f, 0.55f);
            ContactPad(minX, maxX, d);
            Debris(minX, maxX, d, 1u);
        }

        /// <summary>Single log resting on a rock hump with an offcut and a drag scar leading toward the verge (skin 2).</summary>
        private void BuildLogRest(float xl, float xr, float d, float dir)
        {
            float endX = dir < 0f ? xl : xr;
            if (!TryArt(ObstacleArtSlot.RockHump, _rig.Context, new Vector3(endX + (dir * 0.35f), 0f, 0f), Quaternion.identity, Vector3.one))
            {
                Ball(_rig.Context, Mat(GroundingPalette.Stone), endX + (dir * 0.35f), 0f, 0f, 1.0f, 0.9f, 0.9f);
            }

            _rig.Add(
                _rig.Context, PrimitiveType.Cylinder, Mat(GroundingPalette.Bark), new Vector3(endX + (dir * 0.5f), 0.15f, 0.95f),
                AlongZ, new Vector3(0.3f, 0.45f, 0.3f));
            BoxR(
                _rig.Context, Mat(GroundingPalette.Bark), new Vector3(endX - (dir * 0.5f), 0.2f, -(d * 0.5f) - 0.1f),
                Quaternion.Euler(0f, 0f, dir * 30f), new Vector3(0.12f, 0.5f, 0.12f));
            float scarEnd = dir * 5.5f;
            Decal(_m.Furrow, (endX + scarEnd) * 0.5f, 0.9f, Mathf.Abs(scarEnd - endX), 0.4f, 0.011f);
        }

        // ---------------------------------------------------------------- A2 high barrier: root-and-liana curtain

        private void BuildHigh()
        {
            float w = _i.Shape.WidthM;
            float d = _i.DepthM;
            float matBottom = _i.Shape.BottomM;
            float matTop = _i.Shape.TopM + 0.05f;
            float limbY = _t.LimbHeightM;
            float matHeight = matTop - matBottom;
            Material matMaterial = _i.Skin == 1 ? Mat(GroundingPalette.Leaf) : _m.Body;
            Material ribMaterial = Mat(GroundingPalette.BarkDark);
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            ObstacleArtSlot matSlot = ObstacleArtSlot.HangMat;
            if (_i.Skin == 1)
            {
                matSlot = Roll(32u) < 0.5f ? ObstacleArtSlot.HangMatB : ObstacleArtSlot.HangMatC;
            }

            for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
            {
                if (!LaneMasks.Contains(_i.LaneMask, lane))
                {
                    continue;
                }

                float cx = LaneX(lane);
                minX = Mathf.Min(minX, cx - (w * 0.5f));
                maxX = Mathf.Max(maxX, cx + (w * 0.5f));

                // The mat hangs from a hinge at its top so it can sway without ever dropping its lowest tips.
                Transform pivot = _rig.AddPivot(_rig.Body, new Vector3(cx, matTop, 0f));
                _rig.SwayPivot[lane] = pivot;
                _rig.SwayPhase[lane] = 6.2831853f * Roll((uint)(40 + lane));
                Transform p = pivot != null ? pivot : _rig.Body;
                float ox = pivot != null ? 0f : cx;
                float oy = pivot != null ? 0f : matTop;

                if (!TryArt(matSlot, p, new Vector3(ox, oy - matTop, 0f), Quaternion.identity, new Vector3(_i.Variant.Mirror ? -1f : 1f, 1f, 1f)))
                {
                    // The mat fills the box (2.04 x 0.5 from 1.1 to 3.05) with hanging root strands in front.
                    Box(p, matMaterial, ox, oy - (matHeight * 0.5f), 0f, w, matHeight, d);
                    for (int s = 0; s < 6; s++)
                    {
                        float len = 1.62f + (0.3f * Roll((uint)(50 + (lane * 8) + s)));
                        Box(p, ribMaterial, ox - (w * 0.5f) + ((s + 0.5f) * (w / 6f)), oy - (len * 0.5f), -(d * 0.5f) - 0.04f, 0.12f, len, 0.08f);
                    }

                    if (_i.Skin == 1)
                    {
                        Ball(p, Mat(GroundingPalette.Moss), ox - 0.5f, oy - 0.5f, -(d * 0.5f), 0.8f, 0.7f, 0.25f);
                        Ball(p, Mat(GroundingPalette.Moss), ox + 0.4f, oy - 1.2f, -(d * 0.5f), 0.7f, 0.6f, 0.25f);
                    }

                    // Lowest band: ochre cloth wrapped around the root tips (red underside = slide), flanked by ink.
                    float bandY = oy + ((matBottom + 0.20f) - matTop);
                    Box(p, Mat(GroundingPalette.Ochre), ox, bandY, 0f, w * 0.5f, 0.16f, d + 0.02f);
                    Box(p, Mat(GroundingPalette.Ink), ox, bandY - 0.10f, 0f, w * 0.5f, 0.04f, d + 0.03f);
                    Box(p, Mat(GroundingPalette.Ink), ox, bandY + 0.10f, 0f, w * 0.5f, 0.04f, d + 0.03f);
                }

                // Two liana ties from the limb down to the top of the mat (the support is never implied, G3).
                float tieLen = limbY - matTop;
                for (int side = -1; side <= 1; side += 2)
                {
                    float tx = cx + (side * 0.9f);
                    if (!TryArt(ObstacleArtSlot.LianaTie, _rig.Context, new Vector3(tx, limbY, 0f), Quaternion.identity, new Vector3(1f, tieLen, 1f)))
                    {
                        Box(_rig.Context, Mat(GroundingPalette.Moss), tx, matTop + (tieLen * 0.5f), 0f, 0.04f, tieLen, 0.04f);
                    }
                }

                Decal(_m.ContactShadow, cx, 0.3f, w, 1.5f, DecalY);
            }

            if (minX > maxX)
            {
                return;
            }

            int trunkIndex = AnchorIndex(ContextKind.SupportTrunk);
            if (trunkIndex >= 0)
            {
                float trunkX = _i.Anchors[trunkIndex].X;
                float farX = trunkX > 0f ? minX - 0.5f : maxX + 0.5f;
                float span = Mathf.Abs(trunkX - farX);
                Pole(_rig.Context, Mat(GroundingPalette.BarkDark), trunkX, 3.9f, 0f, 1.6f, 8f);
                Box(_rig.Context, Mat(GroundingPalette.BarkDark), trunkX - (Mathf.Sign(trunkX) * 0.7f), 0.05f, 0.5f, 0.6f, 0.12f, 0.4f);
                float dirToFar = farX < trunkX ? -1f : 1f;
                Quaternion limbYaw = dirToFar < 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                if (!TryArt(ObstacleArtSlot.Limb, _rig.Context, new Vector3(trunkX, limbY, 0f), limbYaw, new Vector3(span, 1f, 1f)))
                {
                    // Thick near the trunk (0.7 m), thin at the far end (0.4 m); the underside is at limbY.
                    float quarter = span * 0.25f;
                    _rig.Add(
                        _rig.Context, PrimitiveType.Cylinder, Mat(GroundingPalette.BarkDark),
                        new Vector3(trunkX + (dirToFar * quarter), limbY + 0.35f, 0f), AlongX, new Vector3(0.7f, span * 0.25f, 0.7f));
                    _rig.Add(
                        _rig.Context, PrimitiveType.Cylinder, Mat(GroundingPalette.Bark),
                        new Vector3(trunkX + (dirToFar * quarter * 3f), limbY + 0.2f, 0f), AlongX, new Vector3(0.4f, span * 0.25f, 0.4f));
                }
            }

            Debris(minX, maxX, d, 2u);
        }

        // ---------------------------------------------------------------- A3 full block: buttress-root tree, standing stone, wedged slab

        private void BuildFull()
        {
            float w = _i.Shape.WidthM;
            float d = _i.DepthM;
            float h = _i.Shape.TopM;
            float e = _i.EmbedM;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            Material flute = Mat(GroundingPalette.BarkDark);
            Material stoneBody = _i.Skin == 0 ? _m.Body : _m.Mover;
            Material leaf = Mat(GroundingPalette.Leaf);

            int lane = 0;
            while (lane < LaneMasks.LaneCount)
            {
                if (!LaneMasks.Contains(_i.LaneMask, lane))
                {
                    lane++;
                    continue;
                }

                int first = lane;
                while (lane + 1 < LaneMasks.LaneCount && LaneMasks.Contains(_i.LaneMask, lane + 1))
                {
                    lane++;
                }

                int last = lane;
                lane++;
                float runL = LaneX(first) - (w * 0.5f);
                float runR = LaneX(last) + (w * 0.5f);
                minX = Mathf.Min(minX, runL);
                maxX = Mathf.Max(maxX, runR);

                for (int l = first; l <= last; l++)
                {
                    float cx = LaneX(l);
                    if (_i.Skin == 2)
                    {
                        BuildWedgedSlab(cx, w, d, h, e);
                    }
                    else
                    {
                        BuildFin(cx, w, d, h, e, stoneBody, flute, leaf);
                    }
                }

                if (_i.Skin == 0)
                {
                    // The round trunk rises BEHIND the fin, so the lethal front 1.0 m is a flat-faced mass (7.1).
                    float r = last > first ? 1.2f : 1.0f;
                    float tx = (runL + runR) * 0.5f;
                    Pole(_rig.Body, flute, tx, 6.9f, (d * 0.5f) + r, r * 2f, 14f);
                    if (!TryArt(ObstacleArtSlot.RootWeb, _rig.Body, new Vector3(tx, 0f, 0f), Quaternion.identity, Vector3.one))
                    {
                        if (last > first)
                        {
                            // Root web bridging the 0.36 m crack at the base, at most 0.12 m high.
                            Box(_rig.Body, flute, tx, 0.04f, 0f, _i.LaneWidthM - w + 0.5f, 0.1f, d);
                        }

                        Box(_rig.Body, flute, runL - 0.2f, 0.04f, -(d * 0.5f) + 0.3f, 0.6f, 0.1f, 0.4f);
                        Box(_rig.Body, flute, runR + 0.2f, 0.04f, -(d * 0.5f) + 0.3f, 0.6f, 0.1f, 0.4f);
                    }
                }

                Box(_rig.Context, leaf, (runL + runR) * 0.5f, 0.03f, -(d * 0.5f) - 0.5f, (runR - runL) * 0.9f, 0.06f, 0.7f);
            }

            if (minX > maxX)
            {
                return;
            }

            int vergeIndex = AnchorIndex(ContextKind.VergeTrunk);
            if (vergeIndex >= 0)
            {
                float vx = _i.Anchors[vergeIndex].X;
                float side = vx < 0f ? -1f : 1f;
                Pole(_rig.Context, flute, vx, 5.9f, 0f, 1.2f, 12f);
                float rootFrom = side * (_i.Shape.WidthM * 0.5f + (_i.LaneWidthM * 1f));
                Box(_rig.Context, flute, (rootFrom + (vx - (side * 0.6f))) * 0.5f, 0.04f, 0f, Mathf.Abs((vx - (side * 0.6f)) - rootFrom), 0.1f, 0.45f);
            }

            ContactPad(minX, maxX, d);
            Debris(minX, maxX, d, 3u);
        }

        private void BuildFin(float cx, float w, float d, float h, float e, Material body, Material flute, Material leaf)
        {
            ObstacleArtSlot slot = _i.Skin == 0 ? ObstacleArtSlot.ButtressFin : ObstacleArtSlot.StandingStone;
            bool art = TryArt(slot, _rig.Body, new Vector3(cx, 0f, 0f), Quaternion.identity, new Vector3(_i.Variant.Mirror ? -1f : 1f, 1f, 1f));
            bool legacy = !art && TryLegacy(ObstacleArtSlot.LegacyFull, cx, (h - e) * 0.5f, 0f, w, h + e, d);
            if (art || legacy)
            {
                return;
            }

            // The fin: a flat-faced wall 2.04 x 1.0 x 3.0 that covers the whole lethal front face, sunk by the embed.
            Box(_rig.Body, body, cx, (h - e) * 0.5f, 0f, w, h + e, d);
            if (_i.Skin == 0)
            {
                for (int f = 0; f < 5; f++)
                {
                    Box(_rig.Body, flute, cx - 0.8f + (0.4f * f), 1.45f, -(d * 0.5f) - 0.03f, 0.14f, 2.9f, 0.08f);
                }

                Box(_rig.Body, flute, cx - 0.5f, h + 0.03f, 0f, 0.45f, 0.06f, d * 0.8f);
                Box(_rig.Body, flute, cx + 0.45f, h + 0.03f, 0f, 0.45f, 0.06f, d * 0.8f);
                Box(_rig.Body, Mat(GroundingPalette.Moss), cx - 0.4f, 0.5f, -(d * 0.5f) - 0.015f, 0.5f, 0.9f, 0.03f);
            }
            else
            {
                // Standing stone: vine wraps and a ring of ferns at the foot.
                for (int v = 0; v < 3; v++)
                {
                    float y = 0.9f + (0.8f * v) - (v == 2 ? 0.1f : 0f);
                    BoxR(_rig.Body, leaf, new Vector3(cx, y, 0f), Quaternion.Euler(0f, 0f, (v % 2 == 0) ? 10f : -10f), new Vector3(w + 0.05f, 0.07f, d + 0.05f));
                }

                Ball(_rig.Context, leaf, cx - (w * 0.5f) - 0.05f, 0.12f, 0.4f, 0.5f, 0.3f, 0.5f);
                Ball(_rig.Context, leaf, cx + (w * 0.5f) + 0.05f, 0.12f, -0.4f, 0.5f, 0.3f, 0.5f);
                Ball(_rig.Context, leaf, cx - 0.5f, 0.12f, -(d * 0.5f) - 0.15f, 0.5f, 0.3f, 0.4f);
                Ball(_rig.Context, leaf, cx + 0.5f, 0.12f, -(d * 0.5f) - 0.15f, 0.5f, 0.3f, 0.4f);
            }

            OchreBand(_rig.Body, cx, 1.0f, 0f, w, 0.2f, d, false);
        }

        private void BuildWedgedSlab(float cx, float w, float d, float h, float e)
        {
            int side = ContextLayout.LaneSide(_i.LaneMask);
            if (side == 0)
            {
                side = cx < 0f ? -1 : 1;
            }

            Transform hinge = _rig.AddPivot(_rig.Body, new Vector3(cx, -e, 0f));
            Transform p = hinge != null ? hinge : _rig.Body;
            float oy = hinge != null ? 0f : -e;
            float ox = hinge != null ? 0f : cx;
            if (hinge != null)
            {
                // Leans toward the verge trunk by at most 1.5 degrees, so the top stays within 0.08 m of the box.
                hinge.localRotation = Quaternion.Euler(0f, 0f, -side * _t.WedgedSlabLeanDeg);
            }

            if (!TryArt(ObstacleArtSlot.WedgedSlab, p, new Vector3(ox, oy + e, 0f), Quaternion.identity, new Vector3(_i.Variant.Mirror ? -1f : 1f, 1f, 1f)))
            {
                Box(p, _m.Mover, ox, oy + ((h + e) * 0.5f), 0f, w, h + e, d);
                Box(p, Mat(GroundingPalette.Moss), ox - 0.4f, oy + 0.7f, -(d * 0.5f) - 0.015f, 0.6f, 0.8f, 0.03f);
                OchreBand(p, ox, oy + 1.0f + e, 0f, w, 0.2f, d, false);
            }

            // Jam stones wedged at the base on the verge side.
            float jx = cx + (side * ((w * 0.5f) + 0.12f));
            Ball(_rig.Context, Mat(GroundingPalette.Stone), jx, 0.14f, -0.3f, 0.5f, 0.4f, 0.5f);
            Ball(_rig.Context, Mat(GroundingPalette.StoneDark), jx + (side * 0.2f), 0.12f, 0.25f, 0.45f, 0.35f, 0.45f);
            Ball(_rig.Context, Mat(GroundingPalette.StoneLight), jx, 0.1f, 0.75f, 0.35f, 0.3f, 0.35f);
        }

        // ---------------------------------------------------------------- A4 mover: barrel boulder with wallow, chock and furrow

        private void BuildMover()
        {
            float r = _t.RollRadiusM;
            float startX = LaneX(_i.FromLane);
            float endX = LaneX(_i.ToLane);
            float dir = endX >= startX ? 1f : -1f;
            float travel = Mathf.Abs(endX - startX);
            _rig.StartX = startX;
            _rig.EndX = endX;
            _rig.TravelSign = dir;

            // Wallow: the hollow the barrel sits in.
            if (!TryArt(ObstacleArtSlot.Wallow, _rig.Decals, new Vector3(startX, 0.010f, 0f), Quaternion.identity, Vector3.one))
            {
                Decal(_m.Wallow, startX, 0f, 2.4f, 2.2f, 0.010f);
                Decal(_m.ContactShadow, startX, 0f, 1.8f, 1.6f, 0.011f);
            }

            // Chock: a wedge stone jammed under the barrel on the end-lane side, a stake and three pebbles.
            var chockPos = new Vector3(startX + (dir * 0.5f), 0.1f, 0f);
            Transform chock = _art.Take(_rig, ObstacleArtSlot.Chock, _rig.Context, chockPos, Quaternion.identity, Vector3.one);
            if (chock == null)
            {
                chock = BoxR(_rig.Context, Mat(GroundingPalette.StoneLight), chockPos, Quaternion.Euler(0f, 0f, -dir * 20f), new Vector3(0.4f, 0.24f, 0.9f));
            }

            _rig.Chock = chock;
            _rig.ChockRest = chockPos;
            Pole(_rig.Context, Mat(GroundingPalette.Bark), startX + (dir * 0.9f), 0.2f, 0.35f, 0.1f, 0.4f);
            Ball(_rig.Context, Mat(GroundingPalette.Stone), startX + (dir * 0.75f), 0.05f, -0.5f, 0.14f, 0.1f, 0.14f);
            Ball(_rig.Context, Mat(GroundingPalette.StoneDark), startX + (dir * 0.95f), 0.04f, -0.2f, 0.12f, 0.08f, 0.12f);
            Ball(_rig.Context, Mat(GroundingPalette.StoneLight), startX + (dir * 0.8f), 0.04f, 0.6f, 0.1f, 0.08f, 0.1f);

            // Furrow: the groove from the wallow to the end lane, with ochre ticks along its edges every metre.
            float midX = (startX + endX) * 0.5f;
            if (travel > 0.1f)
            {
                if (!TryArt(ObstacleArtSlot.Furrow, _rig.Decals, new Vector3(midX, 0.011f, 0f), Quaternion.identity, new Vector3(travel, 1f, 1f)))
                {
                    Decal(_m.Furrow, midX, 0f, travel, 0.5f, 0.011f);
                }

                int ticks = Mathf.Min(2, Mathf.FloorToInt(travel));
                for (int k = 1; k <= ticks; k++)
                {
                    for (int edge = -1; edge <= 1; edge += 2)
                    {
                        float tx = startX + (dir * k);
                        Box(_rig.Decals, Mat(GroundingPalette.Ink), tx, 0.009f, edge * 0.32f, 0.16f, 0.014f, 0.22f);
                        Box(_rig.Decals, Mat(GroundingPalette.Ochre), tx, 0.014f, edge * 0.32f, 0.08f, 0.016f, 0.12f);
                    }
                }
            }

            // End sand bar: the loose-sand catch with impact marks already in it.
            if (!TryArt(ObstacleArtSlot.SandBar, _rig.Decals, new Vector3(endX, 0.010f, 0f), Quaternion.identity, Vector3.one))
            {
                Decal(_m.Sand, endX, 0f, 2.4f, 2.2f, 0.010f);
                Ball(_rig.Context, Mat(GroundingPalette.Sand), endX, -0.02f, 0f, 2.0f, 0.16f, 1.8f);
                Decal(_m.Wallow, endX - (dir * 0.5f), -0.3f, 0.55f, 0.35f, 0.013f);
                Decal(_m.Wallow, endX + (dir * 0.2f), 0.4f, 0.45f, 0.3f, 0.013f);
                Decal(_m.Wallow, endX - (dir * 0.1f), 0.1f, 0.4f, 0.4f, 0.013f);
            }

            // Source: the rock ledge and scree the barrel rolled from, with old pale roll marks leading to the wallow.
            int bankIndex = AnchorIndex(ContextKind.ScreeBank);
            if (bankIndex >= 0)
            {
                ContextAnchor bank = _i.Anchors[bankIndex];
                float bz = bank.ZOffsetM;
                Ball(_rig.Context, Mat(GroundingPalette.Stone), bank.X, 0.6f, bz, 2.6f, 1.9f, 2.2f);
                Ball(_rig.Context, Mat(GroundingPalette.StoneDark), bank.X + (dir * 0.9f), 0.35f, bz + 0.5f, 1.4f, 0.9f, 1.2f);
                for (int s = 0; s < 4; s++)
                {
                    float size = 0.12f + (0.08f * Roll((uint)(60 + s)));
                    Ball(
                        _rig.Context, Mat(GroundingPalette.Stone), bank.X + (dir * (1.7f + (0.4f * s))), size * 0.4f,
                        bz + ((Roll((uint)(64 + s)) - 0.5f) * 1.6f), size, size * 0.8f, size);
                }

                float marksFrom = bank.X + (dir * 1.5f);
                float marksTo = startX - (dir * 1.3f);
                float marksLen = (marksTo - marksFrom) * dir;
                if (marksLen > 0.5f)
                {
                    Decal(_m.OldMarks, (marksFrom + marksTo) * 0.5f, 0f, marksLen, 0.8f, 0.009f);
                }
            }

            // The barrel: radius 0.95 m, axis along the path, 0.03 m in the ground; it rolls about its axis.
            _rig.BarrelRestY = r - _t.EmbedMinM;
            Transform root = _rig.AddPivot(_rig.Body, new Vector3(startX, _rig.BarrelRestY, 0f));
            Transform spin = root != null ? _rig.AddPivot(root, Vector3.zero) : null;
            if (root == null || spin == null)
            {
                return;
            }

            _rig.BarrelRoot = root;
            _rig.BarrelSpin = spin;
            if (!TryArt(ObstacleArtSlot.BarrelBoulder, spin, Vector3.zero, Quaternion.identity, Vector3.one))
            {
                float dia = r * 2f;
                _rig.Add(spin, PrimitiveType.Cylinder, _m.Mover, Vector3.zero, AlongZ, new Vector3(dia, 0.8f, dia));
                // The ochre ring (0.35 m wide) flanked by ink rings, so the spin is readable.
                _rig.Add(spin, PrimitiveType.Cylinder, Mat(GroundingPalette.Ink), Vector3.zero, AlongZ, new Vector3(dia * 1.008f, 0.26f, dia * 1.008f));
                _rig.Add(spin, PrimitiveType.Cylinder, Mat(GroundingPalette.Ochre), Vector3.zero, AlongZ, new Vector3(dia * 1.014f, 0.175f, dia * 1.014f));
                // Lichen, a worn flat and a crack on the surface (they turn with the barrel).
                for (int p = 0; p < 3; p++)
                {
                    float phi = (30f + (120f * p)) * Mathf.Deg2Rad;
                    Color patch = p == 0 ? GroundingPalette.Moss : (p == 1 ? GroundingPalette.StoneLight : GroundingPalette.StoneDark);
                    var at = new Vector3(Mathf.Sin(phi) * r * 0.985f, Mathf.Cos(phi) * r * 0.985f, (p - 1) * 0.45f);
                    BoxR(spin, Mat(patch), at, Quaternion.Euler(0f, 0f, -phi * Mathf.Rad2Deg), new Vector3(0.45f, 0.08f, 0.5f));
                }
            }

            // Moving contact pad under the barrel.
            _rig.Add(root, PrimitiveType.Quad, _m.ContactShadow, new Vector3(0f, DecalY - _rig.BarrelRestY, 0f), FlatQuad, new Vector3(r * 2f * _t.ContactDecalScale, 1.9f, 1f));
        }

        // ---------------------------------------------------------------- A5 thorn patch

        private void BuildThorn()
        {
            float w = _i.Shape.WidthM;
            float d = _i.DepthM;
            float h = _i.Shape.TopM;
            float e = _i.EmbedM;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            Material cane = Mat(GroundingPalette.ThornDark);
            Material core = Mat(GroundingPalette.Thorn);
            Material wood = Mat(GroundingPalette.Bark);

            for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
            {
                if (!LaneMasks.Contains(_i.LaneMask, lane))
                {
                    continue;
                }

                float cx = LaneX(lane);
                minX = Mathf.Min(minX, cx - (w * 0.5f));
                maxX = Mathf.Max(maxX, cx + (w * 0.5f));
                float front = -(d * 0.5f);

                // The tops tremble: they hang from a hinge 0.5 m below the top edge.
                Transform hinge = _rig.AddPivot(_rig.Body, new Vector3(cx, h - 0.5f, 0f));
                _rig.SwayPivot[lane] = hinge;
                _rig.SwayPhase[lane] = 6.2831853f * Roll((uint)(70 + lane));

                bool art = TryArt(ObstacleArtSlot.ThornCage, _rig.Body, new Vector3(cx, 0f, 0f), Quaternion.identity, new Vector3(_i.Variant.Mirror ? -1f : 1f, 1f, 1f));
                bool legacy = !art && TryLegacy(ObstacleArtSlot.LegacyThorn, cx, (h - e) * 0.5f, 0f, w, h + e, d);
                if (art)
                {
                    // The cane wall model is a second layer over the cage model.
                    TryArt(ObstacleArtSlot.CaneWall, _rig.Body, new Vector3(cx, 0f, 0f), Quaternion.identity, new Vector3(_i.Variant.Mirror ? -1f : 1f, 1f, 1f));
                }

                if (!art && !legacy)
                {
                    // Root cage and cane wall filling 2.04 x 4.0 x 2.2: front and sides are walls (G6).
                    Box(_rig.Body, core, cx, (h - e) * 0.5f, 0f, w, h + e, d);
                    for (int c = 0; c < 6; c++)
                    {
                        Pole(_rig.Body, cane, cx - 0.85f + (0.34f * c), (h * 0.5f) - 0.05f, front + 0.02f, 0.22f, h);
                    }

                    Transform p = hinge != null ? hinge : _rig.Body;
                    float oy = hinge != null ? 0f : h - 0.5f;
                    float ox = hinge != null ? 0f : cx;
                    for (int t = 0; t < 4; t++)
                    {
                        float top = h + ((Roll((uint)(80 + (lane * 4) + t)) - 0.6f) * 0.15f);
                        Box(p, cane, ox - 0.75f + (0.5f * t), oy + (top - 0.05f - (h - 0.5f)), front + 0.35f, 0.35f, 0.1f, 0.5f);
                    }

                    // Thorn hooks: ink only, never red (G8).
                    Material ink = Mat(GroundingPalette.Ink);
                    float step = w / 4f;
                    for (int t = 0; t < 3; t++)
                    {
                        float tx = cx - (w * 0.5f) + (step * (t + 1));
                        float faceY = (h * (0.35f + (0.2f * (t % 2))));
                        BoxR(_rig.Body, ink, new Vector3(tx, faceY, front), Quaternion.Euler(45f, 45f, 0f), new Vector3(0.28f, 0.28f, 0.28f));
                        BoxR(_rig.Body, ink, new Vector3(tx, h, front + 0.3f), Quaternion.Euler(45f, 45f, 0f), new Vector3(0.28f, 0.28f, 0.28f));
                    }

                    // Three root-cage posts at the front carry the ochre stripes.
                    for (int post = -1; post <= 1; post++)
                    {
                        float px = cx + (post * 0.65f);
                        Box(_rig.Body, wood, px, 0.9f, front - 0.02f, 0.16f, 1.8f, 0.16f);
                        OchreBand(_rig.Body, px, 1.0f, front - 0.02f, 0.16f, 0.14f, 0.16f, true);
                    }

                    Box(_rig.Body, Mat(GroundingPalette.BarkDark), cx - (w * 0.5f) - 0.1f, 0.04f, front + 0.5f, 0.5f, 0.08f, 0.5f);
                    Box(_rig.Body, Mat(GroundingPalette.BarkDark), cx + (w * 0.5f) + 0.1f, 0.04f, front + 0.5f, 0.5f, 0.08f, 0.5f);
                    if (_i.Skin == 1)
                    {
                        // Hedge along a fallen trunk: the log end shows at the front.
                        _rig.Add(_rig.Body, PrimitiveType.Cylinder, wood, new Vector3(cx, 0.4f, front - 0.1f), AlongZ, new Vector3(0.6f, 0.3f, 0.6f));
                    }
                }
            }

            if (minX > maxX)
            {
                return;
            }

            int crownIndex = AnchorIndex(ContextKind.RootCrown);
            if (crownIndex >= 0)
            {
                float crownX = _i.Anchors[crownIndex].X;
                if (!TryArt(ObstacleArtSlot.RootCrown, _rig.Context, new Vector3(crownX, 0f, 0f), Quaternion.identity, Vector3.one))
                {
                    // Where the thicket comes from: a woody crown with a fan of canes (2.5 m at most).
                    Pole(_rig.Context, wood, crownX, 0.5f, 0f, 1.2f, 1.2f);
                    for (int c = -1; c <= 1; c++)
                    {
                        float theta = c * 18f * Mathf.Deg2Rad;
                        _rig.Add(
                            _rig.Context, PrimitiveType.Cylinder, cane,
                            new Vector3(crownX - (Mathf.Sin(theta) * 0.8f), 0.9f + (Mathf.Cos(theta) * 0.8f), 0f),
                            Quaternion.Euler(0f, 0f, c * 18f), new Vector3(0.2f, 0.8f, 0.2f));
                    }
                }
            }

            ContactPad(minX, maxX, d);
            TryArt(ObstacleArtSlot.ThornLitter, _rig.Decals, new Vector3((minX + maxX) * 0.5f, DecalY, -(d * 0.5f) - 0.4f), Quaternion.identity, Vector3.one);
            Debris(minX, maxX, d, 4u);
        }
    }
}
