// QuizBank.cs
// ---------------------------------------------------------------------------
// The questions the boss asks, Typing of the Dead style. When all 4 of the
// boss's limbs are off it is stunned and asks a question: the question
// appears in big letters and 3 answer words float in front of the boss. The
// player TYPES one of them:
//   - the right answer -> critical hit on the boss,
//   - a wrong answer   -> the boss heals.
// The player only has about 10 seconds and zombies may still be around, so
// every question is SHORT and can be read at a glance.
//
// Boss calls QuizBank.Pick(usedFirstLetters) and gets a QuizQuestion (see
// QuizQuestion.cs) with its 3 answers already shuffled into a random order.
//
// HOW TO ADD A QUESTION: add one line to the Questions list below, like
//
//     Q("WHAT DO ZOMBIES CRAVE?", "BRAINS", "SALAD", "TOFU"),
//         the question              RIGHT    wrong    wrong
//
// The FIRST answer is always the right one (the game shuffles them later).
// Rules for a question (when you press Play, CheckQuestions below prints a red
// error in the Console naming any question that breaks one of them):
//   - the question: UPPERCASE, at most 40 characters, ends with "?", only
//     plain keyboard characters (no curly quotes, long dashes or emoji, which
//     the font cannot show), and never '<' or '>' (TextMeshPro would read
//     them as a rich-text tag);
//   - the answers: exactly 3, each ONE word of UPPERCASE letters A-Z only
//     (no digits, spaces, apostrophes or hyphens), 3 to 10 letters long;
//   - the 3 answers start with 3 DIFFERENT letters: the first key the player
//     types chooses the answer, so two answers may never share a first letter.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class QuizBank
{
    private const int MaxQuestionLength = 40; // characters; the question is shown big
    private const int MinAnswerLength = 3;    // letters
    private const int MaxAnswerLength = 10;   // letters

    // Every question. The FIRST answer of each line is the right one.
    private static readonly QuizQuestion[] Questions =
    {
        // ---- Zombies and monsters (the silly ones) ----
        Q("WHAT DO ZOMBIES CRAVE?", "BRAINS", "SALAD", "TOFU"),
        Q("WHAT DO VAMPIRES DRINK?", "BLOOD", "MILK", "SODA"),
        Q("WHAT DO VAMPIRES HATE?", "GARLIC", "CAPES", "BLOOD"),
        Q("WHAT BURNS A VAMPIRE?", "SUNLIGHT", "MOONLIGHT", "RAINBOWS"),
        Q("WHERE DO VAMPIRES SLEEP?", "COFFIN", "HAMMOCK", "BATHTUB"),
        Q("A VAMPIRE CAN TURN INTO A...?", "BAT", "TOASTER", "DOLPHIN"),
        Q("WHAT HURTS A WEREWOLF?", "SILVER", "GOLD", "BACON"),
        Q("WHAT TURNS A MAN INTO A WEREWOLF?", "MOON", "PIZZA", "TAXES"),
        Q("WHAT DO MUMMIES WEAR?", "BANDAGES", "TUXEDOS", "PAJAMAS"),
        Q("WHERE DO MUMMIES COME FROM?", "EGYPT", "CANADA", "MARS"),
        Q("WHAT DOES A GHOST SAY?", "BOO", "MOO", "HELLO"),
        Q("WHAT DOES A GHOST WEAR?", "SHEET", "JEANS", "ARMOR"),
        Q("WHO LIVES IN A HAUNTED HOUSE?", "GHOST", "PLUMBER", "ASTRONAUT"),
        Q("WHAT SOUND DO ZOMBIES MAKE?", "GROAN", "QUACK", "MEOW"),
        Q("WHAT STOPS A ZOMBIE FOR GOOD?", "HEADSHOT", "TICKLE", "COMPLIMENT"),
        Q("WHAT DOES A ZOMBIE NOT HAVE?", "PULSE", "HUNGER", "TEETH"),
        Q("WHAT DOES A ZOMBIE BITE MAKE YOU?", "INFECTED", "RICH", "TALLER"),
        Q("WHAT ELSE ARE ZOMBIES CALLED?", "UNDEAD", "HIPSTERS", "ROOMMATES"),
        Q("WHAT IS A BIG GROUP OF ZOMBIES?", "HORDE", "FLOCK", "SCHOOL"),
        Q("WHAT KEEPS ZOMBIES OUT?", "BARRICADE", "DOORMAT", "PICNIC"),
        Q("CAN THE ZOMBIE HAVE YOUR BRAIN?", "NOPE", "SURE", "MAYBE"),
        Q("WHICH SEA DO ZOMBIES SWIM IN?", "DEAD", "RED", "BLACK"),
        Q("WHAT IS A SKELETON MADE OF?", "BONES", "JELLY", "CHEESE"),
        Q("WHERE DO THE DEAD REST?", "GRAVEYARD", "MALL", "BEACH"),
        Q("RIP MEANS REST IN WHAT?", "PEACE", "GRAVY", "SPACE"),
        Q("WHAT WOKE FRANKENSTEIN'S MONSTER?", "LIGHTNING", "COFFEE", "ALARM"),
        Q("WHAT DOES A WITCH RIDE?", "BROOM", "CAMEL", "SCOOTER"),
        Q("WHAT DOES A WITCH BREW POTIONS IN?", "CAULDRON", "TEAPOT", "BUCKET"),
        Q("WHAT DO PEOPLE CARVE ON HALLOWEEN?", "PUMPKINS", "TURKEYS", "BREAD"),
        Q("WHAT DOES A SCARECROW SCARE?", "CROWS", "BEARS", "TOURISTS"),
        Q("WHAT DO SPIDERS SPIN?", "WEBS", "RECORDS", "PLATES"),
        Q("A BLACK CAT MEANS BAD...?", "LUCK", "HAIR", "WIFI"),
        Q("DRACULA IS FROM WHICH COUNTRY?", "ROMANIA", "FRANCE", "BRAZIL"),
        Q("WHAT DO YOU CALL A SCARY MOVIE?", "HORROR", "COMEDY", "MUSICAL"),

        // ---- This game (they also teach the rules) ----
        Q("WHICH COLOR ZOMBIE EXPLODES?", "RED", "GREEN", "YELLOW"),
        Q("WHAT KILLS ARMORED ZOMBIES AT ONCE?", "EXPLOSION", "KINDNESS", "SNOWBALL"),
        Q("WHAT DO RED BARRELS DO WHEN SHOT?", "EXPLODE", "DANCE", "SING"),
        Q("WHAT KEY PAUSES THE GAME?", "ESCAPE", "SPACE", "DELETE"),
        Q("WHAT KEY DROPS YOUR TARGET?", "BACKSPACE", "ENTER", "TAB"),

        // ---- Riddles ----
        Q("WHAT HAS TEETH BUT CANNOT BITE?", "COMB", "SHARK", "ZOMBIE"),
        Q("WHAT HAS HANDS BUT CANNOT CLAP?", "CLOCK", "BABY", "MONKEY"),
        Q("WHAT HAS ONE EYE BUT CANNOT SEE?", "NEEDLE", "CYCLOPS", "PIRATE"),
        Q("WHAT HAS A NECK BUT NO HEAD?", "BOTTLE", "GIRAFFE", "SWAN"),
        Q("WHAT HAS KEYS BUT NO LOCKS?", "PIANO", "DOOR", "SAFE"),
        Q("WHAT RUNS BUT NEVER WALKS?", "RIVER", "TURTLE", "ZOMBIE"),
        Q("WHAT GETS WETTER AS IT DRIES?", "TOWEL", "DESERT", "RAIN"),
        Q("WHAT GOES UP BUT NEVER COMES DOWN?", "AGE", "BALLOON", "ROCKET"),
        Q("WHAT CAN YOU CATCH BUT NOT THROW?", "COLD", "BALL", "FISH"),
        Q("WHAT DO COWS DRINK?", "WATER", "MILK", "JUICE"),

        // ---- Quick maths (the answer is a number written as a word) ----
        Q("WHAT IS 2 + 2?", "FOUR", "THREE", "SIX"),
        Q("WHAT IS 3 TIMES 3?", "NINE", "SIX", "EIGHT"),
        Q("WHAT IS 10 - 3?", "SEVEN", "EIGHT", "THIRTEEN"),
        Q("WHAT IS HALF OF 12?", "SIX", "FOUR", "TWO"),
        Q("WHAT IS 5 + 6?", "ELEVEN", "TWELVE", "NINE"),
        Q("WHAT IS 7 + 8?", "FIFTEEN", "SIXTEEN", "THIRTEEN"),
        Q("WHAT IS 4 TIMES 5?", "TWENTY", "NINE", "FIFTEEN"),
        Q("WHAT IS 100 DIVIDED BY 10?", "TEN", "ONE", "HUNDRED"),
        Q("HOW MANY LEGS DO 2 ZOMBIES HAVE?", "FOUR", "TWO", "EIGHT"),
        Q("HOW MANY LEGS DOES A SPIDER HAVE?", "EIGHT", "SIX", "TEN"),
        Q("HOW MANY SIDES DOES A TRIANGLE HAVE?", "THREE", "FOUR", "SIX"),
        Q("HOW MANY DAYS ARE IN A WEEK?", "SEVEN", "FIVE", "TEN"),
        Q("HOW MANY MONTHS ARE IN A YEAR?", "TWELVE", "ELEVEN", "NINE"),
        Q("HOW MANY MINUTES ARE IN AN HOUR?", "SIXTY", "FIFTY", "HUNDRED"),

        // ---- Spelling (only one of the three is spelled right) ----
        Q("WHICH IS SPELLED RIGHT?", "ZOMBIE", "SKELITON", "MUMMEY"),
        Q("WHICH IS SPELLED CORRECTLY?", "GHOST", "VAMPIER", "WAREWOLF"),
        Q("WHICH IS SPELLED RIGHT?", "SKELETON", "CEMETARY", "PUMKIN"),
        Q("WHICH IS SPELLED CORRECTLY?", "EXPLOSION", "GRENAID", "BARRELL"),
        Q("WHICH IS SPELLED RIGHT?", "NECESSARY", "SEPERATE", "DEFINATELY"),
        Q("WHICH IS SPELLED CORRECTLY?", "RHYTHM", "BELEIVE", "WIERD"),
        Q("WHICH IS SPELLED RIGHT?", "CALENDAR", "TOMMOROW", "ACCOMODATE"),
        Q("WHICH IS SPELLED CORRECTLY?", "BEGINNING", "ARGUEMENT", "RECIEVE"),

        // ---- Opposites and odd one out ----
        Q("WHAT IS THE OPPOSITE OF ALIVE?", "DEAD", "AWAKE", "HAPPY"),
        Q("WHAT IS THE OPPOSITE OF HOT?", "COLD", "WARM", "SPICY"),
        Q("WHAT IS THE OPPOSITE OF FAST?", "SLOW", "QUICK", "LOUD"),
        Q("WHAT IS THE OPPOSITE OF DAY?", "NIGHT", "MORNING", "SUNNY"),
        Q("WHAT IS THE OPPOSITE OF FRIEND?", "ENEMY", "BUDDY", "PAL"),
        Q("WHICH ONE IS NOT A MONSTER?", "TOASTER", "GOBLIN", "OGRE"),
        Q("WHICH ONE IS NOT A FRUIT?", "CARROT", "APPLE", "BANANA"),
        Q("WHICH ONE IS NOT A PLANET?", "SUN", "MARS", "VENUS"),
        Q("WHICH ONE IS NOT AN ANIMAL?", "PENCIL", "LLAMA", "OTTER"),
        Q("WHICH ONE IS NOT A COLOR?", "TUESDAY", "PURPLE", "ORANGE"),
        Q("WHICH ONE IS NOT A DAY OF THE WEEK?", "JUNE", "MONDAY", "FRIDAY"),
        Q("WHICH BIRD CANNOT FLY?", "PENGUIN", "EAGLE", "ROBIN"),

        // ---- Easy general knowledge ----
        Q("WHICH IS THE RED PLANET?", "MARS", "VENUS", "JUPITER"),
        Q("WHAT IS THE BIGGEST PLANET?", "JUPITER", "SATURN", "EARTH"),
        Q("WHERE DOES THE SUN RISE?", "EAST", "WEST", "NORTH"),
        Q("WHAT IS FROZEN WATER CALLED?", "ICE", "STEAM", "GLASS"),
        Q("WHAT IS H2O?", "WATER", "SALT", "GOLD"),
        Q("WHAT GAS DO WE NEED TO BREATHE?", "OXYGEN", "HELIUM", "NEON"),
        Q("WHAT IS THE LARGEST OCEAN?", "PACIFIC", "ATLANTIC", "INDIAN"),
        Q("WHAT IS THE CAPITAL OF FRANCE?", "PARIS", "LONDON", "ROME"),
        Q("WHAT IS THE CAPITAL OF JAPAN?", "TOKYO", "KYOTO", "OSAKA"),
        Q("WHAT DO BEES MAKE?", "HONEY", "MILK", "SILK"),
        Q("WHAT IS THE FASTEST LAND ANIMAL?", "CHEETAH", "HORSE", "SLOTH"),
        Q("WHICH ANIMAL HAS A TRUNK?", "ELEPHANT", "GIRAFFE", "RHINO"),
        Q("WHICH OF THESE IS A MAMMAL?", "WHALE", "SHARK", "TROUT"),
        Q("WHAT IS A BABY CAT CALLED?", "KITTEN", "PUPPY", "CALF"),
        Q("WHAT DOES A CATERPILLAR BECOME?", "BUTTERFLY", "FROG", "SNAKE"),
        Q("WHERE DO MOST PENGUINS LIVE?", "ANTARCTICA", "SAHARA", "HAWAII"),
        Q("WHAT DOES A RED LIGHT MEAN?", "STOP", "DANCE", "FASTER"),
        Q("HOW MANY PLAYERS IN A CHESS GAME?", "TWO", "FOUR", "SIX"),
        Q("HOW MANY DWARFS DID SNOW WHITE MEET?", "SEVEN", "THREE", "NINE"),
    };

    // Index (in Questions) of the question asked last time, so the same
    // question is never asked twice in a row. -1 = none asked yet.
    private static int lastPickedIndex = -1;

    // The index of the question Pick returned last (for analytics).
    public static int LastPickedIndex
    {
        get { return lastPickedIndex; }
    }

    // Returns a random question (never the same as last time) with its answers
    // in a random order. Prefers questions whose answers' first letters are not
    // in usedFirstLetters (UPPERCASE first letters of words already on screen).
    public static QuizQuestion Pick(List<char> usedFirstLetters)
    {
        // 1. The questions we would like to ask: not the one asked last time,
        //    and none of their answers starts with a letter already on screen
        //    (otherwise the first key could point at two different words).
        List<int> candidates = new List<int>();
        for (int i = 0; i < Questions.Length; i++)
        {
            if (i != lastPickedIndex && !UsesAnyLetter(Questions[i], usedFirstLetters))
            {
                candidates.Add(i);
            }
        }

        // 2. No question qualifies (a lot of letters are taken): any question
        //    will do, as long as it is not the one asked last time.
        if (candidates.Count == 0)
        {
            for (int i = 0; i < Questions.Length; i++)
            {
                if (i != lastPickedIndex)
                {
                    candidates.Add(i);
                }
            }
        }

        // 3. Only possible if the list holds a single question: repeat it.
        if (candidates.Count == 0)
        {
            candidates.Add(0);
        }

        int index = candidates[Random.Range(0, candidates.Count)];
        lastPickedIndex = index;
        return ShuffledCopy(Questions[index]);
    }

    // Builds one question: the first answer is the right one.
    // Used only to write the Questions list above.
    private static QuizQuestion Q(string question, string rightAnswer, string wrongAnswer1, string wrongAnswer2)
    {
        return new QuizQuestion(question, new string[] { rightAnswer, wrongAnswer1, wrongAnswer2 }, 0);
    }

    // True if one of the question's answers starts with a letter in usedLetters.
    private static bool UsesAnyLetter(QuizQuestion question, List<char> usedLetters)
    {
        if (usedLetters == null)
        {
            return false;
        }

        foreach (string answer in question.Answers)
        {
            foreach (char used in usedLetters)
            {
                if (char.ToUpperInvariant(used) == answer[0])
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Returns a NEW question with the same text and the answers in a random
    // order. (The questions in the list are never changed, so the right answer
    // there stays first.)
    private static QuizQuestion ShuffledCopy(QuizQuestion original)
    {
        string[] answers = new string[original.Answers.Length];
        for (int i = 0; i < answers.Length; i++)
        {
            answers[i] = original.Answers[i];
        }

        // Fisher-Yates shuffle: walk from the last slot to the second one and
        // swap each slot with a random slot at or before it. Every order is
        // equally likely. (Random.Range(0, i + 1) gives 0..i: the max is excluded.)
        for (int i = answers.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string temp = answers[i];
            answers[i] = answers[j];
            answers[j] = temp;
        }

        // Find where the right answer ended up.
        string rightAnswer = original.CorrectAnswer;
        int correctIndex = 0;
        for (int i = 0; i < answers.Length; i++)
        {
            if (answers[i] == rightAnswer)
            {
                correctIndex = i;
            }
        }

        return new QuizQuestion(original.Question, answers, correctIndex);
    }

    // ---- Checking the list (helps when you add questions) ----

    // Unity calls this once when Play starts (that is what the attribute
    // does). It prints a red error for every question that breaks a rule from
    // the top of this file, so a typo is found at once and not at the boss.
    // Prints nothing when every question is fine.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CheckQuestions()
    {
        for (int i = 0; i < Questions.Length; i++)
        {
            string problem = FindProblem(Questions[i]);
            if (problem != null)
            {
                Debug.LogError("QuizBank: question " + (i + 1) + " (\"" + Questions[i].Question + "\") " + problem + ". Fix it in QuizBank.cs.");
            }
        }
    }

    // Returns null if the question follows every rule, or else a short
    // description of the first broken rule.
    private static string FindProblem(QuizQuestion question)
    {
        string text = question.Question;
        if (string.IsNullOrEmpty(text) || text[text.Length - 1] != '?')
        {
            return "does not end with a question mark";
        }
        if (text.Length > MaxQuestionLength)
        {
            return "is longer than " + MaxQuestionLength + " characters";
        }
        foreach (char c in text)
        {
            if (c >= 'a' && c <= 'z')
            {
                return "is not all UPPERCASE";
            }
            if (c < ' ' || c > '~' || c == '<' || c == '>')
            {
                return "has a character the font cannot show, or a < or >";
            }
        }

        foreach (string answer in question.Answers)
        {
            if (answer == null || answer.Length < MinAnswerLength || answer.Length > MaxAnswerLength)
            {
                return "has an answer that is not " + MinAnswerLength + " to " + MaxAnswerLength + " letters long";
            }
            foreach (char c in answer)
            {
                if (c < 'A' || c > 'Z')
                {
                    return "has an answer (" + answer + ") that is not only UPPERCASE letters A-Z";
                }
            }
        }

        // The 3 answers must start with 3 different letters.
        string[] answers = question.Answers;
        for (int a = 0; a < answers.Length; a++)
        {
            for (int b = a + 1; b < answers.Length; b++)
            {
                if (answers[a][0] == answers[b][0])
                {
                    return "has two answers starting with " + answers[a][0];
                }
            }
        }
        return null;
    }
}
