// TypingController.cs
// ---------------------------------------------------------------------------
// The ONLY input in the game: reads the keyboard and turns letters into shots.
//
// Rules:
//   - Nothing typed yet: the typed letter picks EVERY target (zombie, boss
//     part, energy orb, barrel, supply crate, quiz answer - see ITypingTarget)
//     whose word starts with it: the CANDIDATES. Usually just one, because
//     new words avoid first letters already on screen. If nothing matches, it
//     is a wrong key.
//   - Then each letter keeps the candidates whose next letter it is (the
//     others drop out). If it matches none of them, it is a wrong key.
//   - WORD CHAIN: word families (hunt / hunter, go / god) share a start on
//     purpose. A word that is finished gets its killing shot while the longer
//     ones stay candidates, so typing "hunter" kills "hunt" on the way. Each
//     kill counts as usual (score, combo +1); 2+ words finished in one run
//     also give a WORD CHAIN bonus (GameManager.AddWordChain).
//   - The bottom word box shows every candidate (shortest first), so it
//     always shows what the keys are hitting.
//   - EVERY correct letter fires a bullet at the nearest candidate (it makes a
//     zombie stagger). A finishing letter fires the killing shot: when that
//     bullet hits, it calls CompleteWord() (a zombie dies or explodes...).
//   - TYPING DIFFICULTY (TypingSettings, picked on the Start panel): on Easy
//     words are stored UPPERCASE, shown in lowercase, and typing them is
//     case-insensitive. On Medium, Hard and Extreme every word is
//     case-sensitive and may contain punctuation (Hard) or digits and symbols
//     (Extreme), which must be typed exactly. Digits and symbols are ignored
//     while no case-sensitive word is on screen.
//   - SPACE only counts when the word being typed expects one (a BRUTE's
//     phrase, "cold dark night"); anywhere else it is ignored, never wrong.
//   - A wrong key never resets progress. The screen flashes red and the
//     combo drops by one.
//   - 1: TAP to throw a lure bomb, HOLD to aim it (a dashed arc) and release
//     to throw (see Powers); while aiming, the ARROW keys steer the throw.
//     1 is read as a key going down / up, not as a typed character.
//     2 uses the freeze power; 3 / 4 are the combo weapons (TAP = use a kept
//     FRENZY / RPG, HOLD = buy one;
//     see Powers). No word contains the keys 1-4, and arrow
//     keys type nothing, so they never clash with typing.
//   - Tab brings up the SHIELD WORD (see Powers, ShieldWord): typing it
//     raises the shield (no damage for a few seconds). It is typed like any
//     word, but no bullets fly: it lives on the HUD, not in the world.
//     Tab again closes it.
//   - Backspace drops the current target so the player can retarget.
//   - Escape pauses the game (and resumes it from the pause panel, see
//     GameManager.OnPauseKey). Keys typed while paused are ignored.
//   - Enter closes a first-time tip box, starts the game from the Start
//     panel, and restarts it from the "You survived" / "You died" panels.
//     On the Start panel, LEFT / RIGHT change the typing difficulty.
//     A tip box never opens in the middle of a word (see GameManager.LateUpdate).
//
// INPUT BACKEND: this project uses Unity's Input System package
// (Project Settings > Player > Active Input Handling), so typed characters come
// from Keyboard.current.onTextInput. If that setting is ever switched to the old
// Input Manager, the #else branches below use Input.inputString instead.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TypingController : MonoBehaviour
{
    [Header("References (wired in the scene)")]
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private Transform player;
    [SerializeField] private HUD hud;

    private const char LureKey = '1';
    private const char FreezeKey = '2';
    private const char FrenzyKey = '3';  // combo weapons (see Powers)
    private const char RocketKey = '4';

    // CANDIDATES: every target whose word matches everything typed so far.
    // Usually one (first letters on screen are kept different), but word
    // families share a start on purpose: with "hunt" and "hunter" on screen,
    // "h-u-n-t" matches both. Each key keeps the candidates it matches; a word
    // that is finished is shot while the longer ones stay candidates, so going
    // on to "hunter" kills both: a WORD CHAIN.
    private readonly List<ITypingTarget> candidates = new List<ITypingTarget>();
    private readonly List<ITypingTarget> scratch = new List<ITypingTarget>();
    private int wordsFinished; // words finished in this run of typing (2+ = a word chain)
    private float lastTypingTime = -100f; // Time.time the player last typed a letter (right or wrong) or dropped a word

    // The target being typed right now: the nearest candidate (null = none).
    // The camera turns toward it; the in-between shots go to it.
    public ITypingTarget CurrentTarget
    {
        get { return NearestCandidate(); }
    }

    // True while a word is half-typed. GameManager waits for the word to be
    // finished (or dropped) before it opens a tip box.
    public bool IsTypingWord
    {
        get { return candidates.Count > 0; }
    }

    // Game seconds since the player last typed a letter (right or wrong) or
    // dropped a word (they are about to type the next one).
    public float SecondsSinceTyping
    {
        get { return Time.time - lastTypingTime; }
    }

#if ENABLE_INPUT_SYSTEM
    private Keyboard keyboard;        // the keyboard we are listening to
    private string pendingText = "";  // characters typed since the last Update

    // The Input System calls this once for every character the player types.
    // We only store the character here; Update() handles it.
    private void OnTextInput(char character)
    {
        pendingText += character;
    }

    private void OnDisable()
    {
        if (keyboard != null)
        {
            keyboard.onTextInput -= OnTextInput;
            keyboard = null;
        }
    }
#endif

    private void Update()
    {
        // ---- 1. Read the keyboard ----
        string typedText;
        bool enterPressed;
        bool dropPressed;  // Backspace
        bool pausePressed; // Escape
        bool shieldPressed; // Tab
        int difficultyStep; // LEFT (-1) / RIGHT (+1) went down this frame (Start panel)
        bool lureDown;     // 1 went down this frame (start aiming)...
        bool lureUp;       // ...and came up (throw)
        bool frenzyDown, frenzyUp; // 3 went down / came up: TAP = use a FRENZY, HOLD = buy one (see Powers)
        bool rocketDown, rocketUp; // 4, the same for the RPG
        bool textModifier;
        Vector2 arrows;    // arrow keys held: x = Right - Left, y = Up - Down (steer the lure bomb)

#if ENABLE_INPUT_SYSTEM
        // Keyboard.current can be missing on the very first frames, or change if a
        // keyboard is plugged in, so make sure we listen to the current one.
        if (keyboard != Keyboard.current)
        {
            if (keyboard != null)
            {
                keyboard.onTextInput -= OnTextInput;
            }
            keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.onTextInput += OnTextInput;
            }
        }

        typedText = pendingText;
        textModifier = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
            || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        pendingText = "";
        enterPressed = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        dropPressed = keyboard != null && keyboard.backspaceKey.wasPressedThisFrame;
        pausePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        shieldPressed = keyboard != null && keyboard.tabKey.wasPressedThisFrame;
        difficultyStep = keyboard == null ? 0
            : (keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftArrowKey.wasPressedThisFrame ? 1 : 0);
        lureDown = keyboard != null && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame);
        lureUp = keyboard != null && (keyboard.digit1Key.wasReleasedThisFrame || keyboard.numpad1Key.wasReleasedThisFrame);
        frenzyDown = keyboard != null && (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame);
        frenzyUp = keyboard != null && (keyboard.digit3Key.wasReleasedThisFrame || keyboard.numpad3Key.wasReleasedThisFrame);
        rocketDown = keyboard != null && (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame);
        rocketUp = keyboard != null && (keyboard.digit4Key.wasReleasedThisFrame || keyboard.numpad4Key.wasReleasedThisFrame);
        arrows = Vector2.zero;
        if (keyboard != null)
        {
            arrows.x = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            arrows.y = (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f);
        }
#else
        typedText = Input.inputString;
        textModifier = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        dropPressed = Input.GetKeyDown(KeyCode.Backspace);
        pausePressed = Input.GetKeyDown(KeyCode.Escape);
        shieldPressed = Input.GetKeyDown(KeyCode.Tab);
        difficultyStep = (Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
        lureDown = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
        lureUp = Input.GetKeyUp(KeyCode.Alpha1) || Input.GetKeyUp(KeyCode.Keypad1);
        frenzyDown = Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
        frenzyUp = Input.GetKeyUp(KeyCode.Alpha3) || Input.GetKeyUp(KeyCode.Keypad3);
        rocketDown = Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
        rocketUp = Input.GetKeyUp(KeyCode.Alpha4) || Input.GetKeyUp(KeyCode.Keypad4);
        arrows = new Vector2(
            (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
            (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
#endif

        // ---- 2. Escape: pause / resume ----
        if (pausePressed)
        {
            GameManager.Instance.OnPauseKey();
        }

        // ---- 3. Outside of play, only Enter does something (and LEFT / RIGHT on the Start panel) ----
        if (GameManager.Instance.State != GameState.Playing)
        {
            if (difficultyStep != 0 && GameManager.Instance.State == GameState.Start)
            {
                GameManager.Instance.CycleTypingDifficulty(difficultyStep);
            }
            if (enterPressed)
            {
                GameManager.Instance.StartOrRestart();
            }
            return;
        }

        // ---- 4. Forget candidates that are gone (reached the player, got blown up...) ----
        if (candidates.Count > 0)
        {
            candidates.RemoveAll(candidate => !candidate.IsAlive);
            if (candidates.Count == 0)
            {
                EndRun();
            }
        }

        // ---- 5. Backspace: drop the word being typed ----
        if (dropPressed && candidates.Count > 0)
        {
            foreach (ITypingTarget candidate in candidates)
            {
                candidate.ResetProgress();
            }
            candidates.Clear();
            lastTypingTime = Time.time;
            EndRun();
        }

        // ---- 5a. Tab: raise the shield ----
        if (shieldPressed)
        {
            GameManager.Instance.Powers.TryUseShield();
        }

        // ---- 5b. 1: aim the lure bomb while held (arrow keys steer it), throw on release ----
        GameManager.Instance.Powers.SetAimInput(arrows);
        if (lureDown && !textModifier)
        {
            GameManager.Instance.Powers.BeginLureAim();
        }
        if (lureUp)
        {
            GameManager.Instance.Powers.ReleaseLureAim();
        }

        // ---- 5c. 3 / 4: TAP to use a kept combo weapon, HOLD to buy one (Powers) ----
        if (frenzyDown && !textModifier)
        {
            GameManager.Instance.Powers.WeaponKeyDown(false);
        }
        if (frenzyUp)
        {
            GameManager.Instance.Powers.WeaponKeyUp(false);
        }
        if (rocketDown && !textModifier)
        {
            GameManager.Instance.Powers.WeaponKeyDown(true);
        }
        if (rocketUp)
        {
            GameManager.Instance.Powers.WeaponKeyUp(true);
        }

        // ---- 6. Handle every character typed this frame ----
        foreach (char character in typedText)
        {
            if (character == ' ' && NextIsSpace())
            {
                HandleCharacter(character); // the space inside a phrase
                continue;
            }
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                continue; // Enter, Backspace, space, ...: not part of the word being typed
            }

            // The power keys. 1, 3 and 4 are handled above as keys held down;
            // their typed characters (repeated while a key is held) are skipped here.
            if (character == LureKey || character == FrenzyKey || character == RocketKey)
            {
                continue;
            }
            if (character == FreezeKey)
            {
                GameManager.Instance.Powers.TryUseFreeze();
                continue;
            }

            // Digits and symbols only matter while a case-sensitive word (any word
            // above Easy, or an energy orb) is around; otherwise they are ignored.
            char upper = char.ToUpperInvariant(character);
            bool isLetter = upper >= 'A' && upper <= 'Z';
            if (!isLetter && !SymbolsMatter())
            {
                continue;
            }
            HandleCharacter(character);
        }

        // ---- 7. Show the word being typed at the bottom of the screen ----
        hud.SetTargetWord(CandidatesText());
    }

    // The bottom word box. One candidate: its word. Several (a word family):
    // all of them side by side, shortest first, each with the typed part in
    // yellow, e.g. "hunt   hunter". So the box always shows exactly what the
    // keys are hitting, never one word while another one is being shot.
    private string CandidatesText()
    {
        if (candidates.Count == 0)
        {
            return "";
        }
        if (candidates.Count == 1)
        {
            return candidates[0].ColoredWord;
        }

        scratch.Clear();
        scratch.AddRange(candidates);
        scratch.Sort((a, b) => a.Word.Length.CompareTo(b.Word.Length));
        const int MaxShown = 3;
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        for (int i = 0; i < scratch.Count && i < MaxShown; i++)
        {
            if (i > 0)
            {
                text.Append("   ");
            }
            text.Append(scratch[i].ColoredWord);
        }
        if (scratch.Count > MaxShown)
        {
            text.Append("   ...");
        }
        return text.ToString();
    }

    // True if a word being typed has a space as its next character (a phrase).
    private bool NextIsSpace()
    {
        foreach (ITypingTarget candidate in candidates)
        {
            if (candidate.IsAlive && !candidate.IsWordComplete && candidate.NextLetter == ' ')
            {
                return true;
            }
        }
        return false;
    }

    // Does the typed character match the expected one? Case-sensitive targets
    // (energy orbs) need the exact character; the others accept either case.
    private static bool Matches(ITypingTarget candidate, char typed, char expected)
    {
        if (candidate.IsCaseSensitive)
        {
            return typed == expected;
        }
        return char.ToUpperInvariant(typed) == expected;
    }

    // True if digits/symbols should count as key presses right now: the current
    // target is case-sensitive, or (no target yet) some case-sensitive target exists.
    private bool SymbolsMatter()
    {
        if (candidates.Count > 0)
        {
            foreach (ITypingTarget candidate in candidates)
            {
                if (candidate.IsCaseSensitive)
                {
                    return true;
                }
            }
            return false;
        }
        foreach (ITypingTarget candidate in spawner.GetTypingTargets())
        {
            if (candidate.IsCaseSensitive)
            {
                return true;
            }
        }
        return false;
    }

    private void HandleCharacter(char typed)
    {
        lastTypingTime = Time.time;

        // Nothing being typed yet: this character picks every target whose word
        // starts with it (usually exactly one), and counts as their first character.
        if (candidates.Count == 0)
        {
            FindTargetsStartingWith(typed, candidates);
            if (candidates.Count == 0)
            {
                WrongKey();
                return;
            }
            wordsFinished = 0;
        }

        // Keep only the candidates whose next character is this one.
        scratch.Clear();
        foreach (ITypingTarget candidate in candidates)
        {
            if (Matches(candidate, typed, candidate.NextLetter))
            {
                scratch.Add(candidate);
            }
        }
        if (scratch.Count == 0)
        {
            WrongKey(); // matches none of them: nothing changes, the combo drops by one
            return;
        }

        // Candidates that do not match drop out (their typed letters go back to white).
        foreach (ITypingTarget candidate in candidates)
        {
            if (!scratch.Contains(candidate))
            {
                candidate.ResetProgress();
            }
        }
        candidates.Clear();
        candidates.AddRange(scratch);
        GameManager.Instance.OnCorrectKey();
        AdvanceCandidates();

        if (candidates.Count == 0)
        {
            EndRun();
        }
    }

    // Drops the word being typed, as Backspace does (its letters go back to
    // white). Called by Powers when a FRENZY swaps every enemy's word.
    public void DropWord()
    {
        if (candidates.Count > 0)
        {
            lastTypingTime = Time.time; // a word was half-typed: the player goes on with a new one
        }
        foreach (ITypingTarget candidate in candidates)
        {
            candidate.ResetProgress();
        }
        candidates.Clear();
        EndRun();
    }

    // Types one letter into every candidate: a shot at the nearest one, or the
    // killing shot at each word this letter finishes.
    private void AdvanceCandidates()
    {
        foreach (ITypingTarget candidate in candidates)
        {
            candidate.AdvanceProgress();
        }

        // Every word finished by this letter gets its killing shot (with "hunt"
        // and "hunter", the T finishes "hunt" and "hunter" stays a candidate).
        // A loaded RPG turns the first of those shots into a rocket.
        bool anyFinished = false;
        ITypingTarget lastFinished = null;
        foreach (ITypingTarget candidate in candidates)
        {
            if (candidate.IsWordComplete && candidate is ShieldWord)
            {
                candidate.CompleteWord(); // raises the shield at once: no bullet, no kill
                anyFinished = true;
                continue;
            }
            if (candidate.IsWordComplete)
            {
                bool rocket = GameManager.Instance.Powers.ConsumeRocket();
                Bullet.Fire(candidate, true, rocket);
                wordsFinished += 1;
                anyFinished = true;
                lastFinished = candidate;
            }
        }
        candidates.RemoveAll(candidate => candidate.IsWordComplete);

        // A word was finished but a longer one goes on: the chain is running.
        // Say so right where it happened, so the player keeps typing.
        if (lastFinished != null && candidates.Count > 0)
        {
            hud.ShowFloatingText(lastFinished.LabelAnchor, "CHAIN!  keep typing", Palette.WordChain);
        }

        // Otherwise a normal shot at the nearest candidate (it staggers).
        ITypingTarget nearest = NearestCandidate();
        if (!anyFinished && nearest != null && !(nearest is ShieldWord))
        {
            Bullet.Fire(nearest, false);
        }
    }


    // A run of typing is over (every candidate finished, dropped or gone). If
    // it finished 2 or more words (hunt, then hunter), it was a WORD CHAIN.
    private void EndRun()
    {
        if (wordsFinished >= 2)
        {
            GameManager.Instance.AddWordChain(wordsFinished);
        }
        wordsFinished = 0;
    }

    private void WrongKey()
    {
        // Progress is NOT reset; the player only loses the combo.
        GameManager.Instance.OnWrongKey();
        hud.FlashRed();
    }

    // Fills 'found' with every alive target whose word starts with the typed character.
    private void FindTargetsStartingWith(char typed, List<ITypingTarget> found)
    {
        found.Clear();
        foreach (ITypingTarget candidate in spawner.GetTypingTargets())
        {
            // Skip a target whose word is already done: a bullet is on its way to it.
            if (candidate.IsWordComplete || !Matches(candidate, typed, candidate.Word[0]))
            {
                continue;
            }
            found.Add(candidate);
        }
    }

    // The candidate closest to the player, or null if there is none.
    private ITypingTarget NearestCandidate()
    {
        ITypingTarget nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (ITypingTarget candidate in candidates)
        {
            if (!candidate.IsAlive)
            {
                continue;
            }
            float distance = Vector3.Distance(player.position, candidate.Position);
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }
        return nearest;
    }
}
