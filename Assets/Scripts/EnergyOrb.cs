// EnergyOrb.cs
// ---------------------------------------------------------------------------
// The boss's attack. When its body has turned fully red, the Boss fires an
// EnergyOrb (EnergyOrb.Launch) at the player. The orb flies for flightSeconds
// and carries its own word, like an enemy:
//   - type the word -> a Bullet hits the orb -> it bursts, the attack is
//     cancelled (and it counts as a kill for score and combo);
//   - too slow -> the orb reaches the player and deals its damage.
//
// Orb words are short but tricky: case-sensitive, with digits and symbols
// (WordBank.PickOrbWord), and drawn in pink like the orb (Palette.WordOrb),
// flashing with every throb of the orb: the word to type first.
// The freeze power stops it in mid-air, with an ice-blue outline.
// IsCaseSensitive = true tells TypingController to compare every character
// exactly. The orb itself is a pulsing pink sphere.
// ---------------------------------------------------------------------------
using System.Collections;
using TMPro;
using UnityEngine;

public class EnergyOrb : MonoBehaviour, ITypingTarget
{
    private const float Size = 0.9f;             // diameter in metres
    private const float PulseSpeed = 10f;        // how fast it throbs
    private const float PulseAmount = 0.12f;     // how much it throbs (fraction of its size)
    private const float HitPlayerDistance = 1.2f;
    private const float LabelAbove = 0.7f;       // the word sits this far above the orb's centre

    private static readonly Color OrbColor = new Color(1f, 0.15f, 0.55f);    // hot pink

    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsAlive { get; private set; }
    public TMP_Text Label { get; private set; }

    private float speed;  // metres per second
    private int damage;
    private HUD hud;
    private LineRenderer frozenOutline; // ice-blue ring around the orb while the freeze power is on

    private const float OutlineRadius = 0.7f;   // in the orb's own units (the orb is 1 across): just outside it
    private const float OutlineWidth = 0.08f;   // metres
    private const int OutlinePoints = 32;

    // Fires an orb from position at the player. It arrives after flightSeconds
    // unless the player types its word first.
    public static EnergyOrb Launch(Vector3 position, string word, float flightSeconds, int damage, HUD hud)
    {
        GameObject orbObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbObject.name = "EnergyOrb";
        Destroy(orbObject.GetComponent<Collider>()); // hits by distance, not physics
        orbObject.transform.position = position;
        orbObject.transform.localScale = Vector3.one * Size;
        orbObject.GetComponent<Renderer>().sharedMaterial = Palette.Lit(OrbColor);

        EnergyOrb orb = orbObject.AddComponent<EnergyOrb>();
        orb.Word = word;
        orb.damage = damage;
        orb.hud = hud;
        orb.IsAlive = true;
        orb.speed = Vector3.Distance(position, AimPoint()) / Mathf.Max(0.1f, flightSeconds);
        orb.launchTime = Time.time;

        orb.Label = hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred above the orb
        orb.Label.color = Palette.WordOrb; // pink like the orb, so it reads as "orb: type it exactly"
        orb.RefreshLabel();
        orb.BuildFrozenOutline();
        return orb;
    }

    // A circle (in its own X/Y plane) that is turned to face the camera every
    // frame, so it always looks like a blue outline around the orb. Hidden
    // unless the freeze power is on.
    private void BuildFrozenOutline()
    {
        GameObject outlineObject = new GameObject("FrozenOutline");
        outlineObject.transform.SetParent(transform, false);
        frozenOutline = outlineObject.AddComponent<LineRenderer>();
        frozenOutline.useWorldSpace = false;
        frozenOutline.loop = true;
        frozenOutline.positionCount = OutlinePoints;
        frozenOutline.startWidth = OutlineWidth;
        frozenOutline.endWidth = OutlineWidth;
        frozenOutline.sharedMaterial = Palette.Unlit(Palette.FrozenIce);
        frozenOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        frozenOutline.receiveShadows = false;
        for (int i = 0; i < OutlinePoints; i++)
        {
            float angle = i * 2f * Mathf.PI / OutlinePoints;
            frozenOutline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * OutlineRadius);
        }
        frozenOutline.enabled = false;
    }

    // Shows the outline while frozen and keeps it facing the camera.
    private void UpdateFrozenOutline(bool frozen)
    {
        frozenOutline.enabled = frozen;
        if (frozen && Camera.main != null)
        {
            frozenOutline.transform.rotation = Camera.main.transform.rotation;
        }
    }

    // The orb flies at the player's face, a little below the camera.
    private static Vector3 AimPoint()
    {
        return CameraDirector.PlayerEye + Vector3.down * 0.4f; // the player, not the camera (it is high up in the map view)
    }

    private void Update()
    {
        if (!IsAlive)
        {
            return;
        }
        // The frozen outline also shows (and faces the camera) while paused.
        UpdateFrozenOutline(Powers.IsFrozen);
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        // The freeze power stops it in mid-air (there is still time to type it):
        // it holds still, with an ice-blue outline, and stops throbbing (and flashing).
        if (Powers.IsFrozen)
        {
            return;
        }

        // Throb, so it reads as "dangerous energy". Its word flashes with every
        // throb (pink -> almost white), so it stands out as the word to type first.
        float wave = Mathf.Sin(Time.time * PulseSpeed); // -1 .. 1
        float pulse = 1f + wave * PulseAmount;
        transform.localScale = Vector3.one * Size * pulse;
        Label.color = Color.Lerp(Palette.WordOrb, Palette.WordOrbFlash, (wave + 1f) * 0.5f);

        Vector3 aim = AimPoint();
        transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);

        // Too late: it hits the player.
        if (Vector3.Distance(transform.position, aim) <= HitPlayerDistance)
        {
            AnalyticsEvent hit = Analytics.NewEvent("damage");
            hit.Add("source", "orb");
            hit.Add("attacker_word", Word);
            hit.Add("attacker_word_length", Word.Length);
            hit.Add("attacker_letters_typed", TypedCount);
            hit.Add("attacker_seconds_alive", Time.time - launchTime);
            GameManager.Instance.TakeDamage(damage, false, hit, null);
            hud.FlashRed();
            LogEnd("hit_player");
            Vanish();
            Destroy(gameObject);
        }
    }

    // ---- Analytics: one "orb" event per orb, when it is gone ----

    private float launchTime; // game time it was fired

    // outcome: "shot" (its word was typed), "hit_player" or "boss_died".
    private void LogEnd(string outcome)
    {
        AnalyticsEvent e = Analytics.NewEvent("orb");
        e.Add("outcome", outcome);
        e.Add("word", Word);
        e.Add("word_length", Word.Length);
        e.Add("letters_typed", TypedCount);
        e.Add("seconds_alive", Time.time - launchTime);
        Analytics.Send(e);
    }

    // ---- ITypingTarget ----

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return true; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public Vector3 HitPoint
    {
        get { return transform.position; }
    }

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * LabelAbove; }
    }

    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return TypedCount >= Word.Length; }
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

    // The player's bullet hit it: the attack is cancelled.
    public void CompleteWord()
    {
        if (!IsAlive)
        {
            return;
        }
        GameManager.Instance.AddKill();
        LogEnd("shot");
        Vanish();
        StartCoroutine(Burst());
    }

    // Called by the Boss when it dies: orbs still in the air fizzle out.
    public void Dissipate()
    {
        if (!IsAlive)
        {
            return;
        }
        LogEnd("boss_died");
        Vanish();
        StartCoroutine(Burst());
    }

    // Stops it counting as a target and removes its word from the HUD.
    private void Vanish()
    {
        IsAlive = false;
        Destroy(Label.gameObject);
    }

    // A quick "pop": swells up and shrinks away, then removes the orb.
    private IEnumerator Burst()
    {
        const float duration = 0.2f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float progress = time / duration;
            float scale = Size * Mathf.Lerp(1.6f, 0f, progress * progress);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void RefreshLabel()
    {
        ColoredWord = Zombie.TypedColorTag + Word.Substring(0, TypedCount) + "</color>" + Word.Substring(TypedCount);
        Label.text = ColoredWord;
    }
}
