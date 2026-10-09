using JungleBooze.Gameplay.Expedition;
using JungleBooze.UI.Common;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// Localized text of the results' next objective (GDD §17) from its kind and numbers. Content names (ability
    /// names, hints) come from the expedition content. Falls back to the objective's own text.
    /// </summary>
    public static class ObjectiveText
    {
        public static string Title(StringTable s, NextObjective o, ExpeditionContent content)
        {
            if (o == null || o.Kind == ObjectiveKind.None)
            {
                return string.Empty;
            }

            AbilityDefinition a = content != null ? content.FindAbility(o.Ability) : null;
            switch (o.Kind)
            {
                case ObjectiveKind.AbilityReady when a != null:
                    return s.Format("objective.abilityReady", a.Name, NumberText.Group(o.Progress), NumberText.Group(o.Target));
                case ObjectiveKind.AbilityProgress when a != null:
                    return s.Format("objective.abilityProgress", a.Name, NumberText.Group(o.Progress), NumberText.Group(o.Target));
                case ObjectiveKind.NearBest:
                    return s.Format("objective.nearBest", Metres(s, o.Progress), Metres(s, o.Target));
                case ObjectiveKind.BeatBest:
                    return s.Format("objective.beatBest", Metres(s, o.Target));
                case ObjectiveKind.Secrets when o.Entry < 0 && o.Target > 0:
                    return s.Format("objective.secrets", o.Progress, o.Target);
                default:
                    return o.Title ?? string.Empty;
            }
        }

        public static string Detail(StringTable s, NextObjective o, ExpeditionContent content)
        {
            if (o == null)
            {
                return string.Empty;
            }

            AbilityDefinition a = content != null ? content.FindAbility(o.Ability) : null;
            switch (o.Kind)
            {
                case ObjectiveKind.AbilityReady when a != null:
                    return string.IsNullOrEmpty(a.ObjectiveLine) ? a.Line : a.ObjectiveLine;
                case ObjectiveKind.AbilityProgress when a != null:
                    return a.Line;
                case ObjectiveKind.NearBest:
                    return s.Get("objective.nearBestDetail");
                default:
                    return o.Detail ?? string.Empty;
            }
        }

        /// <summary>Title and detail on two lines (results card, tests).</summary>
        public static string Line(StringTable s, NextObjective o, ExpeditionContent content)
        {
            string title = Title(s, o, content);
            string detail = Detail(s, o, content);
            return string.IsNullOrEmpty(detail) ? title : title + "\n" + detail;
        }

        public static string Metres(StringTable s, int metres)
        {
            return s.Format("unit.metres", NumberText.Group(metres));
        }
    }
}
