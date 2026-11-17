using UnityEngine;

// Shared timing for the energy bar, desktop skill rows and touch buttons.
// An energy pulse leads a short sweep across the usable tools every six seconds.
public static class EnergyUseReminder
{
    private static GameManager owner;
    private static float fullSince;
    private static bool wasFull;

    public static float Pulse(int toolIndex = -1)
    {
        GameManager game = GameManager.Instance;
        if (owner != game)
        {
            owner = game;
            wasFull = false;
        }
        bool allowed = game != null && game.State == GameState.Playing
            && game.SkillEnergy >= GameManager.MaxSkillEnergy
            && HudReveal.IsShown(HudFeature.Energy)
            && !GuidedIntro.Holding && !GuidedIntro.BlocksLetters
            && (EasyTutorial.Active == null || EasyTutorial.Active.IsFinalWave);
        if (!allowed)
        {
            wasFull = false;
            return 0f;
        }
        if (!wasFull)
        {
            fullSince = Time.unscaledTime;
            wasFull = true;
        }
        float phase = (Time.unscaledTime - fullSince) % 6f;
        if (toolIndex >= 0)
        {
            Powers powers = game.Powers;
            if (powers == null || toolIndex > 3) return 0f;
            string item = toolIndex == 0 ? "lure" : toolIndex == 1 ? "freeze" : toolIndex == 2 ? "frenzy" : "rpg";
            if (!HudReveal.PowerAllowed(item)) return 0f;
            bool active = toolIndex == 0 ? powers.IsAiming : toolIndex == 1 ? Powers.IsFrozen
                : toolIndex == 2 ? Powers.IsFrenzy : powers.RocketLoaded;
            if (active) return 0f;
            phase -= 0.35f + toolIndex * 0.18f;
        }
        float duration = toolIndex < 0 ? 1.1f : 0.75f;
        return phase <= 0f || phase >= duration ? 0f : Mathf.Sin(phase / duration * Mathf.PI);
    }
}
