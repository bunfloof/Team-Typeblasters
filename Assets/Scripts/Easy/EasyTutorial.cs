using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attached only to Easy: deterministic introductions, hands-on objectives and retry supplies.
public class EasyTutorial : MonoBehaviour
{
    public static EasyTutorial Active { get; private set; }
    public bool LessonComplete { get; private set; } = true;
    public int CurrentLesson => wave;
    public bool NeedsRocketTarget => wave == 4 && !LessonComplete;
    private int wave;
    private bool purchased;
    private float nextRefill;
    private TMP_Text objective;

    private static readonly string[] Lessons =
    {
        "LURE BOMB / Normal + Runner\nType enemy words. HOLD 1 to aim, RELEASE to throw (phone: BOMB).",
        "FREEZE / Armored + Explosive\nArmored needs two words; red enemies explode. Press 2 / FREEZE.",
        "FRENZY / Shifter + Ghost\nShifters change words; ghosts hide them. HOLD 3 to BUY, then TAP 3 / FRENZY to use.",
        "RPG / Splitter + Splitlings + Spitter\nHOLD 4 to BUY, TAP 4 / RPG to load, then FINISH a word to fire. Type acid orbs too.",
        "SHIELD / Brute + Word Chain\nBrute phrases include SPACES. Type the longer linked word. TAB / SHIELD, then type its word.",
        "FINAL PRACTICE / Boss\nUse everything you learned. Type boss targets; tools are available again."
    };

    private void OnEnable()
    {
        Active = this;
        Powers.ActionPerformed += OnPower;
    }

    private void OnDisable()
    {
        Powers.ActionPerformed -= OnPower;
        if (Active == this) Active = null;
    }

    public static ZombieKind[] Plan(int wave)
    {
        switch (wave)
        {
            case 1: return new[] { ZombieKind.Normal, ZombieKind.Normal, ZombieKind.Runner };
            case 2: return new[] { ZombieKind.Armored, ZombieKind.Normal, ZombieKind.Explosive };
            case 3: return new[] { ZombieKind.Shifter, ZombieKind.Ghost, ZombieKind.Normal };
            case 4: return new[] { ZombieKind.Splitter, ZombieKind.Splitling, ZombieKind.Spitter };
            default: return new[] { ZombieKind.Normal, ZombieKind.Normal, ZombieKind.Brute };
        }
    }

    public void BeginLesson(int number)
    {
        wave = number;
        purchased = false;
        LessonComplete = wave > 5;
        GameManager.Instance.GrantTrainingCombo(GameManager.Instance.Combo + 25);
        Refill();
        if (objective == null) BuildObjective();
        objective.transform.parent.gameObject.SetActive(true);
        RefreshObjective();
        AnalyticsEvent e = Analytics.NewEvent("tutorial_lesson_start");
        e.Add("lesson", wave);
        e.Add("combo_allowance", 25);
        Analytics.Send(e);
    }

    private void Refill()
    {
        GameManager.Instance.GrantTrainingCombo(25);
        GameManager.Instance.Powers.RefillTrainingSupplies();
        nextRefill = Time.time + 3f;
    }

    // Mistyping or buying the wrong weapon must never permanently block a lesson.
    public void EnsureSupplies()
    {
        if (!LessonComplete && Time.time >= nextRefill) Refill();
    }

    private void OnPower(string action, string item)
    {
        if (wave < 1 || wave > 5 || LessonComplete) return;
        string required = new[] { "lure", "freeze", "frenzy", "rpg", "shield" }[wave - 1];
        if (item != required) return;
        if (action == "buy") purchased = true;
        bool used = wave == 1 ? action == "throw" : wave == 4 ? action == "fire" : action == "use";
        if (!used || ((wave == 3 || wave == 4) && !purchased)) return;
        LessonComplete = true;
        RefreshObjective();
        AnalyticsEvent e = Analytics.NewEvent("tutorial_lesson_complete");
        e.Add("lesson", wave);
        e.Add("tool", item);
        Analytics.Send(e);
    }

    public void EndLesson()
    {
        wave = 0;
        if (objective != null) objective.transform.parent.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (objective != null)
            objective.transform.parent.gameObject.SetActive(wave > 0 && GameManager.Instance.State == GameState.Playing);
    }

    private void RefreshObjective()
    {
        objective.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, wave > 5 ? -235f : -110f);
        objective.text = "<b>TRAINING " + wave + "/6</b>  |  +25 COMBO + TOOL SUPPLIES\n"
            + Lessons[Mathf.Clamp(wave - 1, 0, Lessons.Length - 1)]
            + (wave > 5 ? "" : LessonComplete ? "\n<color=#85FFB3>TOOL DONE - clear remaining enemies.</color>"
                : "\n<color=#FFD98C>REQUIRED: use this tool to unlock the next station. Supplies refill while learning.</color>");
    }

    private void BuildObjective()
    {
        // The word layer's parent is the camera's usable HUD area on phones,
        // so the objective stays above the native keyboard as the view resizes.
        Transform playArea = FindFirstObjectByType<HUD>().WordLayer.parent;
        GameObject panel = new GameObject("Training Objective", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(playArea, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0.18f, 1f);
        rect.anchorMax = new Vector2(0.82f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -110f);
        rect.sizeDelta = new Vector2(0f, 164f);
        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.025f, 0.08f, 0.12f, 0.94f);
        bg.raycastTarget = false;
        GameObject label = new GameObject("Objective", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        objective = label.GetComponent<TMP_Text>();
        objective.rectTransform.anchorMin = Vector2.zero;
        objective.rectTransform.anchorMax = Vector2.one;
        objective.rectTransform.offsetMin = new Vector2(12f, 8f);
        objective.rectTransform.offsetMax = new Vector2(-12f, -8f);
        objective.fontSize = 28f;
        objective.enableAutoSizing = true;
        objective.fontSizeMin = 22f;
        objective.fontSizeMax = 28f;
        objective.alignment = TextAlignmentOptions.Center;
        objective.raycastTarget = false;
    }
}
