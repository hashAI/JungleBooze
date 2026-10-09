using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Editor.LookTest;
using UnityEngine;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Leaf-card meshes cut from the plant atlases (Art/Environment/Plants, ADR 0008): a curved, slightly folded
    /// card per leaf or stalk, and clumps of them. The stem end of every atlas cell is at the bottom (v = yMin).
    /// Vertex color R = wind weight (grows toward the tip), A = ambient occlusion (darker at the base).
    /// </summary>
    public static class HeroBasinPlants
    {
        /// <summary>Atlas cells (xMin, yMin, xMax, yMax) measured on the delivered atlases.</summary>
        public static readonly Rect[] Broadleaf =
        {
            Rect.MinMaxRect(0.099f, 0.500f, 0.496f, 0.977f), Rect.MinMaxRect(0.608f, 0.500f, 0.870f, 0.983f),
            Rect.MinMaxRect(0.094f, 0.018f, 0.480f, 0.500f), Rect.MinMaxRect(0.572f, 0.019f, 0.881f, 0.500f),
        };

        public static readonly Rect[] Fronds =
        {
            Rect.MinMaxRect(0.046f, 0.074f, 0.340f, 0.938f), Rect.MinMaxRect(0.340f, 0.075f, 0.670f, 0.918f),
            Rect.MinMaxRect(0.670f, 0.074f, 0.950f, 0.688f),
        };

        public static readonly Rect[] Bellcaps =
        {
            Rect.MinMaxRect(0.066f, 0.013f, 0.314f, 0.979f), Rect.MinMaxRect(0.364f, 0.013f, 0.633f, 0.980f),
        };

        public static readonly Rect[] Blades =
        {
            Rect.MinMaxRect(0.723f, 0.047f, 0.770f, 0.906f), Rect.MinMaxRect(0.770f, 0.043f, 0.855f, 0.906f),
            Rect.MinMaxRect(0.880f, 0.042f, 0.934f, 0.893f),
        };

        /// <summary>
        /// One card from an atlas cell: base at the origin, growing along +z, rising at <paramref name="pitchDeg"/>
        /// and drooping by <paramref name="droop"/> (fraction of the length) at the tip; width from the cell's aspect.
        /// A small fold along the midrib keeps it from reading as a flat sheet.
        /// </summary>
        public static Mesh Card(Rect cell, float length, float pitchDeg, float droop, float fold, float aspect = 1f)
        {
            const int rows = 5;
            const int cols = 3;
            float width = length * cell.width / Mathf.Max(0.01f, cell.height) * aspect;
            var positions = new Vector3[cols, rows + 1];
            var frames = new LookTestMeshFactory.Frame[cols, rows + 1];
            var uvs = new Vector2[cols, rows + 1];
            var colors = new Color[cols, rows + 1];
            float pitch = pitchDeg * Mathf.Deg2Rad;
            for (int j = 0; j <= rows; j++)
            {
                float t = (float)j / rows;
                float along = t * length;
                float y = Mathf.Sin(pitch) * along - droop * length * t * t;
                float z = Mathf.Cos(pitch) * along;
                for (int i = 0; i < cols; i++)
                {
                    float s = (float)i / (cols - 1) - 0.5f;
                    float x = s * width;
                    float lift = fold * width * Mathf.Abs(s) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.3f));
                    positions[i, j] = new Vector3(x, y + lift, z);
                    uvs[i, j] = new Vector2(Mathf.Lerp(cell.xMin, cell.xMax, s + 0.5f), Mathf.Lerp(cell.yMin, cell.yMax, t));
                    colors[i, j] = new Color(t * t, 0f, 0f, Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(t)));
                }
            }

            for (int j = 0; j <= rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    Vector3 up = positions[i, Mathf.Min(j + 1, rows)] - positions[i, Mathf.Max(j - 1, 0)];
                    Vector3 side = positions[Mathf.Min(i + 1, cols - 1), j] - positions[Mathf.Max(i - 1, 0), j];
                    Vector3 n = Vector3.Cross(up, side).normalized;
                    if (n.y < 0f)
                    {
                        n = -n;
                    }

                    // Softer, rounder shading: lean the normal toward up.
                    n = (n + Vector3.up * 0.6f).normalized;
                    frames[i, j] = new LookTestMeshFactory.Frame { Normal = n, Tangent = side.normalized, Bitangent = up.normalized };
                }
            }

            return LookTestMeshFactory.Grid("Card", positions, frames, uvs, colors);
        }

        /// <summary>A rosette of leaves around a base: random cells, lengths and pitches, evenly turned with jitter.</summary>
        public static void Clump(LookTestMeshAccumulator target, IRandom rng, Rect[] cells, Vector3 basePoint, float yawDeg, int leaves, Vector2 length, Vector2 pitchDeg, float droop, LookTestMeshAccumulator.Painter painter)
        {
            for (int i = 0; i < leaves; i++)
            {
                Rect cell = cells[rng.NextInt(0, cells.Length)];
                Mesh card = Card(cell, rng.NextFloat(length.x, length.y), rng.NextFloat(pitchDeg.x, pitchDeg.y), droop * rng.NextFloat(0.6f, 1.3f), 0.12f);
                float yaw = yawDeg + 360f * i / leaves + rng.NextFloat(-18f, 18f);
                Quaternion turn = Quaternion.Euler(0f, yaw, rng.NextFloat(-12f, 12f));
                target.Append(card, Matrix4x4.TRS(basePoint + Vector3.up * rng.NextFloat(0f, 0.15f), turn, Vector3.one), painter);
            }
        }

        /// <summary>Upright stalks (bellflowers, blades): two crossed cards each.</summary>
        public static void Stalks(LookTestMeshAccumulator target, IRandom rng, Rect[] cells, Vector3 basePoint, int count, float spread, Vector2 height, LookTestMeshAccumulator.Painter painter)
        {
            for (int i = 0; i < count; i++)
            {
                Rect cell = cells[rng.NextInt(0, cells.Length)];
                float h = rng.NextFloat(height.x, height.y);
                Vector3 p = basePoint + new Vector3(rng.NextFloat(-1f, 1f) * spread, 0f, rng.NextFloat(-1f, 1f) * spread);
                float yaw = rng.NextFloat(0f, 180f);
                float lean = rng.NextFloat(70f, 88f);
                Mesh card = Card(cell, h, lean, 0.05f, 0f);
                target.Append(card, Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one), painter);
                target.Append(card, Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw + 90f, 0f), Vector3.one), painter);
            }
        }

        /// <summary>A palm-like crown: fronds radiating from a point, arching down.</summary>
        public static void PalmCrown(LookTestMeshAccumulator target, IRandom rng, Vector3 top, float size, LookTestMeshAccumulator.Painter painter)
        {
            int fronds = rng.NextInt(9, 13);
            for (int i = 0; i < fronds; i++)
            {
                Rect cell = Fronds[rng.NextInt(0, 2)];
                Mesh card = Card(cell, size * rng.NextFloat(0.8f, 1.15f), rng.NextFloat(5f, 40f), 0.55f, 0.15f, 1.4f);
                float yaw = 360f * i / fronds + rng.NextFloat(-15f, 15f);
                target.Append(card, Matrix4x4.TRS(top, Quaternion.Euler(0f, yaw, 0f), Vector3.one), painter);
            }
        }

        /// <summary>Painter for plants in the open: wind from the card (R), no canopy, AO kept.</summary>
        public static Color Open(Vector3 world, Vector3 local, Color source)
        {
            return new Color(source.r, 0f, 0f, source.a);
        }

        /// <summary>World positions of upward-facing vertices of <paramref name="mesh"/> under <paramref name="matrix"/>, thinned.</summary>
        public static List<Vector3> UpwardPoints(Mesh mesh, Matrix4x4 matrix, float minUp, int step)
        {
            var result = new List<Vector3>();
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < vertices.Length; i += Mathf.Max(1, step))
            {
                if (normals.Length > i && normalMatrix.MultiplyVector(normals[i]).normalized.y > minUp)
                {
                    result.Add(matrix.MultiplyPoint3x4(vertices[i]));
                }
            }

            return result;
        }
    }
}
