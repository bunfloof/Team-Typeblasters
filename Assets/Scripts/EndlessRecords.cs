// EndlessRecords.cs
// ---------------------------------------------------------------------------
// Stores the player's personal best records for Endless Mode.
//
// Records:
//   - Best Wave: the highest wave reached in a single Endless run.
//   - Best Score: the highest score achieved in a single Endless run.
//
// STORAGE:
//   Uses Unity PlayerPrefs to save records between game sessions.
//   Records are stored locally and are not shared between devices or players.
//
// SubmitRun returns an EndlessRecordSnapshot containing:
//   - BestWave / BestScore: the updated personal bests.
//   - NewBestWave / NewBestScore: whether this run established a new record.
// ---------------------------------------------------------------------------
using UnityEngine;
public sealed class EndlessRecordSnapshot
{
    public int BestWave { get; private set; }
    public int BestScore { get; private set; }
    public bool NewBestWave { get; private set; }
    public bool NewBestScore { get; private set; }

    public EndlessRecordSnapshot(
        int bestWave, int bestScore,
        bool newBestWave, bool newBestScore)
    {
        BestWave = bestWave;
        BestScore = bestScore;
        NewBestWave = newBestWave;
        NewBestScore = newBestScore;
    }
}

public static class EndlessRecords
{
    private const string BestWaveKey = "Typeblaster.Endless.BestWave";
    private const string BestScoreKey = "Typeblaster.Endless.BestScore";

    public static int BestWave
    {
        get { return PlayerPrefs.GetInt(BestWaveKey, 0); }
    }

    public static int BestScore
    {
        get { return PlayerPrefs.GetInt(BestScoreKey, 0); }
    }

    public static EndlessRecordSnapshot SubmitRun(int wave, int score)
    {
        int previousBestWave = BestWave;
        int previousBestScore = BestScore;

        bool newBestWave = wave > previousBestWave;
        bool newBestScore = score > previousBestScore;

        if (newBestWave)
        {
            PlayerPrefs.SetInt(BestWaveKey, wave);
        }

        if (newBestScore)
        {
            PlayerPrefs.SetInt(BestScoreKey, score);
        }

        if (newBestWave || newBestScore)
        {
            PlayerPrefs.Save();
        }

        return new EndlessRecordSnapshot(
            Mathf.Max(previousBestWave, wave),
            Mathf.Max(previousBestScore, score),
            newBestWave,
            newBestScore
        );
    }
}
