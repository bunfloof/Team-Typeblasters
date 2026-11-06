// RunStats.cs
// ---------------------------------------------------------------------------
// The numbers of one play-through, shown on the "You survived" / "You died"
// screen. GameManager counts them while you play.
// ---------------------------------------------------------------------------
using UnityEngine;

public class RunStats
{
    public int Score;
    public int Kills;
    public int BestCombo;
    public int BiggestMultiKill;  // most enemies killed by one blast / chain reaction
    public int CorrectKeys;
    public int WrongKeys;
    public float Seconds;         // time spent playing (pauses not counted)

    // 0 to 100.
    public float Accuracy
    {
        get
        {
            int total = CorrectKeys + WrongKeys;
            return total == 0 ? 0f : 100f * CorrectKeys / total;
        }
    }

    // Typing speed: by convention one "word" is 5 correct keys.
    public float WordsPerMinute
    {
        get
        {
            if (Seconds < 1f)
            {
                return 0f;
            }
            return (CorrectKeys / 5f) / (Seconds / 60f);
        }
    }

    public bool HasTypingAttempts
    {
        get { return CorrectKeys + WrongKeys > 0; }
    }
}
