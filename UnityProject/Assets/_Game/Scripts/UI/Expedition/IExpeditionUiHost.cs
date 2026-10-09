using JungleBooze.Gameplay.World;

namespace JungleBooze.UI.Expedition
{
    /// <summary>What the Expedition UI asks the App layer to do (implemented by ExpeditionRoot).</summary>
    public interface IExpeditionUiHost
    {
        /// <summary>Home → a new run (instant).</summary>
        void StartExpedition();

        /// <summary>Pause button / RESUME (resume runs the 1.0 s ready beat).</summary>
        void TogglePause();

        /// <summary>Pause or results → Home (a run in progress is abandoned).</summary>
        void ReturnToCamp();

        void RunAgain();

        /// <summary>Results objective card → its ability card.</summary>
        void OpenObjective();

        /// <summary>Abilities list → an ability card.</summary>
        void OpenAbility(AbilityFlags ability);

        /// <summary>LEARN on the open ability card.</summary>
        void LearnSelected();

        void CloseUpgrade();

        /// <summary>"Continue?" accepted.</summary>
        void Revive();

        void DeclineRevive();

        /// <summary>A setting changed (apply sensitivity, haptics, reduced motion, audio, layout).</summary>
        void SettingsChanged();
    }
}
