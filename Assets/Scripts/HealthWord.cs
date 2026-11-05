// HealthWord.cs
// ---------------------------------------------------------------------------
// Health is no longer a crate standing in the map: when the player is hurt,
// a small line appears under the health bar,
//
//      type  medkit  for health
//
// and typing that word gives SupplyCrate.HealAmount health back at once (no
// bullet, nothing to aim at). WaveSpawner offers one per wave (from wave 2,
// and only once the player has lost at least that much health); it goes away
// at the end of the wave if it was not used.
//
// It is an ordinary ITypingTarget, typed like any word, but an IScreenWord:
// the HUD draws it (HUD.SetHealthOffer), WaveSpawner never places it in the
// world and TypingController fires no bullet at it. Not a MonoBehaviour.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;

public class HealthWord : IScreenWord
{
    // The word on offer, or null. Reset by WaveSpawner.Awake (statics survive a scene reload).
    public static HealthWord Current { get; private set; }

    private readonly HUD hud;

    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsAlive { get; private set; }

    private HealthWord(string word, HUD hudRef)
    {
        Word = word.ToUpperInvariant();
        hud = hudRef;
        IsAlive = true;
    }

    // Puts the line under the health bar with this word.
    public static HealthWord Open(string word, HUD hud)
    {
        Close();
        Current = new HealthWord(word, hud);
        Current.RefreshLabel();
        hud.ShowHealthOffer(true);

        AnalyticsEvent e = Analytics.NewEvent("prop");
        e.Add("kind", "health_word");
        e.Add("word", Current.Word);
        Analytics.Send(e);
        return Current;
    }

    // Takes the line away without healing (the wave is over).
    public static void Close()
    {
        if (Current == null)
        {
            return;
        }
        Current.IsAlive = false;
        Current.hud.ShowHealthOffer(false);
        Current = null;
    }

    public static void Reset()
    {
        Current = null;
    }

    // ---- ITypingTarget ----

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return false; }
    }

    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return TypedCount >= Word.Length; }
    }

    // Straight ahead of the camera: typing it never turns the view away from the zombies.
    public Vector3 Position
    {
        get
        {
            Transform view = Camera.main != null ? Camera.main.transform : null;
            return view != null ? view.position + view.forward * 10f : Vector3.zero;
        }
    }

    public Vector3 HitPoint
    {
        get { return Position; }
    }

    public TMP_Text Label
    {
        get { return hud.HealthOfferLabel; }
    }

    public Vector3 LabelAnchor
    {
        get { return Position; }
    }

    public void AdvanceProgress()
    {
        TypedCount += 1;
        RefreshLabel();
    }

    public void ResetProgress()
    {
        TypedCount = 0;
        RefreshLabel();
    }

    // The last letter was typed: health at once.
    public void CompleteWord()
    {
        if (!IsAlive)
        {
            return;
        }
        GameManager game = GameManager.Instance;
        AnalyticsEvent e = Analytics.NewEvent("crate");
        e.Add("kind", "health");
        e.Add("word", Word);
        e.Add("health_before", game.Health);
        Close();
        game.Heal(SupplyCrate.HealAmount);
        hud.ShowHealthGain(SupplyCrate.HealAmount);
        e.Add("energy_gained", 0);
        Analytics.AddPlayerState(e);
        Analytics.Send(e);
    }

    // Typed letters dim, the rest in the health green.
    private void RefreshLabel()
    {
        string shown = Word.ToLowerInvariant();
        ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
        hud.SetHealthOffer(ColoredWord);
    }
}
