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
//   - the SHIELD just above the powers: "[TAB] SHIELD" with its charge, a bar
//     that drains while it is up, and a glow along the screen edges; after
//     Tab, the SHIELD WORD box (bottom-centre, above the hint) with its timer
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
//   - the Start panel (with its "Show tips" toggle and the TYPING DIFFICULTY
//     row), the "You survived" and "You died" panels (with the run's stats at
//     the end), the pause panel and the 3-2-1 resume countdown. The MENU
//     buttons, the SETTINGS gear and the TYPING DIFFICULTY row on these panels
//     are prefabs placed in each level scene (Assets/Prefabs/UI, see MenuButton,
//     SettingsButton and TypingDifficultySelector).
//
// The HUD never decides anything. Other scripts call these methods when a value
// changes (for example GameManager calls SetScore after a kill).
// The objects saved in the scene are wired in the scene (Easy, Normal and Hard);
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
    [SerializeField] private TypingDifficultySelector difficultySelector; // the TYPING DIFFICULTY row on the Start panel (none in Easy)

    // Empty full-screen objects in the scene (see DRAWING ORDER at the top).
    [Header("Drawing layers")]
    [SerializeField] private RectTransform wordLayer;     // enemy words: the first child (only the boss bar goes under it)
    [SerializeField] private RectTransform readoutLayer;  // powers, combo plate, quiz, aim hint
    [SerializeField] private RectTransform markerLayer;   // threat arrows, floating texts
    [SerializeField] private RectTransform hintLayer;     // hint box

    [Header("Boss health bar")]
    [SerializeField] private Vector2 bossBarSize = new Vector2(700f, 26f);
    // The bar sits under the combo plate (top-centre).
    private const float BossBarTop = 130f; // canvas units from the top of the screen (its title is above it)
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
        // Keep the full reference HUD inside narrow desktop and editor views.
        // TouchControls fits the canvas separately around the on-screen keyboard.
        if (!TouchControls.Active && GetComponentInParent<CanvasScaler>() is CanvasScaler scaler)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // The red flash is invisible until the first wrong key. Switching it off
        // saves drawing an invisible full-screen rectangle every frame.
        if (flashImage != null)
        {
            SetFlashAlpha(0f);
        }
        HudReveal.Revealed += RevealFeature;
    }

    private void OnDestroy()
    {
        HudReveal.Revealed -= RevealFeature;
    }

    private void Update()
    {
        UpdateMultiKill();
        UpdateRedFlash();
        UpdatePowerPulses();
        UpdateShieldLook();
        UpdateComboRewardBar();
        UpdateComboLook();
        UpdateHint();
        UpdateQuizPop();
        UpdateHealthOffer();
    }

    private void LateUpdate()
    {
        // After every Update, so the camera has already moved this frame and the
        // texts sit exactly on their world points.
        UpdateFloatingTexts();
        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave)
        {
            waveText.gameObject.SetActive(false);
            scoreText.gameObject.SetActive(false);
            healthBar.gameObject.SetActive(false);
            Transform healthLabel = wordLayer.parent.Find("Health Label");
            if (healthLabel != null) healthLabel.gameObject.SetActive(false);
            if (comboPlate != null) comboPlate.gameObject.SetActive(false);
            if (powersPanel != null) powersPanel.gameObject.SetActive(false);
            if (shieldRow != null) shieldRow.gameObject.SetActive(false);
            targetWordText.transform.parent.gameObject.SetActive(EasyTutorial.Active.AllowsTyping
                && EasyTutorial.Active.CurrentLesson != 2 && !string.IsNullOrEmpty(targetWordText.text));
            tipsToggle.gameObject.SetActive(false);
        }
        else if (HudReveal.Gated)
        {
            ApplyHudReveal();
        }
    }

    // ---- Guided first wave: the HUD appears piece by piece (HudReveal) ----

    private Transform healthLabel;      // the scene's "Health Label" (found once)
    private bool healthLabelSearched;

    private Transform HealthLabel
    {
        get
        {
            if (!healthLabelSearched)
            {
                healthLabelSearched = true;
                healthLabel = wordLayer.parent.Find("Health Label");
            }
            return healthLabel;
        }
    }

    // Shows only the parts of the HUD the guided intro has introduced so far.
    private void ApplyHudReveal()
    {
        bool health = HudReveal.IsShown(HudFeature.Health);
        bool energy = HudReveal.IsShown(HudFeature.Energy);
        bool combo = HudReveal.IsShown(HudFeature.Combo);
        SetShown(waveText.gameObject, HudReveal.IsShown(HudFeature.Wave));
        SetShown(scoreText.gameObject, HudReveal.IsShown(HudFeature.Score));
        SetShown(healthBar.gameObject, health);
        if (HealthLabel != null) SetShown(HealthLabel.gameObject, health);
        if (comboPlate != null) SetShown(comboPlate.gameObject, combo);
        if (comboText != null) SetShown(comboText.gameObject, combo);
        if (comboRewardText != null) SetShown(comboRewardText.gameObject, energy);
        if (comboRewardBar != null) SetShown(comboRewardBar.gameObject, energy);
        if (powersPanel != null)
        {
            bool anyPower = false;
            for (int row = 0; row < PowerCount; row++)
            {
                bool shown = HudReveal.IsShown(RowFeature(row));
                SetShown(powerRows[row].gameObject, shown);
                anyPower |= shown;
            }
            SetShown(powersPanel.gameObject, anyPower && !TouchControls.Active);
        }
        if (shieldRow != null) SetShown(shieldRow.gameObject, HudReveal.IsShown(HudFeature.Shield));
        SetShown(targetWordText.transform.parent.gameObject, !string.IsNullOrEmpty(targetWordText.text)); // no empty box
    }

    // The guided intro is over (or was skipped): every piece of the HUD is shown.
    public void ShowWholeHud()
    {
        SetShown(waveText.gameObject, true);
        SetShown(scoreText.gameObject, true);
        SetShown(healthBar.gameObject, true);
        if (HealthLabel != null) SetShown(HealthLabel.gameObject, true);
        if (comboPlate != null) SetShown(comboPlate.gameObject, true);
        if (comboText != null) SetShown(comboText.gameObject, true);
        if (comboRewardText != null) SetShown(comboRewardText.gameObject, true);
        if (comboRewardBar != null) SetShown(comboRewardBar.gameObject, true);
        if (powersPanel != null)
        {
            for (int row = 0; row < PowerCount; row++) SetShown(powerRows[row].gameObject, true);
            SetShown(powersPanel.gameObject, !TouchControls.Active);
        }
        if (shieldRow != null) SetShown(shieldRow.gameObject, true);
        SetShown(targetWordText.transform.parent.gameObject, true); // the word box (hidden while empty during the intro)
    }

    // The right end (screen x) of the visible power list and shield row in
    // the bottom-left corner, or NaN when none is shown. The guided intro's
    // card keeps clear of it.
    public float PowersRightEdge()
    {
        float right = float.NaN;
        Vector3[] corners = new Vector3[4];
        if (powersPanel != null && powersPanel.gameObject.activeInHierarchy)
        {
            for (int row = 0; row < PowerCount; row++)
            {
                if (powerRows[row].gameObject.activeInHierarchy)
                {
                    right = MaxRight(right, powerNames[row], corners);
                }
            }
        }
        if (shieldRow != null && shieldRow.gameObject.activeInHierarchy)
        {
            right = MaxRight(right, shieldName, corners);
            shieldPipStrip.GetWorldCorners(corners);
            right = float.IsNaN(right) ? corners[2].x : Mathf.Max(right, corners[2].x);
        }
        return right;
    }

    private static float MaxRight(float right, TMP_Text text, Vector3[] corners)
    {
        text.rectTransform.GetWorldCorners(corners);
        float end = corners[0].x + text.preferredWidth * text.rectTransform.lossyScale.x;
        return float.IsNaN(right) ? end : Mathf.Max(right, end);
    }

    // Where a feature sits on screen (the guided intro puts its message next to it).
    public RectTransform FeatureRect(HudFeature feature)
    {
        switch (feature)
        {
            case HudFeature.Health: return (RectTransform)healthBar.transform;
            case HudFeature.Score: return scoreText.rectTransform;
            case HudFeature.Wave: return waveText.rectTransform;
            case HudFeature.Combo: return comboPlate;
            case HudFeature.Energy: return comboRewardText != null ? comboRewardText.rectTransform : comboPlate;
            case HudFeature.Shield: return shieldRow;
            default: return powersPanel != null ? powerRows[RowOf(feature)] : null;
        }
    }

    private static void SetShown(GameObject target, bool shown)
    {
        if (target.activeSelf != shown)
        {
            target.SetActive(shown);
        }
    }

    private static HudFeature RowFeature(int row)
    {
        return row == 0 ? HudFeature.Lure : row == 1 ? HudFeature.Freeze
            : row == FrenzyRow ? HudFeature.Frenzy : HudFeature.Rpg;
    }

    private static int RowOf(HudFeature feature)
    {
        return feature == HudFeature.Lure ? 0 : feature == HudFeature.Freeze ? 1
            : feature == HudFeature.Frenzy ? FrenzyRow : RocketRow;
    }

    // A feature has just been introduced: it pops in, flashes, and a small
    // NEW floats off it (HudReveal.Revealed).
    private void RevealFeature(HudFeature feature)
    {
        ApplyHudReveal(); // visible from this very frame
        switch (feature)
        {
            case HudFeature.Health:
                StartCoroutine(PopIn(healthBar.transform));
                break;
            case HudFeature.Score:
                StartCoroutine(PopIn(scoreText.transform));
                break;
            case HudFeature.Wave:
                StartCoroutine(PopIn(waveText.transform));
                break;
            case HudFeature.Combo:
                comboPopTimer = ComboPopSeconds * 3f;
                if (comboPlate != null) ShowNewTag(comboPlate, new Vector2(ComboPlateSize.x * 0.5f + 40f, -ComboPlateSize.y * 0.5f));
                break;
            case HudFeature.Energy:
                if (comboRewardText != null)
                {
                    PlaceEnergyRow();
                    StartCoroutine(PopIn(comboRewardText.transform));
                    ShowNewTag(comboRewardBar, new Vector2(comboRewardBar.sizeDelta.x + 50f, 0f));
                }
                break;
            case HudFeature.Shield:
                if (shieldRow != null)
                {
                    StartCoroutine(RepeatPulse(-1));
                    ShowNewTag(shieldRow, new Vector2(PowerPipsX + 60f, 0f));
                }
                break;
            default:
                if (powersPanel != null)
                {
                    int row = RowOf(feature);
                    StartCoroutine(RepeatPulse(row));
                    ShowNewTag(powerRows[row], new Vector2(PowerPipsX + 60f, 0f));
                }
                break;
        }
    }

    // Three pulses in a row (row -1 = the shield row), so a new power is noticed.
    private System.Collections.IEnumerator RepeatPulse(int row)
    {
        for (int i = 0; i < 3; i++)
        {
            if (row < 0) PulseShield(); else PulseRow(row);
            yield return new WaitForSecondsRealtime(powerPulseSeconds + 0.08f);
        }
    }

    // From big to its normal size, with a little overshoot.
    private static System.Collections.IEnumerator PopIn(Transform target)
    {
        const float Seconds = 0.45f;
        for (float time = 0f; time < Seconds; time += Time.unscaledDeltaTime)
        {
            float t = time / Seconds;
            float back = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f); // ease-out-back, 0 -> 1
            float scale = Mathf.LerpUnclamped(1.8f, 1f, back);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // A small "NEW" next to a just-introduced piece of the HUD; it rises and fades.
    private void ShowNewTag(RectTransform anchor, Vector2 offset)
    {
        TMP_Text tag = CreateText(anchor, "New Tag", "NEW", 26f, offset);
        tag.rectTransform.sizeDelta = new Vector2(90f, 34f);
        tag.fontStyle = FontStyles.Bold;
        tag.color = new Color(1f, 0.85f, 0.25f);
        tag.fontSharedMaterial = OutlineMaterial(tag);
        StartCoroutine(RiseAndFade(tag, offset));
    }

    private static System.Collections.IEnumerator RiseAndFade(TMP_Text text, Vector2 start)
    {
        const float Seconds = 1.6f;
        for (float time = 0f; time < Seconds; time += Time.unscaledDeltaTime)
        {
            float progress = time / Seconds;
            text.rectTransform.anchoredPosition = start + Vector2.up * 40f * progress;
            text.alpha = 1f - progress * progress;
            yield return null;
        }
        Destroy(text.gameObject);
    }

    // ---- Readouts ----

    public void RestoreTutorialCombatHud()
    {
        waveText.gameObject.SetActive(true);
        scoreText.gameObject.SetActive(true);
        healthBar.gameObject.SetActive(true);
        Transform label = wordLayer.parent.Find("Health Label");
        if (label != null) label.gameObject.SetActive(true);
        if (comboPlate != null) comboPlate.gameObject.SetActive(true);
        if (powersPanel != null) powersPanel.gameObject.SetActive(!TouchControls.Active);
        if (shieldRow != null) shieldRow.gameObject.SetActive(true);
        targetWordText.transform.parent.gameObject.SetActive(true);
    }

    public void SetHealth(int health, int maxHealth)
    {
        healthBar.maxValue = maxHealth;
        healthBar.value = health;
    }

    // ---- Health word (under the health bar, see HealthWord) ----
    //   type  medkit  for health
    // A small line on a dark pill just under the bar; it pops in when offered.

    private RectTransform healthOffer;   // null until first offered
    private CanvasGroup healthOfferGroup;
    private TMP_Text healthOfferWord;
    private float healthOfferShownAt;
    private bool healthOfferOn;

    public TMP_Text HealthOfferLabel
    {
        get
        {
            if (healthOffer == null)
            {
                BuildHealthOffer();
            }
            return healthOfferWord;
        }
    }

    public void ShowHealthOffer(bool on)
    {
        if (healthOffer == null)
        {
            BuildHealthOffer();
        }
        if (on && !healthOfferOn)
        {
            healthOfferShownAt = Time.unscaledTime;
        }
        healthOfferOn = on;
        if (on)
        {
            healthOffer.gameObject.SetActive(true);
        }
    }

    public void SetHealthOffer(string coloredWord)
    {
        HealthOfferLabel.text = "<color=#B8BEC8>type</color>  <b>" + coloredWord + "</b>  <color=#B8BEC8>for health</color>";
        Vector2 size = healthOfferWord.GetPreferredValues(healthOfferWord.text);
        healthOffer.sizeDelta = new Vector2(size.x + 36f, 46f);
    }

    // "+30 HEALTH" rises off the health bar.
    public void ShowHealthGain(int amount)
    {
        RectTransform bar = (RectTransform)healthBar.transform;
        TMP_Text text = CreateText(readoutLayer, "Health Gain", "+" + amount + " HEALTH", 30f, Vector2.zero);
        text.fontStyle = FontStyles.Bold;
        text.color = Palette.WordCrate;
        text.fontSharedMaterial = OutlineMaterial(text);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        Vector3[] corners = new Vector3[4];
        bar.GetWorldCorners(corners);
        Vector2 at = (Vector2)readoutLayer.InverseTransformPoint((corners[2] + corners[3]) * 0.5f) - readoutLayer.rect.center; // the bar's right end
        PlaceRect(text.rectTransform, Centre, LeftMiddle, at + new Vector2(16f, 0f), new Vector2(260f, 40f));
        text.alignment = TextAlignmentOptions.Left;
        StartCoroutine(RiseAndFade(text, text.rectTransform.anchoredPosition));
        StartCoroutine(PopIn(bar));
    }

    private void BuildHealthOffer()
    {
        Image pill = CreateImage("Health Word", readoutLayer, new Color(0.03f, 0.05f, 0.08f, 0.78f));
        healthOffer = pill.rectTransform;
        pill.raycastTarget = false;
        healthOfferGroup = pill.gameObject.AddComponent<CanvasGroup>();
        healthOfferGroup.blocksRaycasts = false;
        Image strip = CreateImage("Strip", healthOffer, Palette.WordCrate);
        strip.rectTransform.anchorMin = Vector2.zero;
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = LeftMiddle;
        strip.rectTransform.sizeDelta = new Vector2(5f, 0f);
        strip.rectTransform.anchoredPosition = Vector2.zero;
        healthOfferWord = CreateText(healthOffer, "Text", "", 26f, Vector2.zero);
        StretchToParent(healthOfferWord.rectTransform);
        healthOfferWord.rectTransform.offsetMin = new Vector2(18f, 0f);
        healthOfferWord.alignment = TextAlignmentOptions.Left;
        healthOfferWord.textWrappingMode = TextWrappingModes.NoWrap;
        healthOfferWord.color = Palette.WordCrate;
        healthOffer.gameObject.SetActive(false);
    }

    // Every frame while it is up: just under the health bar, fading in / out.
    private void UpdateHealthOffer()
    {
        if (healthOffer == null || !healthOffer.gameObject.activeSelf)
        {
            return;
        }
        float target = healthOfferOn && healthBar.gameObject.activeInHierarchy ? 1f : 0f;
        healthOfferGroup.alpha = Mathf.MoveTowards(healthOfferGroup.alpha, target, Time.unscaledDeltaTime * 5f);
        if (!healthOfferOn && healthOfferGroup.alpha <= 0f)
        {
            healthOffer.gameObject.SetActive(false);
            return;
        }

        // Under the bar, lined up with the "Health" label on its left.
        RectTransform bar = (RectTransform)healthBar.transform;
        Vector3[] corners = new Vector3[4];
        bar.GetWorldCorners(corners);
        Vector2 bottomLeft = (Vector2)readoutLayer.InverseTransformPoint(corners[0]) - readoutLayer.rect.center;
        float left = bottomLeft.x;
        if (HealthLabel != null)
        {
            RectTransform label = HealthLabel as RectTransform;
            if (label != null)
            {
                label.GetWorldCorners(corners);
                left = Mathf.Min(left, ((Vector2)readoutLayer.InverseTransformPoint(corners[0]) - readoutLayer.rect.center).x);
            }
        }
        float pop = Mathf.Clamp01((Time.unscaledTime - healthOfferShownAt) / 0.35f);
        float scale = Mathf.LerpUnclamped(1.25f, 1f, 1f - Mathf.Pow(1f - pop, 3f));
        healthOffer.anchorMin = healthOffer.anchorMax = Centre;
        healthOffer.pivot = new Vector2(0f, 1f);
        float top = bottomLeft.y - 14f;
        if (comboRewardBar != null && comboRewardBar.gameObject.activeInHierarchy && !float.IsNaN(EnergyRowBottom))
        {
            top = Mathf.Min(top, EnergyRowBottom - 10f);
        }
        healthOffer.anchoredPosition = new Vector2(left, top);
        healthOffer.localScale = new Vector3(scale, scale, 1f);
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
    private static readonly Vector2 ComboPlateSize = new Vector2(300f, 70f); // compact: ENERGY lives under the health bar now
    private const float ComboTextTop = 6f;
    private const float ComboFontSize = 44f;
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

    private bool targetWordAutoSized;

    // richText is the word with TextMeshPro colour tags (see Zombie.ColoredWord),
    // or "" when there is no target.
    public void SetTargetWord(string richText)
    {
        if (!targetWordAutoSized)
        {
            // Several words (one typed + up to three waiting) shrink to fit the box.
            targetWordAutoSized = true;
            targetWordText.fontSizeMax = targetWordText.fontSize;
            targetWordText.fontSizeMin = targetWordText.fontSize * 0.45f;
            targetWordText.enableAutoSizing = true;
            targetWordText.textWrappingMode = TextWrappingModes.NoWrap;
        }
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
        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave) return;
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
        // BuildLevelSelection();
        startPanel.SetActive(true);
        if (EasyTutorial.Active == null) RefreshDifficulty();
    }

    // ---- Typing difficulty (Start panel, under the "Show tips" toggle) ----
    // The row is the "Typing Difficulty" prefab placed under the Start panel
    // (see TypingDifficultySelector). The Easy scene has none.

    // Shows the difficulty picked now (TypingSettings.Difficulty).
    public void RefreshDifficulty()
    {
        if (difficultySelector != null)
        {
            difficultySelector.Refresh();
        }
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
                title, new Vector2((i - (GameManager.LevelCount - 1) * 0.5f) * 320f, -460f), () => GameManager.Instance.SelectLevel(levelIndex));
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
                else GameManager.Instance.LoadLevelSelectionMenu(); });
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
        AddWordPlate(label);
        return label;
    }

    // ---- Word plates ----
    // Every enemy word sits on a dark, see-through plate a little bigger than
    // the word, so it stands out from bright walls, sky, blasts or other words.
    // The plates live in their own layer at the BACK of the word layer (all
    // plates behind all words, and they scale with the words on phones).
    // WaveSpawner.LayoutLabels moves the words, then calls SyncWordPlates.

    private static readonly Color PlateColor = new Color(0.02f, 0.03f, 0.05f, 0.62f);
    private const float PlatePadX = 18f;  // word units added left + right together
    private const float PlatePadY = 6f;   // ...and top + bottom together

    private RectTransform plateLayer;
    private readonly List<TMP_Text> plateLabels = new List<TMP_Text>();
    private readonly List<Image> plates = new List<Image>();

    private void AddWordPlate(TMP_Text label)
    {
        if (plateLayer == null)
        {
            plateLayer = CreateRect("Word Plates", wordLayer);
            StretchToParent(plateLayer);
        }
        plateLayer.SetAsFirstSibling(); // behind every word

        Image plate = CreateImage("Word Plate", plateLayer, PlateColor);
        plate.enabled = false;
        plateLabels.Add(label);
        plates.Add(plate);
    }

    // A word drawn with no plate behind it (the barrels' words: a different
    // look from every zombie's word). Nothing happens if it has none.
    public void RemoveWordPlate(TMP_Text label)
    {
        int index = plateLabels.IndexOf(label);
        if (index < 0)
        {
            return;
        }
        if (plates[index] != null)
        {
            Destroy(plates[index].gameObject);
        }
        plates.RemoveAt(index);
        plateLabels.RemoveAt(index);
    }

    // Puts each plate under its word (same place, size and scale, plus padding)
    // and drops the plates of words that are gone.
    public void SyncWordPlates()
    {
        for (int i = plates.Count - 1; i >= 0; i--)
        {
            TMP_Text label = plateLabels[i];
            Image plate = plates[i];
            if (label == null)
            {
                if (plate != null)
                {
                    Destroy(plate.gameObject);
                }
                plates.RemoveAt(i);
                plateLabels.RemoveAt(i);
                continue;
            }

            bool show = label.isActiveAndEnabled && label.color.a > 0.05f && !string.IsNullOrEmpty(label.text);
            if (plate.enabled != show)
            {
                plate.enabled = show;
            }
            if (!show)
            {
                continue;
            }

            RectTransform word = label.rectTransform;
            RectTransform rect = plate.rectTransform;
            float scale = word.localScale.x;
            rect.anchorMin = word.anchorMin;
            rect.anchorMax = word.anchorMax;
            rect.pivot = word.pivot;
            rect.localScale = word.localScale;
            rect.sizeDelta = word.sizeDelta + new Vector2(PlatePadX, PlatePadY);
            // Grown evenly on every side, whatever the word's pivot.
            rect.anchoredPosition = word.anchoredPosition
                + new Vector2(PlatePadX * (word.pivot.x - 0.5f), PlatePadY * (word.pivot.y - 0.5f)) * scale;
            Color color = PlateColor;
            color.a *= label.color.a;
            plate.color = color;
        }
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
            wordMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Max(wordOutlineWidth, 0.3f)); // thick enough for coloured words
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
    private const float PowerNameWidth = 300f;
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
            powerPipStrips[row].gameObject.SetActive(false);
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
                ApplyPowerRowLook(row, 0f, 0f); // active effects stay bright after the use pulse ends
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
        bool active = IsPowerRowActive(row);
        bool ready = row >= FrenzyRow && GameManager.Instance.Powers.CanUseNow(row == RocketRow);
        flash = Mathf.Max(flash, active ? 0.7f : 0f);
        powerKeyCaps[row].color = Color.Lerp(Color.white, color, flash);
        powerNames[row].color = Color.Lerp(Color.white, color, flash);

        // No charges: the whole row is dimmed (but fully visible while it flashes).
        bool energyReady = row < FrenzyRow && GameManager.Instance.SkillEnergy >= (row == 0 ? Powers.LureCost : Powers.FreezeCost);
        float restAlpha = active || ready || energyReady || powerCharges[row] > 0 ? 1f : powerEmptyAlpha;
        powerRowGroups[row].alpha = Mathf.Lerp(restAlpha, 1f, flash);
    }

    private static bool IsPowerRowActive(int row)
    {
        Powers powers = GameManager.Instance != null ? GameManager.Instance.Powers : null;
        if (powers == null) return false;
        return row == 0 ? powers.IsAiming : row == 1 ? Powers.IsFrozen
            : row == FrenzyRow ? Powers.IsFrenzy : powers.RocketLoaded;
    }

    private static Color PowerColor(int row)
    {
        switch (row)
        {
            case (int)PowerKind.Lure: return Palette.LureCrate;
            case (int)PowerKind.Freeze: return Palette.FreezeCrate;
            case FrenzyRow: return Palette.WeaponFrenzy;
            default: return Palette.Rocket; // the RPG: amber
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
            int cost = row == 0 ? Powers.LureCost : row == 1 ? Powers.FreezeCost : row == FrenzyRow ? Powers.FrenzyCost : Powers.RocketCost;
            nameText.text += "  " + cost + " E";
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
            slot.gameObject.SetActive(false); // no inventory: the whole row is a direct skill button
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

    // ---- Shield (Tab), just above the powers (bottom-left) ----
    //   [TAB] SHIELD   [#]
    // a wider key cap, the name, then one pip per charge (like the powers).
    // While the shield is up: a bar under the pips drains, the row glows, and
    // a band in the shield's colour lines the screen edges (blinking at the end).

    private const float ShieldKeyWidth = 70f;
    private const float ShieldGlowThickness = 16f;
    private const float ShieldWarnFraction = 0.25f; // the glow blinks once this little time is left

    private RectTransform shieldRow;      // null until the first SetShield
    private CanvasGroup shieldRowGroup;
    private Image shieldKeyCap;
    private TMP_Text shieldName;
    private RectTransform shieldPipStrip;
    private readonly List<Image> shieldPips = new List<Image>();
    private RectTransform shieldBar;
    private RectTransform shieldBarFill;
    private CanvasGroup shieldGlow;
    private int shieldCharges;
    private float shieldFraction;         // 0 = down, 1 = just raised
    private float shieldPulseTimer;

    // charges: shields left this level; timeLeftFraction: 0 while it is down,
    // otherwise how much of it is left (1 = just raised).
    public void SetShield(int charges, int maxCharges, float timeLeftFraction)
    {
        if (shieldRow == null)
        {
            BuildShield();
        }

        shieldCharges = charges;
        shieldFraction = Mathf.Clamp01(timeLeftFraction);

        int shown = Mathf.Max(0, maxCharges);
        while (shieldPips.Count < shown)
        {
            Image pip = CreateImage("Pip", shieldPipStrip, EmptyPipColor);
            float x = PowerPipPadding + shieldPips.Count * (PowerPipSize + PowerPipGap);
            PlaceRect(pip.rectTransform, LeftMiddle, LeftMiddle, new Vector2(x, 0f), new Vector2(PowerPipSize, PowerPipSize));
            shieldPips.Add(pip);
        }
        for (int i = 0; i < shieldPips.Count; i++)
        {
            shieldPips[i].gameObject.SetActive(i < shown);
            shieldPips[i].color = i < charges ? Palette.Shield : EmptyPipColor;
        }
        float stripWidth = shown > 0 ? PowerPipPadding * 2f + shown * PowerPipSize + (shown - 1) * PowerPipGap : 0f;
        shieldPipStrip.sizeDelta = new Vector2(stripWidth, PowerPipSize + PowerPipPadding * 2f);

        bool up = shieldFraction > 0f;
        if (shieldBar.gameObject.activeSelf != up)
        {
            shieldBar.gameObject.SetActive(up);
            shieldGlow.gameObject.SetActive(up);
        }
        shieldBarFill.anchorMax = new Vector2(shieldFraction, 1f);
    }

    // The shield row flashes and grows for a moment (raised, or a hit blocked).
    public void PulseShield()
    {
        if (shieldRow == null)
        {
            BuildShield();
        }
        shieldPulseTimer = Mathf.Max(0.01f, powerPulseSeconds);
    }

    private void UpdateShieldLook()
    {
        if (shieldRow == null)
        {
            return;
        }

        float flash = 0f;
        float grow = 0f;
        if (shieldPulseTimer > 0f)
        {
            shieldPulseTimer -= Time.unscaledDeltaTime;
            float progress = 1f - Mathf.Max(0f, shieldPulseTimer) / Mathf.Max(0.01f, powerPulseSeconds);
            grow = Mathf.Sin(progress * Mathf.PI);
            flash = 1f - progress;
        }

        bool up = shieldFraction > 0f;
        float scale = Mathf.Lerp(1f, powerPulseScale, grow);
        shieldRow.localScale = new Vector3(scale, scale, 1f);
        Color lit = up ? Palette.Shield : Color.white;
        shieldKeyCap.color = Color.Lerp(lit, Palette.Shield, flash);
        shieldName.color = Color.Lerp(lit, Palette.Shield, flash);
        float restAlpha = shieldCharges > 0 || up ? 1f : powerEmptyAlpha;
        shieldRowGroup.alpha = Mathf.Lerp(restAlpha, 1f, flash);

        if (up)
        {
            // A slow breathing glow; it blinks fast when the shield is about to drop.
            float glow = 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
            if (shieldFraction < ShieldWarnFraction && Mathf.Repeat(Time.unscaledTime * 5f, 1f) < 0.4f)
            {
                glow = 0.15f;
            }
            shieldGlow.alpha = Mathf.Max(glow, flash);
        }
    }

    // ShieldRow (above the powers) > KeyCap (+ Key), Name, Pips, Bar (+ Fill);
    // ShieldGlow (four edge bands + a faint tint) at the back of the readouts.
    private void BuildShield()
    {
        if (powersPanel == null)
        {
            BuildPowers();
        }
        float powersHeight = PowerCount * PowerRowHeight + (PowerCount - 1) * PowerRowGap;
        float y = BottomRowY() + powersHeight + PowerRowGap * 2f + PowerRowHeight * 0.5f;

        shieldRow = CreateRect("SHIELD", readoutLayer);
        PlaceRect(shieldRow, BottomLeft, LeftMiddle, new Vector2(powersMargin, y), new Vector2(PowerPipsX + 120f, PowerRowHeight));
        shieldRowGroup = shieldRow.gameObject.AddComponent<CanvasGroup>();

        shieldKeyCap = CreateImage("KeyCap", shieldRow, Color.white);
        PlaceRect(shieldKeyCap.rectTransform, LeftMiddle, LeftMiddle, Vector2.zero, new Vector2(ShieldKeyWidth, PowerKeySize));
        TMP_Text keyText = CreateText(shieldKeyCap.rectTransform, "Key", "TAB", 24f, Vector2.zero);
        StretchToParent(keyText.rectTransform);
        keyText.color = Color.black;
        keyText.fontStyle = FontStyles.Bold;

        shieldName = CreateText(shieldRow, "Name", "SHIELD", 30f, Vector2.zero);
        PlaceRect(shieldName.rectTransform, LeftMiddle, LeftMiddle, new Vector2(ShieldKeyWidth + 14f, 0f),
            new Vector2(PowerPipsX - ShieldKeyWidth - 14f, PowerRowHeight));
        shieldName.alignment = TextAlignmentOptions.Left;
        shieldName.fontStyle = FontStyles.Bold;
        shieldName.textWrappingMode = TextWrappingModes.NoWrap;
        shieldName.fontSharedMaterial = OutlineMaterial(shieldName);

        Image strip = CreateImage("Pips", shieldRow, new Color(0f, 0f, 0f, 0.55f));
        PlaceRect(strip.rectTransform, LeftMiddle, LeftMiddle, new Vector2(PowerPipsX, 0f), Vector2.zero);
        shieldPipStrip = strip.rectTransform;

        // The time left, under the row's name and pips.
        Image bar = CreateImage("Bar", shieldRow, new Color(0f, 0f, 0f, 0.6f));
        PlaceRect(bar.rectTransform, LeftMiddle, new Vector2(0f, 1f), new Vector2(ShieldKeyWidth + 14f, -PowerRowHeight * 0.5f - 2f),
            new Vector2(PowerPipsX + 40f - ShieldKeyWidth - 14f, WeaponHoldBarHeight));
        Image fill = CreateImage("Fill", bar.rectTransform, Palette.Shield);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        shieldBar = bar.rectTransform;
        shieldBarFill = fill.rectTransform;
        bar.gameObject.SetActive(false);

        // The glow: under every readout, over the enemy words only at the very edges.
        RectTransform glow = CreateRect("ShieldGlow", readoutLayer);
        StretchToParent(glow);
        glow.SetAsFirstSibling();
        shieldGlow = glow.gameObject.AddComponent<CanvasGroup>();
        shieldGlow.blocksRaycasts = false;
        shieldGlow.interactable = false;
        Image tint = CreateImage("Tint", glow, new Color(Palette.Shield.r, Palette.Shield.g, Palette.Shield.b, 0.06f));
        StretchToParent(tint.rectTransform);
        CreateEdgeBand(glow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, ShieldGlowThickness));
        CreateEdgeBand(glow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, ShieldGlowThickness));
        CreateEdgeBand(glow, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(ShieldGlowThickness, 0f));
        CreateEdgeBand(glow, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(ShieldGlowThickness, 0f));
        glow.gameObject.SetActive(false);
    }

    // ---- The shield word box (after Tab, see ShieldWord) ----
    //   TYPE IT TO RAISE YOUR SHIELD   (Tab: cancel)
    //               protect
    //   [=========================-----]   <- time left
    // Bottom-centre, above the hint box, so it never covers the enemies' words.

    private const float ShieldPromptBottom = 250f;      // canvas units above the bottom edge
    private static readonly Vector2 ShieldPromptSize = new Vector2(620f, 132f);

    private RectTransform shieldPrompt;   // null until Tab is first pressed
    private TMP_Text shieldPromptWord;
    private RectTransform shieldPromptFill;

    // The word's text (ShieldWord.Label).
    public TMP_Text ShieldPromptLabel
    {
        get
        {
            if (shieldPrompt == null)
            {
                BuildShieldPrompt();
            }
            return shieldPromptWord;
        }
    }

    public void ShowShieldPrompt()
    {
        if (shieldPrompt == null)
        {
            BuildShieldPrompt();
        }
        shieldPrompt.gameObject.SetActive(true);
        shieldPrompt.SetAsLastSibling();
    }

    public void HideShieldPrompt()
    {
        if (shieldPrompt != null)
        {
            shieldPrompt.gameObject.SetActive(false);
        }
    }

    public void SetShieldPromptWord(string richText)
    {
        ShieldPromptLabel.text = richText;
    }

    // fraction: time left to type it (1 = all of it).
    public void SetShieldPromptTime(float fraction)
    {
        if (shieldPrompt == null)
        {
            BuildShieldPrompt();
        }
        shieldPromptFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }

    // ShieldPrompt (dark box, shield-coloured strip and border) > Title, Word, Bar (+ Fill).
    private void BuildShieldPrompt()
    {
        Image border = CreateImage("ShieldPrompt", readoutLayer, Palette.Shield);
        shieldPrompt = border.rectTransform;
        PlaceRect(shieldPrompt, BottomCentre, BottomCentre, new Vector2(0f, ShieldPromptBottom), ShieldPromptSize);

        Image box = CreateImage("Box", shieldPrompt, new Color(0.03f, 0.05f, 0.08f, 0.92f));
        StretchToParent(box.rectTransform);
        box.rectTransform.offsetMin = new Vector2(3f, 3f);
        box.rectTransform.offsetMax = new Vector2(-3f, -3f);

        TMP_Text title = CreateText(shieldPrompt, "Title",
            "TYPE IT TO RAISE YOUR SHIELD   <size=75%><color=#9AA0AA>Tab: cancel</color></size>", 24f, Vector2.zero);
        PlaceRect(title.rectTransform, TopCentre, TopCentre, new Vector2(0f, -10f), new Vector2(ShieldPromptSize.x, 32f));
        title.color = Palette.Shield;
        title.fontStyle = FontStyles.Bold;

        shieldPromptWord = CreateText(shieldPrompt, "Word", "", 56f, Vector2.zero);
        PlaceRect(shieldPromptWord.rectTransform, Centre, Centre, new Vector2(0f, -8f), new Vector2(ShieldPromptSize.x - 20f, 64f));
        shieldPromptWord.fontStyle = FontStyles.Bold;
        shieldPromptWord.textWrappingMode = TextWrappingModes.NoWrap;
        shieldPromptWord.fontSharedMaterial = OutlineMaterial(shieldPromptWord);

        Image bar = CreateImage("Bar", shieldPrompt, new Color(0f, 0f, 0f, 0.7f));
        PlaceRect(bar.rectTransform, BottomCentre, BottomCentre, new Vector2(0f, 10f), new Vector2(ShieldPromptSize.x - 40f, 8f));
        Image fill = CreateImage("Fill", bar.rectTransform, Palette.Shield);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        shieldPromptFill = fill.rectTransform;

        shieldPrompt.gameObject.SetActive(false);
    }

    // One band along a screen edge (anchored from anchorMin to anchorMax).
    private static void CreateEdgeBand(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        Image band = CreateImage("Edge", parent, Palette.Shield);
        RectTransform rect = band.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
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
        // The dashed arc already shows where the bomb will land, so no text box
        // covers the view any more (the guided intro draws the arrow keys once).
        show = false;
        // In the tutorial only the LURE lesson (3) shows how to aim (demo and practice).
        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave && EasyTutorial.Active.CurrentLesson != 3) show = false;
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
        energyNumber.text = GameManager.Instance != null ? GameManager.Instance.SkillEnergy.ToString() : "0";
        comboRewardFillImage.color = color;
    }

    // Puts the ENERGY row just under the health bar (every frame: the bar's
    // length changes with the window, see PlaceComboPlate).
    private void PlaceEnergyRow()
    {
        if (comboRewardBar == null || healthBar == null)
        {
            return;
        }
        RectTransform bar = (RectTransform)healthBar.transform;
        Vector3[] corners = new Vector3[4];
        bar.GetWorldCorners(corners);
        Vector2 bottomLeft = (Vector2)readoutLayer.InverseTransformPoint(corners[0]) - readoutLayer.rect.center;
        Vector2 topRight = (Vector2)readoutLayer.InverseTransformPoint(corners[2]) - readoutLayer.rect.center;
        // The bar: exactly the health bar's length and thickness, one row lower.
        float barHeight = topRight.y - bottomLeft.y;
        float barMiddle = (bottomLeft.y + topRight.y) * 0.5f;
        float y = barMiddle - barHeight - EnergyRowGap;
        comboRewardBar.anchoredPosition = new Vector2(bottomLeft.x, y);
        comboRewardBar.sizeDelta = new Vector2(topRight.x - bottomLeft.x, barHeight);
        energyNumber.fontSize = Mathf.Max(18f, barHeight * 0.78f);

        // The label: a twin of the scene's "Health" label (same box, font, size, colour), one row lower.
        TMP_Text healthText = HealthLabel != null ? HealthLabel.GetComponent<TMP_Text>() : null;
        if (healthText != null)
        {
            RectTransform from = healthText.rectTransform;
            Vector3[] labelCorners = new Vector3[4];
            from.GetWorldCorners(labelCorners);
            Vector2 labelMin = (Vector2)readoutLayer.InverseTransformPoint(labelCorners[0]) - readoutLayer.rect.center;
            Vector2 labelMax = (Vector2)readoutLayer.InverseTransformPoint(labelCorners[2]) - readoutLayer.rect.center;
            float labelMiddle = (labelMin.y + labelMax.y) * 0.5f;
            comboRewardText.fontSize = healthText.fontSize * from.lossyScale.y / readoutLayer.lossyScale.y;
            comboRewardText.fontStyle = healthText.fontStyle;
            comboRewardText.font = healthText.font;
            comboRewardText.horizontalAlignment = healthText.horizontalAlignment;
            comboRewardText.verticalAlignment = healthText.verticalAlignment;
            comboRewardText.color = healthText.color;
            PlaceRect(comboRewardText.rectTransform, Centre, Centre,
                new Vector2((labelMin.x + labelMax.x) * 0.5f, labelMiddle - (barMiddle - y)),
                labelMax - labelMin);
        }
        else
        {
            PlaceRect(comboRewardText.rectTransform, Centre, new Vector2(1f, 0.5f), new Vector2(bottomLeft.x - 14f, y), new Vector2(200f, barHeight + 10f));
        }
        EnergyRowBottom = y - barHeight * 0.5f;
        PlaceTopLeftPlate(bottomLeft, topRight);
    }

    private Image topLeftPlate; // a dark backing behind health and ENERGY: readable over bright walls in the map view

    private void PlaceTopLeftPlate(Vector2 barBottomLeft, Vector2 barTopRight)
    {
        if (topLeftPlate == null)
        {
            topLeftPlate = CreateImage("Top Left Plate", readoutLayer, new Color(0.02f, 0.03f, 0.05f, 0.5f));
            topLeftPlate.raycastTarget = false;
            topLeftPlate.rectTransform.SetAsFirstSibling(); // behind every readout
        }
        bool show = healthBar.gameObject.activeInHierarchy;
        if (topLeftPlate.gameObject.activeSelf != show)
        {
            topLeftPlate.gameObject.SetActive(show);
        }
        if (!show)
        {
            return;
        }
        float left = barBottomLeft.x; // the labels' left end is measured below
        // ...and the "Health" text's own left end, wherever it is aligned in its box.
        TMP_Text labelText = HealthLabel != null ? HealthLabel.GetComponent<TMP_Text>() : null;
        if (labelText != null && labelText.gameObject.activeInHierarchy)
        {
            Vector3[] corners = new Vector3[4];
            labelText.rectTransform.GetWorldCorners(corners);
            float boxLeft = ((Vector2)readoutLayer.InverseTransformPoint(corners[0]) - readoutLayer.rect.center).x;
            float boxRight = ((Vector2)readoutLayer.InverseTransformPoint(corners[2]) - readoutLayer.rect.center).x;
            float textWidth = labelText.preferredWidth * labelText.rectTransform.lossyScale.x / readoutLayer.lossyScale.x;
            HorizontalAlignmentOptions align = labelText.horizontalAlignment;
            float labelLeft = align == HorizontalAlignmentOptions.Left ? boxLeft
                : align == HorizontalAlignmentOptions.Right ? boxRight - textWidth
                : (boxLeft + boxRight - textWidth) * 0.5f;
            left = Mathf.Min(left, labelLeft);
        }
        float bottom = comboRewardBar.gameObject.activeInHierarchy ? EnergyRowBottom : barBottomLeft.y - 4f;
        RectTransform rect = topLeftPlate.rectTransform;
        rect.anchorMin = rect.anchorMax = Centre;
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(left - 16f, bottom - 8f);
        rect.sizeDelta = new Vector2(barTopRight.x + 16f - (left - 16f), barTopRight.y + 14f - (bottom - 8f));
    }

    private const float EnergyRowGap = 14f;     // between the health bar and the ENERGY row

    // The bottom of the ENERGY row (canvas units from the middle), for what goes under it.
    private float EnergyRowBottom = float.NaN;

    private void UpdateComboRewardBar()
    {
        PlaceEnergyRow();
        if (comboRewardFill == null || comboRewardShown == comboRewardTarget)
        {
            return;
        }

        // Slide toward the real progress instead of jumping there.
        comboRewardShown = Mathf.MoveTowards(comboRewardShown, comboRewardTarget, ComboBarSpeed * Time.unscaledDeltaTime);
        comboRewardFill.anchorMax = new Vector2(comboRewardShown, 1f);
    }

    // ENERGY row, under the health bar (top-left): "ENERGY 12" ending just
    // left of a thin teal bar as long as the health bar.
    //   Health  [==============]
    //  ENERGY 12 [=======-------]
    private void BuildComboReward()
    {
        comboRewardText = CreateText(readoutLayer, "Energy Label", "Energy", 32f, Vector2.zero);
        comboRewardText.alignment = TextAlignmentOptions.Right;
        comboRewardText.textWrappingMode = TextWrappingModes.NoWrap;
        comboRewardText.color = Color.white;

        Image bar = CreateImage("EnergyBar", readoutLayer, new Color(0.2f, 0.21f, 0.24f, 0.9f));
        comboRewardBar = bar.rectTransform;
        PlaceRect(comboRewardBar, Centre, LeftMiddle, Vector2.zero, ComboBarSize);

        // The fill is anchored to the bar's left edge; its right anchor is the progress.
        comboRewardFillImage = CreateImage("Fill", comboRewardBar, Yellow);
        comboRewardFill = comboRewardFillImage.rectTransform;
        comboRewardFill.anchorMin = Vector2.zero;
        comboRewardFill.anchorMax = new Vector2(0f, 1f);
        comboRewardFill.offsetMin = Vector2.zero;
        comboRewardFill.offsetMax = Vector2.zero;
        comboRewardShown = 0f;

        // How much energy, inside the bar at its right end.
        energyNumber = CreateText(comboRewardBar, "Energy Number", "0", 22f, Vector2.zero);
        StretchToParent(energyNumber.rectTransform);
        energyNumber.rectTransform.offsetMax = new Vector2(-10f, 0f);
        energyNumber.alignment = TextAlignmentOptions.Right;
        energyNumber.fontStyle = FontStyles.Bold;
        energyNumber.color = Color.white;
        energyNumber.fontSharedMaterial = OutlineMaterial(energyNumber);
    }

    private TMP_Text energyNumber; // the energy, inside the ENERGY bar

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
        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave) return;
        if (string.IsNullOrEmpty(text))
        {
            hintTimeLeft = 0f;
            return;
        }
        if (GuidedIntroView.CardVisible)
        {
            return; // the guided intro's card sits in the same place and says what matters now
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
        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave && text != null && text.StartsWith("+")) return;
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
        if (EasyTutorial.Active != null && won) { HideAllPanels(); EasyTutorial.Active.ShowCompletion(); return; }
        if (stats == null)
        {
            stats = new RunStats(); // nothing was counted: show zeros rather than nothing
        }

        // The game is over: hide the enemies' words so they do not show through the panel.
        wordLayer.gameObject.SetActive(false);
        if (shieldGlow != null)
        {
            shieldGlow.gameObject.SetActive(false);
        }
        HideShieldPrompt();
        if (healthOffer != null)
        {
            healthOffer.gameObject.SetActive(false);
        }

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
        if (!won && Checkpoint.Has(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
        {
            message.text += "\n<color=#40DBCC><b>ENTER</b>  continue from Wave " + (Checkpoint.WaveIndex + 1) + "</color>";
        }
        if (!won)
        {
            // The panel's button goes on from the checkpoint when there is one.
            bool canContinue = Checkpoint.Has(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            foreach (TMP_Text label in lostPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text == "Restart" || label.text == "Continue")
                {
                    label.text = canContinue ? "Continue" : "Restart";
                }
            }
        }
    }

    // Four lines, e.g.
    //   Score 12750
    //   Accuracy 97.5%    Speed 62 WPM
    //   Kills 50    Best combo x51    Biggest blast x7
    //   Time 4:12    Typing HARD    Adaptive ON (58 WPM)
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
            + StatLabel + "Time</color>  <b>" + time + "</b>     "
            + StatLabel + "Typing</color>  <b>" + TypingSettings.Name(TypingSettings.Difficulty) + "</b>     "
            + StatLabel + "Adaptive</color>  <b>" + AdaptiveText() + "</b>";
    }

    // "ON (42 WPM)" once the adaptive difficulty has measured the player, else "ON" / "OFF".
    private static string AdaptiveText()
    {
        if (!DynamicDifficulty.Enabled)
        {
            return "OFF";
        }
        return DynamicDifficulty.Calibrated ? "ON (" + Mathf.RoundToInt(DynamicDifficulty.EstimatedWpm) + " WPM)" : "ON";
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
