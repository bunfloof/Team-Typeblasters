// EasyShield.cs
// ---------------------------------------------------------------------------
// EASY SCENE ONLY.
//
// Implements a one-use shield that the player raises by typing a word.
// Press SPACE to start, provided no zombie word is currently being typed.
// While entering the shield word, normal typing combat is temporarily
// disabled; wrong keys flash red but do not cost combo. ESC, Backspace,
// or the time limit cancels the attempt.
//
// When raised, a blue ring surrounds the player. The first zombie that
// enters its radius is knocked away using Zombie.KillByBlast: the shield
// blocks that attack without awarding a kill score or triggering an
// explosive chain reaction. The shield is consumed and recharges when
// the wave changes. An unused shield stays active.
//
// Uses public members of WaveSpawner, Zombie, TypingController, HUD,
// GameManager, CameraDirector, and BlastRing. No shared gameplay scripts
// need to be modified for the shield's core behavior.
//
// Runs at execution order -30 so it checks for incoming zombies before
// they move and before TypingController processes input.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-30)]
public class EasyShield : MonoBehaviour
{
    private enum ShieldState
    {
        Ready,       // can be raised (press Space)
        Typing,      // the word is being typed
        Armed,       // up: waits for the first zombie
        Recharging   // used: comes back with the next wave
    }

    [Header("Shield")]
    [SerializeField] private string shieldWord = "SHIELD"; // UPPERCASE A-Z only
    [SerializeField] private float shieldRadius = 2f;      // metres around the player (zombies bite at 1.5 m)
    [SerializeField] private float typeSeconds = 8f;       // time to type the word before it is cancelled

    [Header("Look")]
    [SerializeField] private Color shieldColor = new Color(0.3f, 0.7f, 1f); // light blue (not used by any enemy)
    [SerializeField] private float statusMarginRight = 40f;   // HUD units from the right edge
    [SerializeField] private float statusFromBottom = 260f;   // HUD units from the bottom edge
    [SerializeField] private float promptHeight = 0.3f;       // the typing prompt: fraction of the screen height from the bottom
    [SerializeField] private ShieldScreenEffect screenEffect;

    [Header("References (all found automatically if left empty)")]
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private TypingController typing;
    [SerializeField] private Transform player;

    private ShieldState state = ShieldState.Ready;
    private string word;                // shieldWord, validated
    private int typedCount;
    private float typeTimer;
    private int usedInWave;             // the wave the shield was used in (it comes back when the wave changes)
    private bool tipRequested;
    private bool reenableTyping;        // switch TypingController back on at the start of next frame (see Update)

    private BlastRing ring;             // the blue ring while the shield is up
    private TMP_Text statusLabel;       // bottom-right: "[SPACE] SHIELD ready" ...
    private TMP_Text promptLabel;       // above the bottom of the screen while typing

#if ENABLE_INPUT_SYSTEM
    private Keyboard keyboard;          // the keyboard we listen to while the word is typed
    private string pendingText = "";    // characters typed since the last Update

    private void OnTextInput(char character)
    {
        pendingText += character;
    }
#endif

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<WaveSpawner>();
        }
        if (typing == null)
        {
            typing = FindFirstObjectByType<TypingController>();
        }
        if (player == null)
        {
            RailMover rail = FindFirstObjectByType<RailMover>(); // it lives on the Player object
            if (rail != null)
            {
                player = rail.transform;
            }
        }
        if (screenEffect == null)
        {
            screenEffect = GetComponent<ShieldScreenEffect>();
        }

        // The word must be UPPERCASE letters A-Z (typing compares in uppercase).
        word = string.IsNullOrEmpty(shieldWord) ? "" : shieldWord.ToUpperInvariant();
        bool valid = word.Length > 0;
        foreach (char c in word)
        {
            if (c < 'A' || c > 'Z')
            {
                valid = false;
            }
        }
        if (!valid)
        {
            Debug.LogError("EasyShield: Shield Word must be letters A-Z only. Using SHIELD.");
            word = "SHIELD";
        }
    }

    private void OnDisable()
    {
        StopListening();
        if (typing != null)
        {
            typing.enabled = true; // never leave the guns off
        }
        if (ring != null)
        {
            Destroy(ring.gameObject);
            ring = null;
        }
        if (screenEffect != null)
        {
            screenEffect.SetShieldActive(false);
        }
    }

    private void Update()
    {
        if (spawner == null || spawner.Hud == null || player == null || typing == null)
        {
            return;
        }

        // The guns come back one frame AFTER a shield-typing ends, so the key that
        // ended it (Esc, Backspace) is never also read by TypingController (an Esc
        // would pause the game).
        if (reenableTyping)
        {
            typing.enabled = true;
            reenableTyping = false;
        }

        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        EnsureLabels();

        if (!playing)
        {
            // Paused, a tip box, or the game is over: stop typing (a tip box needs
            // TypingController for its Enter key).
            if (state == ShieldState.Typing)
            {
                CancelTyping(null);
            }
            UpdateLabels(false);
            return;
        }

        if (!tipRequested)
        {
            tipRequested = true;
            RequestTip();
        }

        // A used shield comes back with the next wave.
        if (state == ShieldState.Recharging && spawner.CurrentWave != usedInWave)
        {
            state = ShieldState.Ready;
            spawner.Hud.ShowHint("Shield ready! Press SPACE, then type " + word.ToLowerInvariant() + ".", 3f);
        }

        bool spacePressed;
        bool escapePressed;
        bool backspacePressed;
        string typed;
        ReadKeys(out spacePressed, out escapePressed, out backspacePressed, out typed);

        switch (state)
        {
            case ShieldState.Ready:
                if (spacePressed)
                {
                    TryStartTyping();
                }
                break;

            case ShieldState.Typing:
                UpdateTyping(escapePressed, backspacePressed, typed);
                break;

            case ShieldState.Armed:
                if (spacePressed)
                {
                    spawner.Hud.ShowHint("The shield is already up.", 1.5f);
                }
                CheckBubble();
                break;

            case ShieldState.Recharging:
                if (spacePressed)
                {
                    spawner.Hud.ShowHint("Shield used. It comes back next wave.", 2f);
                }
                break;
        }

        UpdateLabels(true);
    }

    // ---- Raising the shield ----

    private void TryStartTyping()
    {
        // A half-typed word would be lost: ask the player to finish it first.
        if (typing.IsTypingWord)
        {
            spawner.Hud.ShowHint("Finish your word first (or Backspace), then SPACE for the shield.", 2.5f);
            return;
        }

        state = ShieldState.Typing;
        typedCount = 0;
        typeTimer = typeSeconds;
        reenableTyping = false;
        typing.enabled = false; // the letters are for the shield now, not for the guns
        StartListening();
    }

    private void UpdateTyping(bool escapePressed, bool backspacePressed, string typed)
    {
        if (escapePressed || backspacePressed)
        {
            CancelTyping("Shield cancelled.");
            return;
        }

        typeTimer -= Time.deltaTime;
        if (typeTimer <= 0f)
        {
            CancelTyping("Too slow: shield cancelled.");
            return;
        }

        foreach (char character in typed)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                continue;
            }
            char upper = char.ToUpperInvariant(character);
            if (upper < 'A' || upper > 'Z')
            {
                continue;
            }

            if (upper == word[typedCount])
            {
                typedCount += 1;
                if (typedCount >= word.Length)
                {
                    RaiseShield();
                    return;
                }
            }
            else
            {
                spawner.Hud.FlashRed(); // only a flash: no combo loss in Easy
            }
        }
    }

    private void CancelTyping(string message)
    {
        state = ShieldState.Ready;
        typedCount = 0;
        StopListening();
        reenableTyping = true;
        if (!string.IsNullOrEmpty(message))
        {
            spawner.Hud.ShowHint(message, 2f);
        }
    }

    private void RaiseShield()
    {
        state = ShieldState.Armed;
        typedCount = 0;
        StopListening();
        reenableTyping = true;

        if (screenEffect != null)
        {
            screenEffect.SetShieldActive(true);
        }

        // The blue ring lies on the ground around the player and moves with it.
        ring = BlastRing.Create(player, shieldRadius, shieldColor);
        spawner.Hud.ShowBigMessage("SHIELD UP!", "blocks the next zombie that reaches you");
    }

    // ---- The shield at work ----

    // The first zombie inside the ring is knocked away: no bite, no damage, no combo reset.
    private void CheckBubble()
    {
        List<Zombie> alive = spawner.AliveZombies;
        for (int i = 0; i < alive.Count; i++)
        {
            Zombie zombie = alive[i];
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }
            if (FlatDistance(zombie.Position, player.position) <= shieldRadius)
            {
                Block(zombie);
                return; // the list changed (the zombie removed itself): stop looping
            }
        }
    }

    private void Block(Zombie zombie)
    {
        Vector3 at = zombie.LabelAnchor;
        zombie.KillByBlast(player.position); // thrown away from the player; no score, and a red zombie does NOT explode

        spawner.Hud.ShowFloatingText(at, "BLOCKED!", shieldColor);
        CameraDirector.Shake(0.25f);

        if (ring != null)
        {
            ring.PlayBlastEffect(); // the ring grows and fades away by itself
            ring = null;
        }
        if (screenEffect != null)
        {
            screenEffect.Pulse();
            screenEffect.SetShieldActive(false);
        }
        state = ShieldState.Recharging;
        usedInWave = spawner.CurrentWave;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---- First-time tip box (the game waits for Enter / OK, like the other tips) ----

    private void RequestTip()
    {
        GameManager.Instance.RequestTip("easy shield", "SHIELD",
            "A SHIELD protects you from ONE zombie.\n\n"
            + "Press SPACE, then type the word <b>" + word.ToLowerInvariant() + "</b>.\n"
            + "A blue ring on the ground shows the shield. it can protect you \n"
            + "from one zombie attack. You can activate shield once per wave.",
            shieldColor);
    }

    // ---- Input ----

    private void StartListening()
    {
#if ENABLE_INPUT_SYSTEM
        StopListening();
        keyboard = Keyboard.current;
        pendingText = "";
        if (keyboard != null)
        {
            keyboard.onTextInput += OnTextInput;
        }
#endif
    }

    private void StopListening()
    {
#if ENABLE_INPUT_SYSTEM
        if (keyboard != null)
        {
            keyboard.onTextInput -= OnTextInput;
            keyboard = null;
        }
        pendingText = "";
#endif
    }

    private void ReadKeys(out bool space, out bool escape, out bool backspace, out string typed)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard current = Keyboard.current;
        space = current != null && current.spaceKey.wasPressedThisFrame;
        escape = current != null && current.escapeKey.wasPressedThisFrame;
        backspace = current != null && current.backspaceKey.wasPressedThisFrame;
        typed = pendingText; // only filled while the word is being typed (see StartListening)
        pendingText = "";
#else
        space = Input.GetKeyDown(KeyCode.Space);
        escape = Input.GetKeyDown(KeyCode.Escape);
        backspace = Input.GetKeyDown(KeyCode.Backspace);
        typed = Input.inputString;
#endif
    }

    // ---- On-screen text (HUD word labels, so they get the game's outlined font) ----

    private void EnsureLabels()
    {
        if (statusLabel != null)
        {
            return;
        }
        HUD hud = spawner.Hud;
        statusLabel = hud.CreateWordLabel(new Vector2(1f, 0.5f)); // pivot on its right end: pinned to the right edge
        statusLabel.fontSize = 32f;
        promptLabel = hud.CreateWordLabel(new Vector2(0.5f, 0.5f));
        promptLabel.fontSize = 56f;
    }

    private void UpdateLabels(bool playing)
    {
        if (statusLabel == null)
        {
            return;
        }

        Rect screen = spawner.Hud.WordLayer.rect;
        string colorTag = "<color=#" + ColorUtility.ToHtmlStringRGB(shieldColor) + ">";
        string status = "";
        if (playing)
        {
            switch (state)
            {
                case ShieldState.Ready:
                    status = colorTag + "[SPACE]</color> SHIELD ready";
                    break;
                case ShieldState.Armed:
                    status = colorTag + "SHIELD UP</color>";
                    break;
                case ShieldState.Recharging:
                    status = "<alpha=#88>shield: next wave";
                    break;
            }
        }
        SetLabel(statusLabel, status, new Vector2(screen.xMax - statusMarginRight, screen.yMin + statusFromBottom));

        string prompt = "";
        if (playing && state == ShieldState.Typing)
        {
            string shown = word.ToLowerInvariant();
            prompt = "<size=50%>type to raise the shield   " + Mathf.CeilToInt(typeTimer) + "s   (Esc = cancel)</size>\n"
                + Zombie.TypedColorTag + shown.Substring(0, typedCount) + "</color>" + shown.Substring(typedCount);
        }
        SetLabel(promptLabel, prompt, new Vector2(0f, screen.yMin + screen.height * promptHeight));
    }

    // Shows text at a position inside the word layer (empty text hides the label).
    private static void SetLabel(TMP_Text label, string text, Vector2 position)
    {
        if (string.IsNullOrEmpty(text))
        {
            label.enabled = false;
            return;
        }
        label.text = text;
        label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, label.preferredHeight);
        label.rectTransform.anchoredPosition = position;
        label.enabled = true;
    }
}
