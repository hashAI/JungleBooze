using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Pooled instances of the obstacle art models that exist in the project (spec 005 15.2). Instances are made once
    /// at setup, inactive; a rig borrows them when it is bound (<see cref="Take"/>) and returns them when it is
    /// unbound (<see cref="Release"/>). A missing model costs nothing: <see cref="Take"/> returns null and the caller
    /// builds the procedural gray-box piece. No allocation after construction.
    /// </summary>
    public sealed class ObstacleArtPool
    {
        private readonly Transform[][] _instances;
        private readonly bool[][] _taken;

        /// <param name="parent">Holder transform the idle instances live under.</param>
        /// <param name="slots">The slots this pool serves.</param>
        /// <param name="rigFactor">How many rigs may hold a slot's full instance count at once.</param>
        public ObstacleArtPool(Transform parent, ObstacleArtSlot[] slots, int rigFactor)
        {
            int count = ObstacleArtSlots.Count;
            _instances = new Transform[count][];
            _taken = new bool[count][];
            for (int s = 0; s < slots.Length; s++)
            {
                ObstacleArtSlot slot = slots[s];
                if (!ObstacleArtSlots.Exists(slot))
                {
                    continue;
                }

                int n = ObstacleArtSlots.InstancesOf(slot) * rigFactor;
                var list = new Transform[n];
                int made = 0;
                for (int i = 0; i < n; i++)
                {
                    Transform art = EnvironmentArt.Attach(parent, ObstacleArtSlots.NameOf(slot));
                    if (art == null)
                    {
                        break;
                    }

                    art.gameObject.SetActive(false);
                    list[made++] = art;
                }

                _instances[(int)slot] = list;
                _taken[(int)slot] = new bool[n];
            }
        }

        /// <summary>True when at least one instance of <paramref name="slot"/> was made.</summary>
        public bool Has(ObstacleArtSlot slot)
        {
            Transform[] list = _instances[(int)slot];
            return list != null && list.Length > 0 && list[0] != null;
        }

        /// <summary>
        /// Borrows an instance for <paramref name="rig"/>, parents it under <paramref name="parent"/> at the given
        /// local pose and turns it on. Null when the model does not exist or every instance is in use.
        /// </summary>
        public Transform Take(
            ObstacleRig rig, ObstacleArtSlot slot, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            int index = (int)slot;
            Transform[] list = _instances[index];
            if (list == null || rig.ArtTakenCount >= rig.ArtTaken.Length)
            {
                return null;
            }

            bool[] taken = _taken[index];
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null || taken[i])
                {
                    continue;
                }

                taken[i] = true;
                Transform art = list[i];
                art.SetParent(parent, false);
                art.localPosition = position;
                art.localRotation = rotation;
                art.localScale = scale;
                art.gameObject.SetActive(true);
                rig.ArtTaken[rig.ArtTakenCount] = art;
                rig.ArtTakenSlot[rig.ArtTakenCount] = (index * 1000) + i;
                rig.ArtTakenCount++;
                return art;
            }

            return null;
        }

        /// <summary>Returns every instance <paramref name="rig"/> borrowed.</summary>
        public void Release(ObstacleRig rig)
        {
            for (int k = 0; k < rig.ArtTakenCount; k++)
            {
                int code = rig.ArtTakenSlot[k];
                int index = code / 1000;
                int i = code % 1000;
                Transform art = rig.ArtTaken[k];
                if (art != null)
                {
                    art.gameObject.SetActive(false);
                }

                if (_taken[index] != null && i < _taken[index].Length)
                {
                    _taken[index][i] = false;
                }

                rig.ArtTaken[k] = null;
            }

            rig.ArtTakenCount = 0;
        }
    }
}
