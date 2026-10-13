// HUD.cs
// ---------------------------------------------------------------------------
// Everything drawn on the screen-space Canvas:
//   - health bar (top-left)
//   - "Wave 2 / 5" and Score (top-right)
//   - the COMBO, big, on a dark plate at the top-centre (nothing covers it). Under
//     it: what the combo can buy (the COMBO WEAPONS status line + a thin bar)
//   - the powers (bottom-left, just above the height of the bottom word box so
//     they never overlap it): "[1] LURE BOMB" and "[2] FREEZE" with their charges,
//     and the combo weapons kept for later, "[3] FRENZY" and "[4] RPG": one
//     square per slot, lit while a weapon is kept, clickable to use it; a bar
//     under the slots fills while 3 / 4 is HELD to buy one
//   - while 1 is held (aiming the lure bomb): an arrow-keys hint in a dark box
//     (bottom-right); a "+1" next to a power whenever a charge is gained
//   - the first-time tip box (the game waits until Enter / OK)
//   - the current target's word (bottom-centre) and a hint box just above it
//   - red arrows at the screen edges pointing at zombies you cannot see
//   - floating texts ("+30 HEALTH") that rise from a point in the world
//   - the "Wave N" banner, the multi-kill popup and the red wrong-key flash
//   - the red boss health bar (top-centre) and the boss quiz box under it
//   - the enemies' words (CreateWordLabel). They are drawn on this Canvas, on
//     top of the 3D scene, so an enemy's body can never hide another's word.
//     WaveSpawner.LayoutLabels moves them to their enemies every frame.
//   - the Start panel (with its "Show tips" toggle), the "You survived" and
//     "You died" panels (with the run's stats at the end), the pause panel
//     and the 3-2-1 resume countdown
//
// The HUD never decides anything. Other scripts call these methods when a value
// changes (for example GameManager calls SetScore after a kill).
// The objects saved in the scene are wired in the scene (Prototype.unity);
// everything else is built here in code the first time it is needed (see the
// Build... methods).
//
// No image files: an Image component WITHOUT a sprite draws a plain rectangle
// in its colour, and every text is TextMeshPro with its default font.
//
// DRAWING ORDER. uGUI draws a Canvas's children in order: a later child is
// drawn on top of an earlier one. From back to front:
//   enemy words < readouts (health, score, powers, quiz...) < markers (threat
//   arrows, floating texts) < hint < red flash < big popup < Start/Won/Lost
//   panels < pause panel < countdown. The scene has one empty full-screen
//   "layer" object per group (Enemy Words, Readouts, Markers, Hint), so the
//   pieces made while playing land at the right depth.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Readouts")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text targetWordText;

    [Header("Wave banner and big popup")]
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private TMP_Text multiKillText;      // the big popup: "TRIPLE KILL!", "WORD CHAIN x2!", "FRENZY!"

    [Header("Wrong-key flash")]
    [SerializeField] private Image flashImage;           // full-screen red image, normally invisible
    [SerializeField] private float flashDuration = 0.15f; // seconds
    [SerializeField] private float flashMaxAlpha = 0.35f; // how strong the red is at the start of a flash

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject wonPanel;
    [SerializeField] private GameObject lostPanel;
    [SerializeField] private TMP_Text wonScoreText;
    [SerializeField] private TMP_Text lostScoreText;
    [SerializeField] private Toggle tipsToggle;           // "Show tips" on the Start panel
    [SerializeField] private GameObject pausePanel;       // Esc: its Resume and Restart buttons call GameManager
    [SerializeField] private TMP_Text countdownText;      // the 3-2-1 before play resumes

    // Empty full-screen objects in the scene (see DRAWING ORDER at the top).
    [Header("Drawing layers")]
    [SerializeField] private RectTransform wordLayer;     // enemy words: the first child (only the boss bar goes under it)
    [SerializeField] private RectTransform readoutLayer;  // powers, combo plate, quiz, aim hint
    [SerializeField] private RectTransform markerLayer;   // threat arrows, floating texts
    [SerializeField] private RectTransform hintLayer;     // hint box

    [Header("Boss health bar")]
    [SerializeField] private Vector2 bossBarSize = new Vector2(700f, 26f);
    // The bar sits under the combo plate (top-centre).
    private const float BossBarTop = 190f; // canvas units from the top of the screen (its title is above it)
    [SerializeField] private Color bossBarColor = new Color(0.85f, 0.1f, 0.1f);

    [Header("Enemy words")]
    [SerializeField] private float wordFontSize = 40f;   // size at scale 1 (see WaveSpawner.LayoutLabels)
    [SerializeField] private float wordOutlineWidth = 0.25f;

    [Header("Powers (bottom-left)")]
    [SerializeField] private float powersMargin = 40f;        // canvas units from the left and bottom edges
    [SerializeField] private float powerPulseSeconds = 0.4f;  // PulsePower: how long a row grows and flashes
    [SerializeField] private float powerPulseScale = 1.25f;   // PulsePower: row size at the top of the pulse
    [SerializeField] private float powerEmptyAlpha = 0.4f;    // a power with no charges is dimmed to this

    [Header("Threat arrows")]
    [SerializeField] private Color threatArrowColor = new Color(1f, 0.18f, 0.12f);  // red
    [SerializeField] private float threatArrowInset = 70f;    // canvas units between an arrow and the screen edge
    [SerializeField] private float threatNearDistance = 8f;   // metres: closer threats get a big, fully opaque arrow
    [SerializeField] private float threatFarDistance = 30f;   // metres: farther threats get a small, faint arrow

    [Header("Feedback")]
    [SerializeField] private float floatingTextSeconds = 1.1f;  // lifetime of a floating text
    [SerializeField] private float floatingTextRise = 70f;      // canvas units it rises during its life

    // Anchor and pivot points used by the code-built pieces (0,0 = bottom-left, 1,1 = top-right).
    private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
    private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);

    private static readonly Color Yellow = new Color(0.95f, 0.75f, 0.1f); // hint strip, reward bar

    private float flashTimer; // counts down from flashDuration to 0 while a flash is fading

    private Material wordMaterial;    // the font with a black outline, shared by every outlined text

    private GameObject bossBar;       // null until the first boss fight
    private RectTransform bossBarFill;
    private TMP_Text bossBarTitle;

    private void Awake()
    {
        // The red flash is invisible until the first wrong key. Switching it off
        // saves drawing an invisible full-screen rectangle every frame.
        if (flashImage != null)
        {
            SetFlashAlpha(0f);
        }
    }

    private void Update()
    {
        UpdateMultiKill();
        UpdateRedFlash();
        UpdatePowerPulses();
        UpdateComboRewardBar();
        UpdateComboLook();
        UpdateHint();
        UpdateQuizPop();
    }

    private void LateUpdate()
    {
        // After every Update, so the camera has already moved this frame and the
        // texts sit exactly on their world points.
        UpdateFloatingTexts();
    }

    // ---- Readouts ----

    public void SetHealth(int health, int maxHealth)
    {
        healthBar.maxValue = maxHealth;
        healthBar.value = health;
    }

    public void SetWave(int wave, int waveCount)
    {
        waveText.text = "Wave " + wave + " / " + waveCount;
    }

    public void SetScore(int score)
    {
        scoreText.text = "Score " + score;
    }

    // ---- Combo (top-centre, big) ----
    // "COMBO x12" in big letters on a dark plate at the top-centre, drawn over
    // everything in the game (the readout layer is above the enemy words). It
    // pops when the combo goes up, shakes red when it goes down, and changes
    // colour once it can buy a weapon. The weapon status line and its bar sit
    // just under it on the same plate (SetWeaponStatus).

    private const float ComboPlateTop = 6f;          // canvas units below the top edge
    private static readonly Vector2 ComboPlateSize = new Vector2(560f, 128f);
    private const float ComboTextTop = 10f;
    private const float ComboFontSize = 64f;
    private const float ComboPopScale = 1.35f;
    private const float ComboPopSeconds = 0.25f;
    private const float ComboShakeSeconds = 0.3f;
    private const float ComboShakeDistance = 10f;

    private RectTransform comboPlate;   // null until the first SetCombo
    private int shownCombo = 1;
    private float comboPopTimer;        // counts down while the number pops (combo up)
    private float comboShakeTimer;      // counts down while it shakes red (combo down)

    public void SetCombo(int combo)
    {
        if (comboPlate == null)
        {
            BuildComboPlate();
        }
        if (combo > shownCombo)
        {
            comboPopTimer = ComboPopSeconds;
        }
        else if (combo < shownCombo)
        {
            comboShakeTimer = ComboShakeSeconds;
        }
        shownCombo = combo;
        comboText.text = "<size=50%>COMBO</size> x" + combo;
        UpdateComboLook();
    }

    // The combo's colour: white, then the FRENZY red once it can buy a frenzy,
    // then the RPG orange once it can buy an RPG.
    private Color ComboColor()
    {
        if (shownCombo >= Powers.RocketCost)
        {
            return Palette.BlastOrange;
        }
        if (shownCombo >= Powers.FrenzyCost)
        {
            return Palette.WeaponFrenzy;
        }
        return Color.white;
    }

    private void UpdateComboLook()
    {
        if (comboPlate == null)
        {
            return;
        }
        comboPopTimer = Mathf.Max(0f, comboPopTimer - Time.unscaledDeltaTime);
        comboShakeTimer = Mathf.Max(0f, comboShakeTimer - Time.unscaledDeltaTime);

        float pop = comboPopTimer / ComboPopSeconds;              // 1 -> 0
        float scale = Mathf.Lerp(1f, ComboPopScale, pop * pop);
        comboText.rectTransform.localScale = new Vector3(scale, scale, 1f);

        float shake = comboShakeTimer / ComboShakeSeconds;        // 1 -> 0
        float offset = Mathf.Sin(comboShakeTimer * 70f) * ComboShakeDistance * shake;
        comboText.rectTransform.anchoredPosition = new Vector2(offset, -ComboTextTop);

        Color rest = ComboColor();
        comboText.color = Color.Lerp(rest, new Color(1f, 0.15f, 0.1f), shake);

        PlaceComboPlate();
    }

    private const float ComboSideGap = 24f;       // at least this much space beside the health bar and the Wave / Score texts
    private const float ComboMinScale = 0.55f;

    private const float HealthBarMinWidth = 140f; // the health bar is never shortened below this

    private float healthBarFullWidth = -1f;       // the bar's width as saved in the scene (read on first use)

    // The plate always stays centred. On a narrower window, where it would
    // reach the health bar (top-left), the health bar gets SHORTER instead
    // (never below HealthBarMinWidth). Only if that is still not enough, or
    // the plate would reach the Wave / Score texts (top-right), does the plate
    // get smaller (still centred): it never covers them.
    private void PlaceComboPlate()
    {
        float width = readoutLayer.rect.width;
        float centre = width * 0.5f;

        // Room on the right: up to the Wave / Score texts.
        float rightTexts = Mathf.Max(waveText != null ? waveText.preferredWidth : 0f,
            scoreText != null ? scoreText.preferredWidth : 0f);
        float right = width - (RightTextMargin + rightTexts + ComboSideGap);
        float halfRoom = right - centre;

        // Room on the left: up to the health bar at its SHORTEST.
        RectTransform bar = healthBar != null ? healthBar.transform as RectTransform : null;
        bool barOnLeft = bar != null && bar.anchorMin.x == 0f && bar.anchorMax.x == 0f; // anchored to the left edge
        float barLeft = 0f;
        if (barOnLeft)
        {
            if (healthBarFullWidth < 0f)
            {
                healthBarFullWidth = bar.sizeDelta.x;
            }
            barLeft = bar.anchoredPosition.x - bar.sizeDelta.x * bar.pivot.x;
            float shortestRight = barLeft + Mathf.Min(HealthBarMinWidth, healthBarFullWidth);
            halfRoom = Mathf.Min(halfRoom, centre - (shortestRight + ComboSideGap));
        }

        float scale = Mathf.Clamp(halfRoom * 2f / ComboPlateSize.x, ComboMinScale, 1f);
        comboPlate.localScale = new Vector3(scale, scale, 1f);
        comboPlate.anchoredPosition = new Vector2(0f, -ComboPlateTop);

        // The health bar ends just before the plate (or keeps its full length).
        if (barOnLeft)
        {
            float plateLeft = centre - ComboPlateSize.x * scale * 0.5f;
            float barWidth = Mathf.Clamp(plateLeft - ComboSideGap - barLeft,
                Mathf.Min(HealthBarMinWidth, healthBarFullWidth), healthBarFullWidth);
            if (!Mathf.Approximately(bar.sizeDelta.x, barWidth))
            {
                bar.sizeDelta = new Vector2(barWidth, bar.sizeDelta.y);
                // Keep the bar's left end where it is, whatever its pivot.
                bar.anchoredPosition = new Vector2(barLeft + barWidth * bar.pivot.x, bar.anchoredPosition.y);
            }
        }
    }

    private const float RightTextMargin = 40f;    // the Wave / Score texts end this far from the right edge

    // Moves the scene's Combo text (it used to sit top-right) onto a dark plate
    // at the top-centre, in the readout layer so enemy words never cover it.
    private void BuildComboPlate()
    {
        Image plate = CreateImage("ComboPlate", readoutLayer, new Color(0f, 0f, 0f, 0.55f));
        comboPlate = plate.rectTransform;
        PlaceRect(comboPlate, TopCentre, TopCentre, new Vector2(0f, -ComboPlateTop), ComboPlateSize);

        comboText.rectTransform.SetParent(comboPlate, false);
        PlaceRect(comboText.rectTransform, TopCentre, TopCentre, new Vector2(0f, -ComboTextTop),
            new Vector2(ComboPlateSize.x, ComboFontSize * 1.15f));
        comboText.enableAutoSizing = false;
        comboText.fontSize = ComboFontSize;
        comboText.fontStyle = FontStyles.Bold;
        comboText.alignment = TextAlignmentOptions.Center;
        comboText.textWrappingMode = TextWrappingModes.NoWrap;
        comboText.fontSharedMaterial = OutlineMaterial(comboText);
    }

    // richText is the word with TextMeshPro colour tags (see Zombie.ColoredWord),
    // or "" when there is no target.
    public void SetTargetWord(string richText)
    {
        targetWordText.text = richText;
    }

    // ---- Banner ----

    public void ShowBanner(string message)
    {
        bannerText.text = message;
        bannerText.gameObject.SetActive(true);
    }

    public void HideBanner()
    {
        bannerText.gameObject.SetActive(false);
    }

    // ---- Wrong-key flash ----

    public void FlashRed()
    {
        if (flashDuration <= 0f)
        {
            return; // a duration of 0 switches the flash off (it could never fade out)
        }

        flashTimer = flashDuration;
        SetFlashAlpha(flashMaxAlpha);
    }

    private void UpdateRedFlash()
    {
        if (flashTimer <= 0f)
        {
            return;
        }

        // Fade the red from flashMaxAlpha down to 0.
        flashTimer -= Time.unscaledDeltaTime; // unscaled: keeps fading while the game is paused
        float alpha = flashMaxAlpha * Mathf.Clamp01(flashTimer / flashDuration);
        SetFlashAlpha(alpha);
    }

    private void SetFlashAlpha(float alpha)
    {
        Color color = flashImage.color;
        color.a = alpha;
        flashImage.color = color;
        flashImage.enabled = alpha > 0f; // nothing to draw when it is fully transparent
    }

    // ---- Multi-kill popup ----
    // "TRIPLE KILL!" with the points under it, above the middle of the screen.
    // It pops in big, settles to normal size, stays a moment, then fades out.

    private const float MultiKillPopSeconds = 0.15f;  // shrinking from big to normal size
    private const float MultiKillHoldSeconds = 0.9f;  // fully visible
    private const float MultiKillFadeSeconds = 0.4f;  // fading out
    private const float MultiKillPopScale = 1.6f;

    private float multiKillAge = float.MaxValue;

    // A WORD CHAIN (typing "hunter" also killed "hunt"): the same big popup as a
    // multi-kill, e.g. "WORD CHAIN x2!" with the bonus points under it.
    public void ShowWordChain(int words, int bonusPoints)
    {
        ShowBigPopup("WORD CHAIN x" + words + "!", "+" + bonusPoints + " BONUS");
    }

    public void ShowMultiKill(int kills, int points)
    {
        string title;
        if (kills == 2)
        {
            title = "DOUBLE KILL!";
        }
        else if (kills == 3)
        {
            title = "TRIPLE KILL!";
        }
        else if (kills == 4)
        {
            title = "QUAD KILL!";
        }
        else
        {
            title = "MASSACRE!";
        }

        ShowBigPopup(title, "x" + kills + " SCORE  +" + points);
    }

    // Any big announcement ("FRENZY!", "RPG LOADED!"), in the multi-kill popup.
    public void ShowBigMessage(string title, string subtitle)
    {
        ShowBigPopup(title, subtitle);
    }

    // The big popup above the middle of the screen: a title, and a smaller line under it.
    private void ShowBigPopup(string title, string subtitle)
    {
        multiKillText.text = title + "\n<size=55%>" + subtitle + "</size>";
        multiKillText.gameObject.SetActive(true);
        multiKillAge = 0f;
        UpdateMultiKill();
    }

    private void UpdateMultiKill()
    {
        if (!multiKillText.gameObject.activeSelf)
        {
            return;
        }

        float total = MultiKillPopSeconds + MultiKillHoldSeconds + MultiKillFadeSeconds;
        if (multiKillAge >= total)
        {
            multiKillText.gameObject.SetActive(false);
            return;
        }

        // Scale: big -> normal during the pop.
        float pop = Mathf.Clamp01(multiKillAge / MultiKillPopSeconds);
        float scale = Mathf.Lerp(MultiKillPopScale, 1f, pop);
        multiKillText.transform.localScale = new Vector3(scale, scale, 1f);

        // Alpha: 1, then down to 0 during the fade.
        float fadeStart = MultiKillPopSeconds + MultiKillHoldSeconds;
        multiKillText.alpha = 1f - Mathf.Clamp01((multiKillAge - fadeStart) / MultiKillFadeSeconds);

        multiKillAge += Time.deltaTime; // game time: the popup freezes while paused
    }

    // ---- Panels ----

    public void ShowStartPanel()
    {
        HideAllPanels();
        BuildLevelSelection();
        startPanel.SetActive(true);
    }

    // Ticks or unticks "Show tips" without calling its OnValueChanged (GameManager
    // already knows the value; it just has to be shown again after a Restart).
    public void SetTipsToggle(bool on)
    {
        tipsToggle.SetIsOnWithoutNotify(on);
    }

    public void ShowWonPanel(int finalScore)
    {
        HideAllPanels();
        SetTargetWord(""); // typing is over, so clear the word at the bottom
        wonScoreText.text = "Final score: " + finalScore;
        BuildLevelCompletion();
        wonPanel.SetActive(true);
    }

    private bool levelSelectionBuilt;
    private bool levelCompletionBuilt;

    private void BuildLevelSelection()
    {
        if (levelSelectionBuilt) return;
        levelSelectionBuilt = true;
        CreateText(startPanel.transform, "Level Select Label", "SELECT LEVEL", 28f, new Vector2(0f, -390f));
        for (int i = 0; i < GameManager.LevelCount; i++)
        {
            int levelIndex = i;
            string title = (i + 1) + ". " + GameManager.LevelName(i);
            TMP_Text label = CreateButton(startPanel.transform, "Select " + GameManager.LevelName(i),
                title, new Vector2((i - 1) * 320f, -460f), () => GameManager.Instance.SelectLevel(levelIndex));
            label.fontSize = 32f;
            RectTransform rect = (RectTransform)label.transform.parent;
            rect.sizeDelta = new Vector2(290f, 80f);
            label.rectTransform.sizeDelta = rect.sizeDelta;
            if (i == GameManager.CurrentLevelIndex)
            {
                rect.GetComponent<Image>().color = new Color(0.12f, 0.4f, 0.5f);
                label.text = title + " <size=65%>(current)</size>";
            }
        }
    }

    private void BuildLevelCompletion()
    {
        if (levelCompletionBuilt) return;
        levelCompletionBuilt = true;
        // Reuse the scene's existing replay button and keyboard hint.
        Button replay = wonPanel.GetComponentInChildren<Button>(true);
        RectTransform replayRect = replay.GetComponent<RectTransform>();
        PlaceRect(replayRect, Centre, Centre, new Vector2(-200f, -230f), new Vector2(360f, 100f));
        TMP_Text replayLabel = replay.GetComponentInChildren<TMP_Text>(true);
        replayLabel.text = "REPLAY LEVEL";
        replayLabel.fontSize = 34f;
        string next = GameManager.NextLevelName;
        TMP_Text nextLabel = CreateButton(wonPanel.transform, "Next Level",
            next != null ? "NEXT: " + next : "LEVEL SELECT", new Vector2(200f, -230f),
            () => { if (GameManager.NextLevelName != null) GameManager.Instance.NextLevel();
                else GameManager.Instance.SelectLevel(0); });
        nextLabel.fontSize = 34f;
        RectTransform nextRect = (RectTransform)nextLabel.transform.parent;
        nextRect.sizeDelta = new Vector2(360f, 100f);
        nextLabel.rectTransform.sizeDelta = nextRect.sizeDelta;
        foreach (TMP_Text label in wonPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.text == "(or press Enter)") label.text = "Enter: replay this level";
        }
        if (next == null)
        {
            CreateText(wonPanel.transform, "Campaign Complete", "Final level complete!", 32f, new Vector2(0f, -390f));
        }
    }

    public void ShowLostPanel(int finalScore)
    {
        HideAllPanels();
        SetTargetWord("");
        lostScoreText.text = "Final score: " + finalScore;
        lostPanel.SetActive(true);
    }

    public void HideAllPanels()
    {
        startPanel.SetActive(false);
        wonPanel.SetActive(false);
        lostPanel.SetActive(false);
        HideBanner();
        HideBossBar();
        HidePausePanel();
        HideCountdown();
        HidePowerTip();
        HideQuiz();
        multiKillText.gameObject.SetActive(false);
    }

    // ---- Boss health bar (top-centre) ----

    public void ShowBossBar(string title)
    {
        if (bossBar == null)
        {
            BuildBossBar();
        }
        bossBarTitle.text = title;
        SetBossHealth(1f, 1f);
        bossBar.SetActive(true);
    }

    public void SetBossHealth(float health, float maxHealth)
    {
        if (bossBar == null)
        {
            return;
        }
        // The fill is anchored to the left edge; its right anchor is the health fraction.
        float fraction = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        bossBarFill.anchorMax = new Vector2(fraction, 1f);
    }

    public void HideBossBar()
    {
        if (bossBar != null)
        {
            bossBar.SetActive(false);
        }
    }

    // Creates the bar under this Canvas the first time it is needed:
    //   BossBar (dark background) > Fill (red) + Title (text above the bar).
    // An Image without a sprite simply draws a solid rectangle in its colour.
    private void BuildBossBar()
    {
        bossBar = new GameObject("BossBar", typeof(RectTransform), typeof(Image));
        RectTransform barRect = bossBar.GetComponent<RectTransform>();
        barRect.SetParent(transform, false);
        barRect.SetAsFirstSibling(); // drawn under the panels and the red flash
        barRect.anchorMin = new Vector2(0.5f, 1f);
        barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -BossBarTop);
        barRect.sizeDelta = bossBarSize;
        bossBar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        bossBarFill = fill.GetComponent<RectTransform>();
        bossBarFill.SetParent(barRect, false);
        bossBarFill.anchorMin = Vector2.zero;
        bossBarFill.anchorMax = Vector2.one;
        bossBarFill.offsetMin = new Vector2(3f, 3f);   // small dark border around the red
        bossBarFill.offsetMax = new Vector2(-3f, -3f);
        fill.GetComponent<Image>().color = bossBarColor;

        GameObject title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.SetParent(barRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 4f);
        titleRect.sizeDelta = new Vector2(0f, 40f);
        bossBarTitle = title.GetComponent<TextMeshProUGUI>();
        bossBarTitle.fontSize = 32;
        bossBarTitle.alignment = TextAlignmentOptions.Center;
        bossBarTitle.color = Color.white;
    }

    // ---- Enemy words ----

    // Creates a word for an enemy (a zombie or a boss part). pivot says which point
    // of the word sits on the enemy's label anchor: (0.5, 0) = centred above it,
    // (1, 0.5) = to its left, (0, 0.5) = to its right.
    // The word starts hidden; WaveSpawner.LayoutLabels shows it once it has
    // placed it (on its enemy, or just inside the screen edge when the enemy is
    // off to a side), so a word never flashes up anywhere else first.
    // The owner destroys the word's GameObject when the enemy goes away.
    public TMP_Text CreateWordLabel(Vector2 pivot)
    {
        GameObject word = new GameObject("Word", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = word.GetComponent<RectTransform>();
        rect.SetParent(wordLayer, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;

        TextMeshProUGUI label = word.GetComponent<TextMeshProUGUI>();
        label.fontSize = wordFontSize;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (pivot.x < 0.25f)
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }
        else if (pivot.x > 0.75f)
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }
        else
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        }

        // A black outline keeps the white word readable over any background.
        label.fontSharedMaterial = OutlineMaterial(label);

        label.enabled = false;
        return label;
    }

    // The RectTransform the words live in (WaveSpawner converts screen points into it).
    public RectTransform WordLayer
    {
        get { return wordLayer; }
    }

    // The font with a black outline. One material shared by every outlined text
    // (a material per text would be wasteful): enemy words, floating texts,
    // and the power names.
    private Material OutlineMaterial(TMP_Text sample)
    {
        if (wordMaterial == null)
        {
            wordMaterial = new Material(sample.fontSharedMaterial);
            wordMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            wordMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, wordOutlineWidth);
            wordMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        }
        return wordMaterial;
    }

    // ---- Pause ----

    public void ShowPausePanel()
    {
        pausePanel.SetActive(true);
    }

    public void HidePausePanel()
    {
        pausePanel.SetActive(false);
    }

    // Big number in the middle of the screen while the game is about to resume.
    public void ShowCountdown(string text)
    {
        countdownText.text = text;
        countdownText.gameObject.SetActive(true);
    }

    public void HideCountdown()
    {
        countdownText.gameObject.SetActive(false);
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, Vector2 position)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1200f, fontSize * 1.4f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    // A grey button with a text on it. Returns the text, so it can be changed later.
    private TMP_Text CreateButton(Transform parent, string objectName, string text, Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, 80f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.25f, 0.25f, 0.28f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image; // darkens when hovered / pressed
        button.onClick.AddListener(onClick);

        // The keyboard is for typing only: the arrow keys must not move a
        // "selection" from button to button (same as the scene's buttons).
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        TMP_Text label = CreateText(rect, "Text", text, 40f, Vector2.zero);
        label.rectTransform.sizeDelta = rect.sizeDelta;
        return label;
    }

    // ---- Powers (bottom-left) ----
    // Four rows, keys 1 to 4 from top to bottom. Row 0 = lure bomb, row 1 =
    // freeze (the same numbers as PowerKind, so (int)kind gives the row):
    //   [1] LURE BOMB  [#][#][ ]
    // a white key cap with the key to press, the power's name, then one small
    // square ("pip") per possible charge, filled in the power's colour.
    // Rows 2 and 3 are the COMBO WEAPONS the player has bought and kept for later:
    //   [3] FRENZY     [ ] [ ]
    //   [4] RPG        [ ] [ ]
    // one plain square per slot, filled in the weapon's colour while one is kept.
    // These two rows can also be CLICKED to use the weapon (same as tapping its key);
    // a thin bar under the slots fills while the key is HELD to buy one.

    private const int PowerCount = 4;
    private const int FrenzyRow = 2;
    private const int RocketRow = 3;
    private const float WeaponSlotSize = 44f;   // dark square of one weapon slot
    private const float WeaponSquareSize = 32f; // the square inside it (grey, or lit while a weapon is kept)
    private const float PowerRowHeight = 44f;
    private const float PowerRowGap = 10f;
    private const float PowerKeySize = 40f;
    private const float PowerNameX = 54f;       // the name starts this far right of the row's left end
    private const float PowerNameWidth = 190f;
    private const float PowerPipsX = 250f;      // the pips start this far right of the row's left end
    private const float PowerPipSize = 24f;
    private const float PowerPipGap = 6f;
    private const float PowerPipPadding = 4f;   // dark border around the pips
    private static readonly Color EmptyPipColor = new Color(0.3f, 0.31f, 0.35f);

    private RectTransform powersPanel;          // null until the first SetPowers / PulsePower
    private readonly RectTransform[] powerRows = new RectTransform[PowerCount];
    private readonly CanvasGroup[] powerRowGroups = new CanvasGroup[PowerCount]; // dims a whole row at once
    private readonly Image[] powerKeyCaps = new Image[PowerCount];
    private readonly TMP_Text[] powerNames = new TMP_Text[PowerCount];
    private readonly RectTransform[] powerPipStrips = new RectTransform[PowerCount]; // dark strip behind the pips
    private readonly List<Image> lurePips = new List<Image>();
    private readonly List<Image> freezePips = new List<Image>();
    private readonly int[] powerCharges = new int[PowerCount];
    private readonly float[] powerPulseTimers = new float[PowerCount]; // counts down while a row pulses
    private readonly Image[,] weaponSquares = new Image[PowerCount, Powers.MaxKept]; // only the weapon rows have them

    public void SetPowers(int lureCharges, int freezeCharges, int maxCharges)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }

        powerCharges[(int)PowerKind.Lure] = lureCharges;
        powerCharges[(int)PowerKind.Freeze] = freezeCharges;
        for (int row = 0; row < FrenzyRow; row++)
        {
            ShowPips(row, maxCharges);
            if (powerPulseTimers[row] <= 0f)
            {
                ApplyPowerRowLook(row, 0f, 0f); // a pulsing row is updated by UpdatePowerPulses
            }
        }
    }

    // The combo weapons kept for later (Powers): one square per slot, lit for each weapon kept.
    public void SetWeapons(int frenzyKept, int rocketKept)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }

        powerCharges[FrenzyRow] = frenzyKept;
        powerCharges[RocketRow] = rocketKept;
        for (int row = FrenzyRow; row <= RocketRow; row++)
        {
            for (int slot = 0; slot < Powers.MaxKept; slot++)
            {
                weaponSquares[row, slot].color = slot < powerCharges[row] ? PowerColor(row) : EmptyPipColor;
            }
            if (powerPulseTimers[row] <= 0f)
            {
                ApplyPowerRowLook(row, 0f, 0f);
            }
        }
    }

    // Makes one power slot flash and grow for a moment (a charge was just earned or used).
    public void PulsePower(PowerKind kind)
    {
        PulseRow((int)kind);
    }

    // The same for a combo weapon's slot (rocket = the RPG, otherwise the FRENZY).
    public void PulseWeapon(bool rocket)
    {
        PulseRow(rocket ? RocketRow : FrenzyRow);
    }

    private void PulseRow(int row)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }

        powerPulseTimers[row] = Mathf.Max(0.01f, powerPulseSeconds);
        powerRows[row].SetAsLastSibling(); // while it is big, it is drawn over the other rows
    }

    private void UpdatePowerPulses()
    {
        if (powersPanel == null)
        {
            return;
        }

        for (int row = 0; row < PowerCount; row++)
        {
            if (powerPulseTimers[row] <= 0f)
            {
                continue;
            }

            powerPulseTimers[row] -= Time.unscaledDeltaTime; // unscaled: also pulses in the pause
            if (powerPulseTimers[row] <= 0f)
            {
                ApplyPowerRowLook(row, 0f, 0f); // back to normal
                continue;
            }

            // progress goes 0 -> 1. The size goes up and back down (half a sine
            // wave); the colour starts at full strength and fades back to white.
            float progress = 1f - powerPulseTimers[row] / Mathf.Max(0.01f, powerPulseSeconds);
            ApplyPowerRowLook(row, Mathf.Sin(progress * Mathf.PI), 1f - progress);
        }
    }

    // grow: 0 = normal size .. 1 = powerPulseScale.
    // flash: 0 = normal colours .. 1 = key cap and name in the power's colour.
    private void ApplyPowerRowLook(int row, float grow, float flash)
    {
        float scale = Mathf.Lerp(1f, powerPulseScale, grow);
        powerRows[row].localScale = new Vector3(scale, scale, 1f);

        Color color = PowerColor(row);
        powerKeyCaps[row].color = Color.Lerp(Color.white, color, flash);
        powerNames[row].color = Color.Lerp(Color.white, color, flash);

        // No charges: the whole row is dimmed (but fully visible while it flashes).
        float restAlpha = powerCharges[row] > 0 ? 1f : powerEmptyAlpha;
        powerRowGroups[row].alpha = Mathf.Lerp(restAlpha, 1f, flash);
    }

    private static Color PowerColor(int row)
    {
        switch (row)
        {
            case (int)PowerKind.Lure: return Palette.LureCrate;
            case (int)PowerKind.Freeze: return Palette.FreezeCrate;
            case FrenzyRow: return Palette.WeaponFrenzy;
            default: return Palette.BlastOrange; // the RPG
        }
    }

    // Shows maxCharges pips in the row (creating more when needed), filled for
    // each charge the power has.
    private void ShowPips(int row, int maxCharges)
    {
        List<Image> pips = row == (int)PowerKind.Lure ? lurePips : freezePips;
        int shown = Mathf.Max(0, maxCharges);

        while (pips.Count < shown)
        {
            Image pip = CreateImage("Pip", powerPipStrips[row], EmptyPipColor);
            float x = PowerPipPadding + pips.Count * (PowerPipSize + PowerPipGap);
            PlaceRect(pip.rectTransform, LeftMiddle, LeftMiddle, new Vector2(x, 0f), new Vector2(PowerPipSize, PowerPipSize));
            pips.Add(pip);
        }

        for (int i = 0; i < pips.Count; i++)
        {
            bool visible = i < shown;
            if (pips[i].gameObject.activeSelf != visible)
            {
                pips[i].gameObject.SetActive(visible);
            }
            pips[i].color = i < powerCharges[row] ? PowerColor(row) : EmptyPipColor;
        }

        // The dark strip hugs the visible pips.
        float stripWidth = 0f;
        if (shown > 0)
        {
            stripWidth = PowerPipPadding * 2f + shown * PowerPipSize + (shown - 1) * PowerPipGap;
        }
        powerPipStrips[row].sizeDelta = new Vector2(stripWidth, PowerPipSize + PowerPipPadding * 2f);
    }

    // Space kept between the top of the bottom word box and the corner panels.
    private const float AboveWordBoxGap = 16f;

    // How high (canvas units from the bottom edge) the bottom-left powers and the
    // bottom-right aiming hint start: just above the TOP of the bottom-centre
    // word box, measured from the box itself. On a narrow window the box reaches
    // the corners, so sitting above it keeps them from ever overlapping it.
    private float BottomRowY()
    {
        float y = powersMargin;
        RectTransform box = targetWordText != null ? targetWordText.rectTransform.parent as RectTransform : null;
        if (box != null && box.anchorMin.y == 0f && box.anchorMax.y == 0f) // anchored to the bottom edge
        {
            float top = box.anchoredPosition.y + box.sizeDelta.y * (1f - box.pivot.y);
            y = Mathf.Max(y, top + AboveWordBoxGap);
        }
        return y;
    }

    // Powers (bottom-left corner) > one row per power > KeyCap (+ Key text), Name, Pips.
    private void BuildPowers()
    {
        float panelHeight = PowerCount * PowerRowHeight + (PowerCount - 1) * PowerRowGap;
        powersPanel = CreateRect("Powers", readoutLayer);
        PlaceRect(powersPanel, BottomLeft, BottomLeft, new Vector2(powersMargin, BottomRowY()),
            new Vector2(PowerPipsX + 120f, panelHeight));

        string[] rowNames = { "LURE BOMB", "FREEZE", "FRENZY", "RPG" };
        for (int row = 0; row < PowerCount; row++)
        {
            string key = (row + 1).ToString();
            string powerName = rowNames[row];

            // Rows are stacked from the bottom: the last row sits at the bottom.
            // The pivot is the row's left-middle, so a pulsing row grows to the right.
            float y = (PowerCount - 1 - row) * (PowerRowHeight + PowerRowGap) + PowerRowHeight * 0.5f;
            RectTransform rowRect = CreateRect(powerName, powersPanel);
            PlaceRect(rowRect, BottomLeft, LeftMiddle, new Vector2(0f, y), new Vector2(PowerPipsX + 120f, PowerRowHeight));
            powerRows[row] = rowRect;
            powerRowGroups[row] = rowRect.gameObject.AddComponent<CanvasGroup>();

            Image keyCap = CreateImage("KeyCap", rowRect, Color.white);
            PlaceRect(keyCap.rectTransform, LeftMiddle, LeftMiddle, Vector2.zero, new Vector2(PowerKeySize, PowerKeySize));
            powerKeyCaps[row] = keyCap;
            TMP_Text keyText = CreateText(keyCap.rectTransform, "Key", key, 30f, Vector2.zero);
            StretchToParent(keyText.rectTransform);
            keyText.color = Color.black;
            keyText.fontStyle = FontStyles.Bold;

            TMP_Text nameText = CreateText(rowRect, "Name", powerName, 30f, Vector2.zero);
            PlaceRect(nameText.rectTransform, LeftMiddle, LeftMiddle, new Vector2(PowerNameX, 0f),
                new Vector2(PowerNameWidth, PowerRowHeight));
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.fontStyle = FontStyles.Bold;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.fontSharedMaterial = OutlineMaterial(nameText);
            powerNames[row] = nameText;

            if (row < FrenzyRow)
            {
                Image strip = CreateImage("Pips", rowRect, new Color(0f, 0f, 0f, 0.55f));
                PlaceRect(strip.rectTransform, LeftMiddle, LeftMiddle, new Vector2(PowerPipsX, 0f), Vector2.zero);
                powerPipStrips[row] = strip.rectTransform;
            }
            else
            {
                BuildWeaponSlot(rowRect, row);
            }

            ApplyPowerRowLook(row, 0f, 0f);
        }
        SetWeapons(0, 0);
    }

    // A weapon row: one plain square per slot (Powers.MaxKept), each on a dark
    // square where the pips would be, and the whole row is a button (clicking it
    // uses the weapon, like its key).
    private void BuildWeaponSlot(RectTransform rowRect, int row)
    {
        for (int i = 0; i < Powers.MaxKept; i++)
        {
            Image slot = CreateImage("WeaponSlot", rowRect, new Color(0f, 0f, 0f, 0.55f));
            PlaceRect(slot.rectTransform, LeftMiddle, LeftMiddle, new Vector2(PowerPipsX + i * (WeaponSlotSize + PowerPipGap), 0f),
                new Vector2(WeaponSlotSize, WeaponSlotSize));

            Image square = CreateImage("Square", slot.rectTransform, EmptyPipColor);
            PlaceRect(square.rectTransform, Centre, Centre, Vector2.zero, new Vector2(WeaponSquareSize, WeaponSquareSize));
            weaponSquares[row, i] = square;
        }

        // The HOLD bar just under the slots: fills while 3 / 4 is held to buy
        // (Powers, SetWeaponHold). Hidden while the key is not held.
        float barWidth = Powers.MaxKept * WeaponSlotSize + (Powers.MaxKept - 1) * PowerPipGap;
        Image bar = CreateImage("HoldBar", rowRect, new Color(0f, 0f, 0f, 0.6f));
        PlaceRect(bar.rectTransform, LeftMiddle, new Vector2(0f, 1f), new Vector2(PowerPipsX, -WeaponSlotSize * 0.5f - 2f),
            new Vector2(barWidth, WeaponHoldBarHeight));
        Image fill = CreateImage("Fill", bar.rectTransform, PowerColor(row));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        weaponHoldBars[row - FrenzyRow] = bar.rectTransform;
        weaponHoldFills[row - FrenzyRow] = fill.rectTransform;
        bar.gameObject.SetActive(false);

        // An invisible image over the row catches the mouse click.
        Image hitArea = rowRect.gameObject.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        Button button = rowRect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None; // the arrow keys aim lure bombs, they never select buttons
        button.navigation = navigation;
        bool rocket = row == RocketRow;
        button.onClick.AddListener(() => OnWeaponClicked(rocket));
    }

    private const float WeaponHoldBarHeight = 6f;
    private readonly RectTransform[] weaponHoldBars = new RectTransform[2];  // [0] FRENZY, [1] RPG
    private readonly RectTransform[] weaponHoldFills = new RectTransform[2];

    // How far 3 (FRENZY) or 4 (RPG) has been held toward buying one (0..1);
    // 0 hides the bar.
    public void SetWeaponHold(bool rocket, float progress)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }
        int index = rocket ? 1 : 0;
        bool show = progress > 0f;
        if (weaponHoldBars[index].gameObject.activeSelf != show)
        {
            weaponHoldBars[index].gameObject.SetActive(show);
        }
        weaponHoldFills[index].anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    private void OnWeaponClicked(bool rocket)
    {
        if (GameManager.Instance.State == GameState.Playing)
        {
            GameManager.Instance.Powers.UseWeapon(rocket);
        }
    }

    // ---- Lure bomb aiming hint (bottom-right, while 1 is held) ----

    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private const string AimHintText =
        "Use the ARROW KEYS to aim\n<size=75%>LEFT / RIGHT: direction    UP / DOWN: distance\nRelease 1 to throw</size>";

    private const float AimHintFontSize = 22f;
    private const float AimHintPadding = 14f;   // space between the text and the edge of its box

    private Image aimHintBox; // null until the first time 1 is held

    // Shown by Powers while the player holds 1 to aim the lure bomb: the text in a
    // see-through black box, sized to fit the text.
    public void SetAimHint(bool show)
    {
        if (!show)
        {
            if (aimHintBox != null)
            {
                aimHintBox.gameObject.SetActive(false);
            }
            return;
        }

        if (aimHintBox == null)
        {
            aimHintBox = CreateImage("AimHint", readoutLayer, new Color(0f, 0f, 0f, 0.6f));

            TMP_Text text = CreateText(aimHintBox.rectTransform, "Text", AimHintText, AimHintFontSize, Vector2.zero);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.color = Palette.BlastOrange; // the same orange as the dashed arc
            text.textWrappingMode = TextWrappingModes.NoWrap;

            Vector2 textSize = text.GetPreferredValues(AimHintText);
            Vector2 boxSize = textSize + Vector2.one * (AimHintPadding * 2f);
            PlaceRect(aimHintBox.rectTransform, BottomRight, BottomRight, new Vector2(-powersMargin, BottomRowY()), boxSize);
            StretchToParent(text.rectTransform);
        }
        aimHintBox.gameObject.SetActive(true);
    }

    // ---- "+1" next to a power (bottom-left) when a charge is gained ----

    private const float GainRiseDistance = 36f;  // canvas units it floats up
    private const float GainSeconds = 1f;

    // A "+1" appears just right of that power's row, floats up and fades out.
    public void ShowPowerGain(PowerKind kind)
    {
        ShowRowGain((int)kind);
    }

    // The same "+1" next to a combo weapon's slot when one is bought.
    public void ShowWeaponGain(bool rocket)
    {
        ShowRowGain(rocket ? RocketRow : FrenzyRow);
    }

    private void ShowRowGain(int row)
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }

        TMP_Text plusOne = CreateText(powersPanel, "PlusOne", "+1", 34f, Vector2.zero);
        RectTransform rowRect = powerRows[row];
        float x = row < FrenzyRow ? PowerPipsX + 140f : PowerPipsX + Powers.MaxKept * (WeaponSlotSize + PowerPipGap) + 10f;
        Vector2 start = rowRect.anchoredPosition + new Vector2(x, 0f);
        PlaceRect(plusOne.rectTransform, BottomLeft, LeftMiddle, start, new Vector2(80f, PowerRowHeight));
        plusOne.alignment = TextAlignmentOptions.Left;
        plusOne.fontStyle = FontStyles.Bold;
        plusOne.color = PowerColor(row);
        plusOne.fontSharedMaterial = OutlineMaterial(plusOne);
        StartCoroutine(FloatAndFade(plusOne, start));
    }

    // Game time: while a tip box pauses the game, the "+1" waits, then plays.
    private System.Collections.IEnumerator FloatAndFade(TMP_Text text, Vector2 start)
    {
        for (float time = 0f; time < GainSeconds; time += Time.deltaTime)
        {
            float progress = time / GainSeconds;
            text.rectTransform.anchoredPosition = start + Vector2.up * GainRiseDistance * progress;
            text.alpha = 1f - progress * progress; // stays bright, then fades at the end
            yield return null;
        }
        Destroy(text.gameObject);
    }

    // ---- First-time power tip box (the game is frozen while it shows) ----

    private GameObject tipPanel;   // null until the first tip
    private RectTransform tipBox;
    private TMP_Text tipTitle;
    private TMP_Text tipBody;
    private TMP_Text tipHint;
    private RectTransform tipButton;

    // Tip box layout, in canvas units: the box grows to fit the text, and
    // everything is stacked from the top, so a long text never runs into the title.
    private const float TipBoxWidth = 1100f;
    private const float TipTextWidth = 1000f;     // the body wraps at this width
    private const float TipPadding = 40f;         // inside the box, top and bottom
    private const float TipTitleSize = 56f;
    private const float TipBodySize = 28f;
    private const float TipGap = 28f;             // between title, body, hint and button
    private const float TipButtonHeight = 80f;

    public void ShowPowerTip(string title, string body, Color titleColor)
    {
        if (tipPanel == null)
        {
            BuildTipPanel();
        }
        tipTitle.text = title;
        tipTitle.color = titleColor;
        tipBody.text = body;
        LayoutTip();
        tipPanel.transform.SetAsLastSibling(); // on top of everything
        tipPanel.SetActive(true);
    }

    // Measures the body text and stacks title, body, hint and OK button from the
    // top of the box, then sizes the box around them.
    private void LayoutTip()
    {
        float titleHeight = TipTitleSize * 1.3f;
        float bodyHeight = tipBody.GetPreferredValues(tipBody.text, TipTextWidth, 0f).y;
        float hintHeight = tipHint.fontSize * 1.4f;

        float y = -TipPadding; // from the top of the box, going down
        PlaceTop(tipTitle.rectTransform, y, TipTextWidth, titleHeight);
        y -= titleHeight + TipGap;
        PlaceTop(tipBody.rectTransform, y, TipTextWidth, bodyHeight);
        y -= bodyHeight + TipGap;
        PlaceTop(tipHint.rectTransform, y, TipTextWidth, hintHeight);
        y -= hintHeight + TipGap * 0.5f;
        PlaceTop(tipButton, y, tipButton.sizeDelta.x, TipButtonHeight);
        y -= TipButtonHeight + TipPadding;

        tipBox.sizeDelta = new Vector2(TipBoxWidth, -y);
    }

    // Puts rect at 'y' units below the top of the tip box, centred left-right.
    private static void PlaceTop(RectTransform rect, float y, float width, float height)
    {
        PlaceRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(width, height));
    }

    public void HidePowerTip()
    {
        if (tipPanel != null)
        {
            tipPanel.SetActive(false);
        }
    }

    // TipPanel (dark full-screen) > Box > Title, Body, "Press ENTER to continue", OK button.
    private void BuildTipPanel()
    {
        tipPanel = new GameObject("TipPanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = tipPanel.GetComponent<RectTransform>();
        panelRect.SetParent(transform, false);
        StretchToParent(panelRect);
        tipPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // The box is centred on the screen; its height is set by LayoutTip.
        Image box = CreateImage("Box", panelRect, new Color(0.03f, 0.05f, 0.1f, 0.92f));
        tipBox = box.rectTransform;
        PlaceRect(tipBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TipBoxWidth, 520f));

        tipTitle = CreateText(tipBox, "Title", "", TipTitleSize, Vector2.zero);
        tipTitle.fontStyle = FontStyles.Bold;
        tipTitle.textWrappingMode = TextWrappingModes.NoWrap;

        tipBody = CreateText(tipBox, "Body", "", TipBodySize, Vector2.zero);
        tipBody.textWrappingMode = TextWrappingModes.Normal;
        tipBody.alignment = TextAlignmentOptions.Top;

        tipHint = CreateText(tipBox, "Hint", "Press ENTER to continue", 26f, Vector2.zero);
        tipHint.color = SmallTextColor;
        TMP_Text okLabel = CreateButton(tipBox, "OkButton", "OK", Vector2.zero, OnTipOkClicked);
        tipButton = (RectTransform)okLabel.rectTransform.parent;
    }

    private void OnTipOkClicked()
    {
        GameManager.Instance.ConfirmTip();
    }

    // ---- Combo reward (top-centre, on the combo plate, under the big COMBO) ----

    private const float ComboRewardTop = 82f;     // canvas units below the top of the combo plate
    private const float ComboBarTop = 114f;
    private const float ComboBarSpeed = 2.5f;     // the bar fills / empties at up to 2.5 whole bars per second
    private static readonly Vector2 ComboBarSize = new Vector2(380f, 8f);
    private static readonly Color SmallTextColor = new Color(0.8f, 0.82f, 0.86f); // light grey

    private TMP_Text comboRewardText;       // null until the first SetWeaponStatus
    private RectTransform comboRewardBar;   // dark background of the bar
    private RectTransform comboRewardFill;
    private Image comboRewardFillImage;
    private float comboRewardTarget;        // the progress to show (0..1)
    private float comboRewardShown;         // the progress drawn now; slides toward comboRewardTarget

    // Under the combo text: the COMBO WEAPONS status (Powers), e.g.
    // "[3] FRENZY ready!  RPG at 20" with its bar filled to 'progress' (0..1)
    // in 'color'. The text may hold rich-text tags.
    public void SetWeaponStatus(string text, float progress, Color color)
    {
        if (comboRewardText == null)
        {
            BuildComboReward();
        }
        comboRewardTarget = Mathf.Clamp01(progress);
        comboRewardText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        comboRewardFillImage.color = color;
    }

    private void UpdateComboRewardBar()
    {
        if (comboRewardFill == null || comboRewardShown == comboRewardTarget)
        {
            return;
        }

        // Slide toward the real progress instead of jumping there.
        comboRewardShown = Mathf.MoveTowards(comboRewardShown, comboRewardTarget, ComboBarSpeed * Time.unscaledDeltaTime);
        comboRewardFill.anchorMax = new Vector2(comboRewardShown, 1f);
    }

    // ComboReward (text) and ComboRewardBar (dark) > Fill, centred on the combo plate under the big COMBO.
    private void BuildComboReward()
    {
        if (comboPlate == null)
        {
            BuildComboPlate();
        }
        comboRewardText = CreateText(comboPlate, "ComboReward", "", 24f, Vector2.zero);
        PlaceRect(comboRewardText.rectTransform, TopCentre, TopCentre, new Vector2(0f, -ComboRewardTop),
            new Vector2(ComboPlateSize.x - 20f, 30f));
        comboRewardText.alignment = TextAlignmentOptions.Center;
        comboRewardText.fontStyle = FontStyles.Bold;
        comboRewardText.textWrappingMode = TextWrappingModes.NoWrap;
        comboRewardText.color = SmallTextColor;

        Image bar = CreateImage("ComboRewardBar", comboPlate, new Color(0.2f, 0.21f, 0.24f, 0.9f));
        comboRewardBar = bar.rectTransform;
        PlaceRect(comboRewardBar, TopCentre, TopCentre, new Vector2(0f, -ComboBarTop), ComboBarSize);

        // The fill is anchored to the bar's left edge; its right anchor is the progress.
        comboRewardFillImage = CreateImage("Fill", comboRewardBar, Yellow);
        comboRewardFill = comboRewardFillImage.rectTransform;
        comboRewardFill.anchorMin = Vector2.zero;
        comboRewardFill.anchorMax = new Vector2(0f, 1f);
        comboRewardFill.offsetMin = Vector2.zero;
        comboRewardFill.offsetMax = Vector2.zero;
        comboRewardShown = 0f;
    }

    // ---- Hint box (bottom-centre, just above the target word box) ----

    private const float HintBottom = 174f;        // the target word box ends 160 units above the bottom edge
    private const float HintMaxTextWidth = 1100f; // longer hints wrap onto more lines
    private const float HintStripWidth = 8f;      // the yellow strip on the left
    private const float HintPaddingLeft = 30f;    // text starts after the strip
    private const float HintPaddingRight = 22f;
    private const float HintPaddingY = 12f;
    private const float HintFadeInSeconds = 0.2f;
    private const float HintFadeOutSeconds = 0.4f;
    private const float HintRise = 14f;           // it rises this many units while fading in

    private RectTransform hintBox;   // null until the first hint
    private CanvasGroup hintGroup;   // fades the whole box
    private TMP_Text hintText;
    private float hintTimeLeft;      // game seconds before the hint starts fading out
    private float hintAlpha;         // 0 = invisible .. 1 = fully visible

    // A hint box above the typed word at the bottom of the screen, for seconds
    // (game time). Newer hints replace older ones. "" fades the current hint out.
    public void ShowHint(string text, float seconds)
    {
        if (string.IsNullOrEmpty(text))
        {
            hintTimeLeft = 0f;
            return;
        }

        if (hintBox == null)
        {
            BuildHint();
        }
        if (!hintBox.gameObject.activeSelf)
        {
            hintAlpha = 0f; // it was hidden: fade in from nothing
            hintBox.gameObject.SetActive(true);
        }

        // Fit the box around the text: as wide as the text (up to the limit), as tall as its lines.
        Vector2 textSize = hintText.GetPreferredValues(text, HintMaxTextWidth, 10000f);
        float textWidth = Mathf.Min(textSize.x, HintMaxTextWidth) + 2f; // +2: a little room so it never wraps early
        hintBox.sizeDelta = new Vector2(textWidth + HintPaddingLeft + HintPaddingRight, textSize.y + HintPaddingY * 2f);
        hintText.text = text;

        hintTimeLeft = Mathf.Max(0.1f, seconds);
        ApplyHint();
    }

    private void UpdateHint()
    {
        if (hintBox == null || !hintBox.gameObject.activeSelf)
        {
            return;
        }

        float deltaTime = Time.deltaTime; // game time: the hint waits while the game is paused
        if (hintTimeLeft > 0f)
        {
            hintTimeLeft -= deltaTime;
            hintAlpha = Mathf.MoveTowards(hintAlpha, 1f, deltaTime / HintFadeInSeconds);
        }
        else
        {
            hintAlpha = Mathf.MoveTowards(hintAlpha, 0f, deltaTime / HintFadeOutSeconds);
            if (hintAlpha <= 0f)
            {
                hintBox.gameObject.SetActive(false);
                return;
            }
        }
        ApplyHint();
    }

    private void ApplyHint()
    {
        hintGroup.alpha = hintAlpha;
        hintBox.anchoredPosition = new Vector2(0f, HintBottom - HintRise * (1f - hintAlpha));
    }

    // Hint (dark box, bottom-centre) > Strip (yellow, left edge) + Text.
    private void BuildHint()
    {
        Image box = CreateImage("HintBox", hintLayer, new Color(0.03f, 0.04f, 0.08f, 0.82f));
        hintBox = box.rectTransform;
        PlaceRect(hintBox, BottomCentre, BottomCentre, new Vector2(0f, HintBottom), new Vector2(600f, 60f));
        hintGroup = box.gameObject.AddComponent<CanvasGroup>();

        // The strip is as tall as the box (anchored to its left edge, top to bottom).
        Image strip = CreateImage("Strip", hintBox, Yellow);
        strip.rectTransform.anchorMin = new Vector2(0f, 0f);
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = LeftMiddle;
        strip.rectTransform.anchoredPosition = Vector2.zero;
        strip.rectTransform.sizeDelta = new Vector2(HintStripWidth, 0f);

        // The text fills the box minus the padding.
        hintText = CreateText(hintBox, "Text", "", 34f, Vector2.zero);
        RectTransform textRect = hintText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(HintPaddingLeft, HintPaddingY);
        textRect.offsetMax = new Vector2(-HintPaddingRight, -HintPaddingY);
        hintText.alignment = TextAlignmentOptions.Left;
        hintText.textWrappingMode = TextWrappingModes.Normal;

        hintAlpha = 0f;
    }

    // ---- Threat arrows (screen edges) ----

    private const int MaxThreatArrows = 8;
    private const float ThreatArrowArmLength = 34f;
    private const float ThreatArrowThickness = 10f;
    private const float ThreatArrowFarAlpha = 0.3f;   // alpha of an arrow for a far threat
    private const float ThreatArrowFarScale = 0.75f;  // size of an arrow for a far threat
    private const float ThreatArrowNearScale = 1.25f; // size of an arrow for a near threat
    private const float ThreatArrowThrob = 0.12f;     // near threats: the arrow throbs by +-12%
    private const float ThreatArrowThrobSpeed = 12f;

    private readonly RectTransform[] threatArrows = new RectTransform[MaxThreatArrows]; // built on first use
    private readonly CanvasGroup[] threatArrowGroups = new CanvasGroup[MaxThreatArrows];

    // Arrows at the screen edges pointing at threats that are off screen
    // (world positions). Call every frame; an empty list hides them all.
    public void UpdateThreatArrows(List<Vector3> threats, Camera cam)
    {
        int used = 0; // arrows placed this frame

        if (threats != null && threats.Count > 0 && cam != null)
        {
            if (threatArrows[0] == null)
            {
                BuildThreatArrows();
            }

            // The arrows sit on a rectangle threatArrowInset inside the screen edges.
            Rect area = markerLayer.rect;
            float halfWidth = area.width * 0.5f - threatArrowInset;
            float halfHeight = area.height * 0.5f - threatArrowInset;
            Transform view = cam.transform;

            for (int i = 0; i < threats.Count && used < MaxThreatArrows; i++)
            {
                Vector3 threat = threats[i];

                // On screen (in front of the camera and inside the view): no arrow needed.
                Vector3 viewportPoint = cam.WorldToViewportPoint(threat);
                bool onScreen = viewportPoint.z > 0f
                    && viewportPoint.x >= 0f && viewportPoint.x <= 1f
                    && viewportPoint.y >= 0f && viewportPoint.y <= 1f;
                if (onScreen)
                {
                    continue;
                }

                PlaceThreatArrow(used, threat, view, halfWidth, halfHeight);
                used++;
            }
        }

        // Hide the arrows not used this frame.
        for (int i = used; i < MaxThreatArrows; i++)
        {
            if (threatArrows[i] != null && threatArrows[i].gameObject.activeSelf)
            {
                threatArrows[i].gameObject.SetActive(false);
            }
        }
    }

    private void PlaceThreatArrow(int index, Vector3 threat, Transform view, float halfWidth, float halfHeight)
    {
        // Which way to point, seen from the middle of the screen: the threat's
        // position relative to the camera (x = right, y = up).
        //   - In front of the camera this is exactly the direction in which the
        //     point would appear on screen.
        //   - BEHIND the camera the projected point comes out mirrored through the
        //     middle of the screen; x and y relative to the camera are that
        //     projection flipped back, so "behind you on the left" points left.
        Vector3 local = view.InverseTransformPoint(threat);
        Vector2 direction = new Vector2(local.x, local.y);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.down; // exactly behind the camera: point down ("behind you")
        }

        // Stretch the direction until it touches the inset rectangle.
        float stretchX = direction.x != 0f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float stretchY = direction.y != 0f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        Vector2 edgePoint = direction * Mathf.Min(stretchX, stretchY);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // closeness: 1 = nearer than threatNearDistance .. 0 = farther than threatFarDistance.
        float distance = Vector3.Distance(view.position, threat);
        float closeness = 1f - Mathf.Clamp01((distance - threatNearDistance) / Mathf.Max(0.01f, threatFarDistance - threatNearDistance));
        float scale = Mathf.Lerp(ThreatArrowFarScale, ThreatArrowNearScale, closeness);
        if (closeness >= 1f)
        {
            scale *= 1f + ThreatArrowThrob * Mathf.Sin(Time.unscaledTime * ThreatArrowThrobSpeed); // very close: warning throb
        }

        RectTransform arrow = threatArrows[index];
        arrow.anchoredPosition = edgePoint;
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        arrow.localScale = new Vector3(scale, scale, 1f);
        threatArrowGroups[index].alpha = Mathf.Lerp(ThreatArrowFarAlpha, 1f, closeness);
        if (!arrow.gameObject.activeSelf)
        {
            arrow.gameObject.SetActive(true);
        }
    }

    // 8 arrows, each a ">" chevron: two bars that meet at the arrow's tip. The
    // tip is the arrow's position and it points right (+x) before it is turned.
    private void BuildThreatArrows()
    {
        for (int i = 0; i < MaxThreatArrows; i++)
        {
            RectTransform arrow = CreateRect("ThreatArrow", markerLayer);
            arrow.sizeDelta = Vector2.zero;
            threatArrowGroups[i] = arrow.gameObject.AddComponent<CanvasGroup>();
            AddArrowBar(arrow, 45f);   // lower bar of the ">"
            AddArrowBar(arrow, -45f);  // upper bar of the ">"
            arrow.gameObject.SetActive(false);
            threatArrows[i] = arrow;
        }
    }

    // One bar of a chevron. Its pivot is its right end (the tip), so turning it
    // swings the bar around the tip: +45 degrees = down-left, -45 = up-left.
    private void AddArrowBar(RectTransform arrow, float degrees)
    {
        Image bar = CreateImage("Bar", arrow, threatArrowColor);
        PlaceRect(bar.rectTransform, Centre, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(ThreatArrowArmLength, ThreatArrowThickness));
        bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    // ---- Floating texts ----

    private const int FloatingTextCount = 10;       // at most this many at once (the oldest is reused)
    private const float FloatingTextPopSeconds = 0.12f;
    private const float FloatingTextPopScale = 1.35f;
    private const float FloatingTextFadeFrom = 0.55f; // starts fading after 55% of its life
    private const float FloatingStackSeconds = 0.35f; // texts started this close together...
    private const float FloatingStackRange = 2f;      // ...this close (metres) are stacked...
    private const float FloatingStackGap = 52f;       // ...this many canvas units apart

    private readonly TMP_Text[] floatingTexts = new TMP_Text[FloatingTextCount]; // built on first use
    private readonly Vector3[] floatingPoints = new Vector3[FloatingTextCount];   // the world point each follows
    private readonly float[] floatingAges = new float[FloatingTextCount];
    private readonly float[] floatingLifts = new float[FloatingTextCount];        // extra height when stacked

    // Text that appears at a world position (e.g. "+30" or "+1 LURE BOMB"),
    // rises and fades out.
    public void ShowFloatingText(Vector3 worldPosition, string text, Color color)
    {
        if (floatingTexts[0] == null)
        {
            BuildFloatingTexts();
        }

        int slot = FreeFloatingText();

        // Texts that start together at the same place (for example several kills
        // at once) are stacked instead of drawn on top of each
        // other: the new one starts just above the highest recent one.
        float lift = 0f;
        for (int i = 0; i < FloatingTextCount; i++)
        {
            if (i == slot || !floatingTexts[i].gameObject.activeSelf)
            {
                continue;
            }
            bool recent = floatingAges[i] < FloatingStackSeconds;
            bool near = (floatingPoints[i] - worldPosition).sqrMagnitude < FloatingStackRange * FloatingStackRange;
            if (recent && near)
            {
                float top = floatingLifts[i] + FloatingRise(floatingAges[i]) + FloatingStackGap;
                lift = Mathf.Max(lift, top);
            }
        }

        TMP_Text label = floatingTexts[slot];
        label.text = text;
        label.color = color;
        floatingPoints[slot] = worldPosition;
        floatingAges[slot] = 0f;
        floatingLifts[slot] = lift;
        label.transform.SetAsLastSibling(); // the newest text is drawn on top
        label.gameObject.SetActive(true);
        PlaceFloatingText(slot, Camera.main);
    }

    // A hidden text, or else the oldest one.
    private int FreeFloatingText()
    {
        int oldest = 0;
        for (int i = 0; i < FloatingTextCount; i++)
        {
            if (!floatingTexts[i].gameObject.activeSelf)
            {
                return i;
            }
            if (floatingAges[i] > floatingAges[oldest])
            {
                oldest = i;
            }
        }
        return oldest;
    }

    // How far a floating text has risen at this age (steady speed, so stacked
    // texts keep the same gap between them).
    private float FloatingRise(float age)
    {
        return floatingTextRise * Mathf.Clamp01(age / Mathf.Max(0.01f, floatingTextSeconds));
    }

    private void UpdateFloatingTexts()
    {
        if (floatingTexts[0] == null)
        {
            return;
        }

        Camera cam = Camera.main;
        for (int i = 0; i < FloatingTextCount; i++)
        {
            if (!floatingTexts[i].gameObject.activeSelf)
            {
                continue;
            }

            floatingAges[i] += Time.deltaTime; // game time: frozen while paused
            if (floatingAges[i] >= floatingTextSeconds)
            {
                floatingTexts[i].gameObject.SetActive(false);
                continue;
            }
            PlaceFloatingText(i, cam);
        }
    }

    // Pins a floating text to its world point on screen, risen and faded by its age.
    private void PlaceFloatingText(int slot, Camera cam)
    {
        TMP_Text label = floatingTexts[slot];
        if (cam == null)
        {
            label.enabled = false;
            return;
        }

        Vector3 screenPoint = cam.WorldToScreenPoint(floatingPoints[slot]);
        if (screenPoint.z <= 0f)
        {
            label.enabled = false; // behind the camera (it keeps ageing)
            return;
        }

        Vector2 position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerLayer, screenPoint, null, out position);

        float age = floatingAges[slot];
        position.y += floatingLifts[slot] + FloatingRise(age);
        label.rectTransform.anchoredPosition = position;

        // Pops in a bit big, then settles.
        float scale = Mathf.Lerp(FloatingTextPopScale, 1f, Mathf.Clamp01(age / FloatingTextPopSeconds));
        label.rectTransform.localScale = new Vector3(scale, scale, 1f);

        // Fully visible, then fades out over the rest of its life.
        float life = Mathf.Clamp01(age / Mathf.Max(0.01f, floatingTextSeconds));
        label.alpha = life < FloatingTextFadeFrom ? 1f : 1f - (life - FloatingTextFadeFrom) / (1f - FloatingTextFadeFrom);
        label.enabled = true;
    }

    private void BuildFloatingTexts()
    {
        for (int i = 0; i < FloatingTextCount; i++)
        {
            TMP_Text label = CreateText(markerLayer, "FloatingText", "", 44f, Vector2.zero);
            label.rectTransform.sizeDelta = new Vector2(900f, 70f);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSharedMaterial = OutlineMaterial(label);
            label.gameObject.SetActive(false);
            floatingTexts[i] = label;
        }
    }

    // ---- Boss quiz (top-centre, under the boss health bar) ----

    private const float QuizTop = 232f;              // canvas units below the top edge (combo and boss bar are above)
    private const float QuizMaxTextWidth = 1300f;    // longer questions wrap onto more lines
    private const float QuizMinBoxWidth = 760f;
    private const float QuizSidePadding = 50f;
    private const float QuizCaptionTop = 14f;
    private const float QuizQuestionTop = 58f;       // below the caption
    private const float QuizTimerBottom = 18f;
    private const float QuizExtraHeight = 102f;      // box height = question height + this (caption, timer, padding)
    private const float QuizTimerWarning = 0.25f;    // below this much time left the bar turns red
    private const float QuizPopSeconds = 0.18f;
    private const float QuizPopScale = 1.12f;
    private static readonly Vector2 QuizTimerSize = new Vector2(600f, 10f);
    private static readonly Color QuizTimerLowColor = new Color(1f, 0.3f, 0.2f);

    private RectTransform quizBox;        // null until the first quiz
    private TMP_Text quizQuestion;
    private RectTransform quizTimerFill;
    private Image quizTimerFillImage;
    private float quizPopAge = float.MaxValue;

    // Question box at the top-centre, under the boss health bar.
    public void ShowQuiz(string question)
    {
        if (quizBox == null)
        {
            BuildQuiz();
        }
        quizBox.gameObject.SetActive(true);

        // Fit the box to the question: as wide as the text (between the limits),
        // as tall as its lines.
        Vector2 textSize = quizQuestion.GetPreferredValues(question, QuizMaxTextWidth, 10000f);
        float textWidth = Mathf.Min(textSize.x, QuizMaxTextWidth) + 2f; // +2: a little room so it never wraps early
        quizQuestion.rectTransform.sizeDelta = new Vector2(textWidth, textSize.y);
        float boxWidth = Mathf.Max(QuizMinBoxWidth, textWidth + QuizSidePadding * 2f);
        quizBox.sizeDelta = new Vector2(boxWidth, textSize.y + QuizExtraHeight);
        quizQuestion.text = question;

        SetQuizTimer(1f);
        quizPopAge = 0f;
        UpdateQuizPop();
    }

    // Time left to answer (1 = all of it, 0 = none), as a shrinking bar.
    public void SetQuizTimer(float fraction)
    {
        if (quizBox == null)
        {
            return;
        }

        // The bar shrinks toward its middle.
        fraction = Mathf.Clamp01(fraction);
        quizTimerFill.anchorMin = new Vector2(0.5f - fraction * 0.5f, 0f);
        quizTimerFill.anchorMax = new Vector2(0.5f + fraction * 0.5f, 1f);
        quizTimerFillImage.color = fraction < QuizTimerWarning ? QuizTimerLowColor : Palette.WordQuiz;
    }

    public void HideQuiz()
    {
        if (quizBox != null)
        {
            quizBox.gameObject.SetActive(false);
        }
    }

    // A new question pops in slightly big, then settles.
    private void UpdateQuizPop()
    {
        if (quizBox == null || quizPopAge > QuizPopSeconds)
        {
            return;
        }
        quizPopAge += Time.unscaledDeltaTime;
        float settle = Mathf.Clamp01(quizPopAge / QuizPopSeconds);
        settle = 1f - (1f - settle) * (1f - settle);
        float scale = Mathf.Lerp(QuizPopScale, 1f, settle);
        quizBox.localScale = new Vector3(scale, scale, 1f);
    }

    // QuizBox (dark, top-centre) > Accent (cyan line on top), Caption, Question, Timer > Fill.
    private void BuildQuiz()
    {
        Image box = CreateImage("QuizBox", readoutLayer, new Color(0.03f, 0.05f, 0.1f, 0.88f));
        quizBox = box.rectTransform;
        PlaceRect(quizBox, TopCentre, TopCentre, new Vector2(0f, -QuizTop), new Vector2(QuizMinBoxWidth, 200f));

        Image accent = CreateImage("Accent", quizBox, Palette.WordQuiz);
        accent.rectTransform.anchorMin = new Vector2(0f, 1f);
        accent.rectTransform.anchorMax = new Vector2(1f, 1f);
        accent.rectTransform.pivot = TopCentre;
        accent.rectTransform.anchoredPosition = Vector2.zero;
        accent.rectTransform.sizeDelta = new Vector2(0f, 4f);

        TMP_Text caption = CreateText(quizBox, "Caption", "<b>BOSS QUIZ</b>   <color=#9AA3AE><size=80%>type the right answer</size></color>", 30f, Vector2.zero);
        PlaceRect(caption.rectTransform, TopCentre, TopCentre, new Vector2(0f, -QuizCaptionTop), new Vector2(QuizMaxTextWidth, 40f));
        caption.color = Palette.WordQuiz;
        caption.textWrappingMode = TextWrappingModes.NoWrap;

        quizQuestion = CreateText(quizBox, "Question", "", 48f, Vector2.zero);
        PlaceRect(quizQuestion.rectTransform, TopCentre, TopCentre, new Vector2(0f, -QuizQuestionTop), new Vector2(QuizMaxTextWidth, 60f));
        quizQuestion.textWrappingMode = TextWrappingModes.Normal;

        Image timer = CreateImage("Timer", quizBox, new Color(0.15f, 0.17f, 0.21f));
        PlaceRect(timer.rectTransform, BottomCentre, BottomCentre, new Vector2(0f, QuizTimerBottom), QuizTimerSize);

        quizTimerFillImage = CreateImage("Fill", timer.rectTransform, Palette.WordQuiz);
        quizTimerFill = quizTimerFillImage.rectTransform;
        quizTimerFill.anchorMin = Vector2.zero;
        quizTimerFill.anchorMax = Vector2.one;
        quizTimerFill.offsetMin = Vector2.zero;
        quizTimerFill.offsetMax = Vector2.zero;
    }

    // ---- End of game: stats ----

    private const string StatLabel = "<color=#9AA3AE>";  // grey labels, white numbers

    // Shows the "You survived" (won) or "You died" panel with the run's stats,
    // using the existing panels and their Restart buttons.
    public void ShowResults(bool won, RunStats stats)
    {
        if (stats == null)
        {
            stats = new RunStats(); // nothing was counted: show zeros rather than nothing
        }

        // The game is over: hide the enemies' words so they do not show through the panel.
        wordLayer.gameObject.SetActive(false);

        TMP_Text message;
        if (won)
        {
            ShowWonPanel(stats.Score);
            message = wonScoreText;
        }
        else
        {
            ShowLostPanel(stats.Score);
            message = lostScoreText;
        }

        // The stats replace "Final score: ...".
        message.text = StatsText(stats);
    }

    // Four lines, e.g.
    //   Score 12750
    //   Accuracy 97.5%    Speed 62 WPM
    //   Kills 50    Best combo x51    Biggest blast x7
    //   Time 4:12
    private static string StatsText(RunStats stats)
    {
        int seconds = Mathf.FloorToInt(stats.Seconds);
        string time = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        // InvariantCulture: always a dot in "97.5", whatever the computer's language.
        string accuracy = stats.Accuracy.ToString("0.0", CultureInfo.InvariantCulture);

        return "<size=125%>" + StatLabel + "Score</color>  <b>" + stats.Score + "</b></size>\n"
            + StatLabel + "Accuracy</color>  <b>" + accuracy + "%</b>     "
            + StatLabel + "Speed</color>  <b>" + Mathf.RoundToInt(stats.WordsPerMinute) + " WPM</b>\n"
            + StatLabel + "Kills</color>  <b>" + stats.Kills + "</b>     "
            + StatLabel + "Best combo</color>  <b>x" + stats.BestCombo + "</b>     "
            + StatLabel + "Biggest blast</color>  <b>x" + stats.BiggestMultiKill + "</b>\n"
            + StatLabel + "Time</color>  <b>" + time + "</b>";
    }

    // ---- Small UI helpers ----

    // An empty UI object (only a RectTransform) under parent.
    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    // A flat-coloured rectangle (an Image with no sprite draws a plain rectangle).
    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.GetComponent<RectTransform>().SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // never catches mouse clicks (the buttons must stay clickable)
        return image;
    }

    // Pins a UI element to one point of its parent.
    //   anchor: which point of the PARENT it sticks to   (0,0 = bottom-left ... 1,1 = top-right)
    //   pivot:  which point of the ELEMENT sits there
    //   position: offset from that point in canvas units;  size: width and height
    private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Makes a UI element fill its parent completely.
    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
