// EndlessLeaderboard.cs
// ---------------------------------------------------------------------------
// The ENDLESS leaderboard: the best runs on this computer, ranked by the wave
// reached (then the score), each with who played it and how fast they typed.
//
//   PlayerProfile.Name           who is playing (asked in the menu; a run
//                                played without a name is not recorded)
//   EndlessLeaderboard.Submit()  GameManager, when an Endless run ends
//   EndlessLeaderboard.Entries   the board, best first (MainMenu shows it)
//
// Kept in PlayerPrefs (the browser's storage on the web build), so it is the
// board of one browser on one computer: everyone who plays on the lab machine
// shares it. There is no server.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PlayerProfile
{
    public const int MaxLength = 12;
    private const string NameKey = "Typeblaster.PlayerName";

    public static bool HasName
    {
        get { return !string.IsNullOrEmpty(PlayerPrefs.GetString(NameKey, "")); }
    }

    // "PLAYER" until a name has been typed.
    public static string Name
    {
        get
        {
            string name = PlayerPrefs.GetString(NameKey, "");
            return string.IsNullOrEmpty(name) ? "PLAYER" : name;
        }
        set
        {
            PlayerPrefs.SetString(NameKey, Clean(value));
            PlayerPrefs.Save();
        }
    }

    // Letters, digits, spaces, - and _ only, at most MaxLength, in capitals.
    public static string Clean(string name)
    {
        if (name == null) return "";
        System.Text.StringBuilder clean = new System.Text.StringBuilder();
        foreach (char c in name.Trim())
        {
            if (clean.Length >= MaxLength) break;
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
            {
                clean.Append(char.ToUpperInvariant(c));
            }
        }
        return clean.ToString().Trim();
    }

    public static bool Allowed(char c)
    {
        return char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';
    }
}

[System.Serializable]
public class LeaderboardEntry
{
    public string name;
    public int wave;
    public int wpm;
    public int score;
}

public static class EndlessLeaderboard
{
    public const int Size = 10;
    private const string BoardKey = "Typeblaster.Endless.Leaderboard";

    [System.Serializable]
    private class Board
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    // Best first.
    public static List<LeaderboardEntry> Entries
    {
        get { return Load().entries; }
    }

    // Adds a finished run. Returns its rank (1 = top), or 0 if it did not make the board.
    public static int Submit(string name, int wave, int wpm, int score)
    {
        if (wave <= 0)
        {
            return 0;
        }
        Board board = Load();
        LeaderboardEntry entry = new LeaderboardEntry { name = name, wave = wave, wpm = Mathf.Max(0, wpm), score = Mathf.Max(0, score) };
        board.entries.Add(entry);
        board.entries.Sort(Compare);
        int rank = board.entries.IndexOf(entry) + 1;
        if (board.entries.Count > Size)
        {
            board.entries.RemoveRange(Size, board.entries.Count - Size);
        }
        PlayerPrefs.SetString(BoardKey, JsonUtility.ToJson(board));
        PlayerPrefs.Save();
        return rank <= Size ? rank : 0;
    }

    // The higher wave first; the same wave: the higher score; then the faster typist.
    private static int Compare(LeaderboardEntry a, LeaderboardEntry b)
    {
        if (a.wave != b.wave) return b.wave.CompareTo(a.wave);
        if (a.score != b.score) return b.score.CompareTo(a.score);
        return b.wpm.CompareTo(a.wpm);
    }

    private static Board Load()
    {
        string json = PlayerPrefs.GetString(BoardKey, "");
        Board board = null;
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                board = JsonUtility.FromJson<Board>(json);
            }
            catch (System.Exception)
            {
                board = null; // a damaged board starts again rather than breaking the menu
            }
        }
        if (board == null || board.entries == null)
        {
            board = new Board();
        }
        board.entries.Sort(Compare);
        return board;
    }
}
