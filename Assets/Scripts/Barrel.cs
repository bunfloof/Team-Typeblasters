// Barrel.cs
// ---------------------------------------------------------------------------
// An explosive barrel: a plain red cylinder standing in the level. While its
// area is being fought it is ACTIVE: it shows a word (orange) and its blast
// ring on the ground. Typing the word shoots it and it explodes, killing every
// zombie inside the ring, exactly like a red zombie. Tip for players: type all
// but the last letter, wait for zombies to walk into the ring, then finish.
//
// Explosions set barrels off too (a chain reaction), and a barrel next to the
// boss hurts it (BossDamage).
//
// How it is used:
//   Scene:       the Barrel prefab (Assets/Prefabs/Barrel), placed under a fight
//                (an Encounter): it belongs to that fight.
//   WaveSpawner: barrel.Activate(word, hud) when the player arrives in its area,
//                barrel.Deactivate() when the player leaves.
//   Bullet:      CompleteWord() when the player's last shot hits it.
//
// After it explodes the cylinder is gone, but the empty "Barrel" object stays
// with IsExploded = true, so lists that still hold it never point at a
// destroyed object.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;

public class Barrel : MonoBehaviour, ITypingTarget
{
    public const float BlastRadius = 4f;
    public const float BossDamage = 150f;  // damage to the boss if it stands in the blast

    private const float Height = 1f;       // metres (the prefab's cylinder is this tall)
    private const float LabelAboveTop = 0.35f;

    [SerializeField] private GameObject cylinder; // the red cylinder (it keeps its collider, so thrown bodies bounce off it)

    private bool isActive;                 // true between Activate and Deactivate
    private BlastRing ring;

    // True once it has exploded.
    public bool IsExploded { get; private set; }

    public int TypedCount { get; private set; }  // letters typed so far

    // Makes it typeable: shows its word and its blast ring.
    public void Activate(string word, HUD hud)
    {
        if (IsExploded)
        {
            return;
        }

        Word = word;
        TypedCount = 0;
        isActive = true;

        if (Label == null)
        {
            Label = hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred above the barrel
        }
        Label.color = Palette.WordBarrel;
        Label.gameObject.SetActive(true); // WaveSpawner.LayoutLabels places it and shows it
        RefreshLabel();

        if (ring == null)
        {
            ring = BlastRing.Create(transform, BlastRadius, Palette.BlastOrange);
        }
        ring.gameObject.SetActive(true);
    }

    // Not typeable any more (word and ring hidden). Called when the player leaves the area.
    public void Deactivate()
    {
        isActive = false;
        TypedCount = 0;
        if (Label != null)
        {
            Label.gameObject.SetActive(false);
        }
        if (ring != null)
        {
            ring.gameObject.SetActive(false);
        }
    }

    // Blows up now (shot, or set off by another explosion).
    // chain: the chain reaction it belongs to (null = start a new one).
    public void Detonate(ExplosionChain chain)
    {
        if (IsExploded)
        {
            return;
        }
        IsExploded = true;
        isActive = false;

        if (Label != null)
        {
            Destroy(Label.gameObject);
        }
        if (ring != null)
        {
            ring.gameObject.SetActive(true);
            ring.PlayBlastEffect(); // the ring lets go of the barrel, grows and disappears
            ring = null;
        }
        Destroy(cylinder);

        if (chain == null)
        {
            // Shot by the player: this barrel starts the chain reaction.
            chain = new ExplosionChain();
            chain.Report = Analytics.NewEvent("blast");
            chain.Report.Add("source", "barrel");
        }
        chain.NoteBarrel();
        chain.Begin(); // Explosion.Detonate calls the matching End()
        Explosion.Detonate(transform.position, BlastRadius, chain, BossDamage, null); // the chain has its report already
    }

    // Rebuilds the rich text: typed letters in yellow, the rest in orange.
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

    // Typeable only while its area is being fought and it has not exploded.
    public bool IsAlive
    {
        get { return isActive && !IsExploded; }
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
        get { return transform.position + Vector3.up * (Height * 0.5f); }
    }

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * (Height + LabelAboveTop); }
    }

    public void AdvanceProgress()
    {
        TypedCount += 1;
        RefreshLabel();
    }

    public void ResetProgress()
    {
        TypedCount = 0;
        if (!IsExploded)
        {
            RefreshLabel();
        }
    }

    // The player's last bullet hit it.
    public void CompleteWord()
    {
        Detonate(null);
    }
}
