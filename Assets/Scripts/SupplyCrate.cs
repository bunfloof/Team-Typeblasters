// SupplyCrate.cs
// ---------------------------------------------------------------------------
// A supply crate: a plain coloured cube standing in the level. While its area
// is being fought it shows a word (green). Typing the word shoots it open and
// gives its reward (the word itself hints at what is inside):
//   Health - white cube:  +HealAmount health
//   Lure   - orange cube: +1 lure bomb (press 1 to throw)
//   Freeze - blue cube:   +1 freeze    (press 2 to use)
// Grabbing one costs time while zombies keep walking: a small gamble.
//
// How it is used:
//   Scene:       a crate prefab (Assets/Prefabs: SupplyCrate is the health
//                crate, LureCrate and FreezeCrate are variants of it), placed
//                under a fight (an Encounter): it belongs to that fight.
//   WaveSpawner: crate.Activate(word, hud) when the player arrives in its area,
//                crate.Deactivate() when the player leaves.
//   Bullet:      CompleteWord() when the player's last shot hits it.
//
// After it is opened the cube is gone, but the empty "SupplyCrate" object
// stays with IsOpened = true, so lists that still hold it never point at a
// destroyed object.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;

public enum SupplyKind
{
    Health,
    Lure,
    Freeze
}

public class SupplyCrate : MonoBehaviour, ITypingTarget
{
    public const int HealAmount = 30;

    private const float Size = 0.8f;            // metres (the prefab's cube is this big)
    private const float LabelAboveTop = 0.35f;

    [SerializeField] private SupplyKind kind = SupplyKind.Health; // what is inside (the cube's colour shows it)
    [SerializeField] private GameObject cube;                      // the coloured cube (it keeps its collider)

    private bool isActive;                      // true between Activate and Deactivate
    private HUD hud;

    public SupplyKind Kind
    {
        get { return kind; }
    }

    // True once it has been opened.
    public bool IsOpened { get; private set; }

    public int TypedCount { get; private set; }  // letters typed so far

    // Makes it typeable (shows its word).
    public void Activate(string word, HUD hudRef)
    {
        if (IsOpened)
        {
            return;
        }

        hud = hudRef;
        Word = word;
        TypedCount = 0;
        isActive = true;

        if (Label == null)
        {
            Label = hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred above the crate
        }
        Label.color = Palette.WordCrate;
        Label.gameObject.SetActive(true); // WaveSpawner.LayoutLabels places it and shows it
        RefreshLabel();
    }

    // Not typeable any more. Called when the player leaves the area.
    public void Deactivate()
    {
        isActive = false;
        TypedCount = 0;
        if (Label != null)
        {
            Label.gameObject.SetActive(false);
        }
    }

    // The player's bullet hit it: give the reward and remove the cube.
    private void Open()
    {
        IsOpened = true;
        isActive = false;
        if (Label != null)
        {
            Destroy(Label.gameObject);
        }
        Destroy(cube);

        // Analytics: what the crate gave (health is capped at full, a power at 3).
        AnalyticsEvent e = Analytics.NewEvent("crate");
        e.Add("kind", Kind.ToString().ToLowerInvariant());
        e.Add("word", Word);
        e.Add("health_before", GameManager.Instance.Health);

        // A power that is already full gives nothing, so it shows no "+1" either.
        string text = null;
        if (Kind == SupplyKind.Health)
        {
            GameManager.Instance.Heal(HealAmount);
            text = "+" + HealAmount + " HEALTH";
        }
        else if (Kind == SupplyKind.Lure)
        {
            if (GameManager.Instance.Powers.AddCharge(PowerKind.Lure))
            {
                text = "+1 LURE BOMB";
            }
        }
        else
        {
            if (GameManager.Instance.Powers.AddCharge(PowerKind.Freeze))
            {
                text = "+1 FREEZE";
            }
        }
        if (text != null)
        {
            hud.ShowFloatingText(transform.position + Vector3.up, text, Palette.WordCrate);
        }

        e.Add("power_gained", Kind != SupplyKind.Health && text != null); // false: that power was full
        Analytics.AddPlayerState(e); // health after, charges after...
        Analytics.Send(e);
    }

    // Rebuilds the rich text: typed letters in yellow, the rest in green.
    // Word is stored UPPERCASE but shown in lowercase.
    private void RefreshLabel()
    {
        string shown = Word.ToLowerInvariant();
        ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
        Label.text = ColoredWord;
    }

    // ---- ITypingTarget ----

    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public TMP_Text Label { get; private set; }

    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return Word != null && TypedCount >= Word.Length; }
    }

    // Typeable only while its area is being fought and it has not been opened.
    public bool IsAlive
    {
        get { return isActive && !IsOpened; }
    }

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return false; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public Vector3 HitPoint
    {
        get { return transform.position + Vector3.up * (Size * 0.5f); }
    }

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * (Size + LabelAboveTop); }
    }

    public void AdvanceProgress()
    {
        TypedCount += 1;
        RefreshLabel();
    }

    public void ResetProgress()
    {
        TypedCount = 0;
        if (!IsOpened)
        {
            RefreshLabel();
        }
    }

    // The player's last bullet hit it.
    public void CompleteWord()
    {
        if (IsAlive)
        {
            Open();
        }
    }
}
