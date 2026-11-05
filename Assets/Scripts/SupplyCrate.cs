// SupplyCrate.cs
// ---------------------------------------------------------------------------
// A supply crate: a plain coloured cube standing in the level. While its area
// is being fought it shows a word. Typing the word shoots it open and gives
// its reward (the word itself hints at what is inside):
//   Health - white cube, green word:  +HealAmount health
//   Energy - teal cube, teal word:    +EnergyAmount skill energy (the same
//            teal as the ENERGY number on the HUD, so it reads "energy")
// Grabbing one costs time while zombies keep walking: a small gamble.
//
// How it is used:
//   Scene:       a crate prefab (Assets/Prefabs: SupplyCrate is the health
//                crate; LureCrate and FreezeCrate are variants of it that are
//                now both ENERGY crates - they used to give a lure bomb or a
//                freeze, which made no sense once powers ran on energy), placed
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
    Energy
}

public class SupplyCrate : MonoBehaviour, ITypingTarget
{
    public const int HealAmount = 30;

    // Energy in an energy crate, always the same. 7: a level had two lure
    // crates (6 energy each) and two freeze crates (8 each), 28 energy in all;
    // four energy crates at 7 give the same 28, so the energy balance (typed
    // kills earn 1, cap 20, powers cost 6 / 8 / 12 / 18) does not change. One
    // crate is a bit more than a lure bomb, about a third of the cap.
    public const int EnergyAmount = 7;

    private const float Size = 0.8f;            // metres (the prefab's cube is this big)
    private const float LabelAboveTop = 0.35f;

    [SerializeField] private SupplyKind kind = SupplyKind.Health; // what is inside (the cube's colour shows it)
    [SerializeField] private GameObject cube;                      // the coloured cube (it keeps its collider)

    private bool isActive;                      // true between Activate and Deactivate
    private HUD hud;

    // Anything but a health crate is an ENERGY crate (old scenes saved lure
    // = 1 and freeze = 2 kinds: both are energy crates now).
    public SupplyKind Kind
    {
        get { return kind == SupplyKind.Health ? SupplyKind.Health : SupplyKind.Energy; }
    }

    // An energy crate is painted the energy teal (its prefab still carries the
    // old lure / freeze material).
    private void Awake()
    {
        if (Kind == SupplyKind.Energy && cube != null)
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                // Unlit: exactly the HUD's ENERGY teal whatever the light (the
                // forest's light made a lit cube look green beside the teal word).
                cubeRenderer.sharedMaterial = Palette.Unlit(Palette.Energy);
            }
        }
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
        Label.color = Kind == SupplyKind.Energy ? Palette.Energy : Palette.WordCrate;
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

        string text;
        int energyGained = 0;
        if (Kind == SupplyKind.Health)
        {
            GameManager.Instance.Heal(HealAmount);
            text = "+" + HealAmount + " HEALTH";
        }
        else
        {
            // Energy stops at the cap (GameManager.MaxSkillEnergy): say what really came in.
            int before = GameManager.Instance.SkillEnergy;
            GameManager.Instance.AddSkillEnergy(EnergyAmount);
            energyGained = GameManager.Instance.SkillEnergy - before;
            text = energyGained > 0 ? "+" + energyGained + " ENERGY" : "ENERGY FULL";
        }
        hud.ShowFloatingText(transform.position + Vector3.up, text,
            Kind == SupplyKind.Energy ? Palette.Energy : Palette.WordCrate);

        e.Add("energy_gained", energyGained); // 0 for health, or when the energy was already full
        Analytics.AddPlayerState(e); // health after, charges after...
        Analytics.Send(e);
    }

    // Rebuilds the rich text: typed letters dim, the rest in the crate's colour.
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
