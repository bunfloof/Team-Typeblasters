// ShieldWord.cs
// ---------------------------------------------------------------------------
// The word that raises the SHIELD. Tab does not raise it straight away:
// Powers opens a ShieldWord (a box near the bottom of the screen, drawn by
// HUD, with a timer bar) and the player has to TYPE it, while the enemies
// keep coming. Typing it raises the shield (Powers.RaiseShield); running out
// of time or pressing Tab again closes it, and the shield charge is kept.
//
// It is an ordinary ITypingTarget, so TypingController types it like any
// word (WaveSpawner.GetTypingTargets lists ShieldWord.Current), with two
// differences: no bullets are fired at it (it is not in the world), and its
// word is never placed in the world by WaveSpawner.LayoutLabels.
// Not a MonoBehaviour: Powers creates and closes it.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;

public class ShieldWord : ITypingTarget
{
    // The open shield word, or null. Reset by Powers.Awake (statics survive a scene reload).
    public static ShieldWord Current { get; private set; }

    private readonly Powers powers;
    private readonly HUD hud;
    private readonly bool caseSensitive;

    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsAlive { get; private set; }

    private ShieldWord(string word, Powers owner, HUD hudRef)
    {
        Word = word;
        powers = owner;
        hud = hudRef;
        caseSensitive = TypingSettings.IsCaseSensitive;
        IsAlive = true;
    }

    // Opens the shield word box with this word (already styled for the difficulty).
    public static ShieldWord Open(string word, Powers owner, HUD hud)
    {
        Close();
        Current = new ShieldWord(word, owner, hud);
        hud.ShowShieldPrompt();
        Current.RefreshLabel();
        return Current;
    }

    // Closes the open shield word (if any) without raising the shield.
    public static void Close()
    {
        if (Current == null)
        {
            return;
        }
        Current.IsAlive = false;
        Current.hud.HideShieldPrompt();
        Current = null;
    }

    // Forgets the shield word of a previous scene (called by Powers.Awake).
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
        get { return caseSensitive; }
    }

    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return TypedCount >= Word.Length; }
    }

    // A point straight ahead of the camera: while the shield word is typed,
    // the view does not turn away from the enemies.
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
        get { return hud.ShieldPromptLabel; }
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

    // Called by TypingController as soon as the last letter is typed (no bullet).
    public void CompleteWord()
    {
        if (!IsAlive)
        {
            return;
        }
        Close();
        powers.RaiseShield();
    }

    // Typed letters in yellow, the rest in the shield's colour (set on the text).
    private void RefreshLabel()
    {
        string shown = TypingSettings.Shown(Word, caseSensitive);
        ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
        hud.SetShieldPromptWord(ColoredWord);
    }
}
