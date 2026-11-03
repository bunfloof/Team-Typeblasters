// TypingDifficultySelector.cs
// ---------------------------------------------------------------------------
// The TYPING DIFFICULTY row on the Start panel of Normal and Hard
// (prefab: Assets/Prefabs/UI/Typing Difficulty.prefab):
//
//   TYPING DIFFICULTY
//   [ EASY ] [ MEDIUM ] [ HARD ] [ EXTREME ]
//
// Each button's On Click calls Pick with its number, set in the prefab's
// Inspector: 0 = EASY, 1 = MEDIUM, 2 = HARD, 3 = EXTREME (the order of the
// TypingDifficulty enum). LEFT / RIGHT on the keyboard do the same (see
// TypingController). GameManager.SetTypingDifficulty saves the choice and
// calls HUD.RefreshDifficulty, which calls Refresh here: the picked button is
// filled in its colour, the others are dark with a coloured name.
// The Easy scene has no row: its tutorial always types on EASY.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TypingDifficultySelector : MonoBehaviour
{
    private static readonly Color IdleColor = new Color(0.17f, 0.18f, 0.21f);

    [Tooltip("The button backgrounds, in the order EASY, MEDIUM, HARD, EXTREME.")]
    [SerializeField] private Image[] buttons = new Image[TypingSettings.DifficultyCount];
    [Tooltip("The names on those buttons, in the same order.")]
    [SerializeField] private TMP_Text[] names = new TMP_Text[TypingSettings.DifficultyCount];

    // Called by the buttons: 0 = EASY ... 3 = EXTREME.
    public void Pick(int level)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetTypingDifficulty((TypingDifficulty)level);
        }
    }

    // Shows the difficulty picked now (TypingSettings.Difficulty).
    public void Refresh()
    {
        TypingDifficulty picked = TypingSettings.Difficulty;
        for (int i = 0; i < buttons.Length; i++)
        {
            TypingDifficulty level = (TypingDifficulty)i;
            bool selected = level == picked;
            buttons[i].color = selected ? TypingSettings.Tint(level) : IdleColor;
            names[i].color = selected ? Color.black : TypingSettings.Tint(level);
        }
    }
}
