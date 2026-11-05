// HudReveal.cs
// ---------------------------------------------------------------------------
// While the guided first wave of the Normal level is running (GuidedIntro),
// the HUD starts EMPTY and each piece appears only when the intro introduces
// it (Reveal): health, score, wave, combo, energy, the four powers and the
// shield. A power can only be used once it has appeared. HUD, Powers and the
// touch buttons ask IsShown.
//
// Outside the guided intro (it was skipped or is over, other levels)
// everything is shown. Revealed fires once per feature so the HUD can make
// it pop in (HUD.RevealFeature).
// ---------------------------------------------------------------------------
using System;

public enum HudFeature
{
    Health,
    Score,
    Wave,
    Combo,
    Energy,
    Lure,
    Freeze,
    Frenzy,
    Rpg,
    Shield
}

public static class HudReveal
{
    private static readonly bool[] shown = new bool[Enum.GetValues(typeof(HudFeature)).Length];

    // A feature has just appeared (HUD makes it pop in).
    public static event Action<HudFeature> Revealed;

    // True while the HUD is being revealed piece by piece.
    public static bool Gated
    {
        get { return GuidedIntro.Running; }
    }

    public static bool IsShown(HudFeature feature)
    {
        return !Gated || shown[(int)feature];
    }

    // A new guided intro: everything hidden again.
    public static void HideAll()
    {
        Array.Clear(shown, 0, shown.Length);
    }

    public static void Reveal(HudFeature feature)
    {
        if (shown[(int)feature])
        {
            return;
        }
        shown[(int)feature] = true;
        Revealed?.Invoke(feature);
    }

    // May this power be used? ("shield", "lure", "freeze", "frenzy", "rpg", as Powers names them)
    public static bool PowerAllowed(string item)
    {
        switch (item)
        {
            case "shield": return IsShown(HudFeature.Shield);
            case "lure": return IsShown(HudFeature.Lure);
            case "freeze": return IsShown(HudFeature.Freeze);
            case "frenzy": return IsShown(HudFeature.Frenzy);
            case "rpg": return IsShown(HudFeature.Rpg);
            default: return true;
        }
    }
}
