using System.Collections.Generic;

namespace JungleBooze.Core.Save
{
    /// <summary>The ordered migration chain. Append a step for every new version; never remove one.</summary>
    public static class SaveMigrations
    {
        public static IReadOnlyList<ISaveMigration> All { get; } = new ISaveMigration[]
        {
            new MigrateV1LaneGameToV2(),
        };

        /// <summary>Applies every step from data.version up to <see cref="SaveData.CurrentVersion"/>. Returns steps applied.</summary>
        public static int Upgrade(SaveData data)
        {
            int applied = 0;
            for (int guard = 0; guard < 64 && data.version < SaveData.CurrentVersion; guard++)
            {
                ISaveMigration step = null;
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].From == data.version)
                    {
                        step = All[i];
                        break;
                    }
                }

                if (step == null)
                {
                    break;
                }

                step.Apply(data);
                data.version = step.From + 1;
                applied++;
            }

            return applied;
        }
    }
}
