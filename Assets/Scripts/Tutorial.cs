// Tutorial.cs
// ---------------------------------------------------------------------------
// Shows each gameplay hint ONCE (for example the first time a runner appears
// or the first time you earn a lure bomb), in the hint box above the typed
// word:
//
//   Tutorial.Once("runner", "RUNNER! Fast zombie - shoot it first.");
//
// The list of hints already shown is static, so it survives a Restart (the
// scene reloads): players are not told the same thing twice.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class Tutorial
{
    private const float DefaultSeconds = 5f;

    private static readonly HashSet<string> shown = new HashSet<string>();

    // Shows text (for seconds) if the hint called key was never shown before.
    public static void Once(string key, string text, float seconds = DefaultSeconds)
    {
        if (shown.Contains(key))
        {
            return;
        }

        HUD hud = Object.FindFirstObjectByType<HUD>();
        if (hud == null)
        {
            return;
        }

        shown.Add(key);
        hud.ShowHint(text, seconds);

        AnalyticsEvent e = Analytics.NewEvent("hint");
        e.Add("key", key);
        Analytics.Send(e);
    }
}
