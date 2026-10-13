// AnalyticsEvent.cs
// ---------------------------------------------------------------------------
// One analytics event, written as a JSON object, field by field:
//
//   AnalyticsEvent e = Analytics.NewEvent("crate");   // {"event":"crate"
//   e.Add("kind", "Health");                         //  ,"kind":"Health"
//   e.Add("health_after", 100);                      //  ,"health_after":100
//   Analytics.Send(e);                               //  ...}
//
// JsonUtility cannot write a free list of fields like this, so the text is
// built by hand. Numbers are always written with a dot (1.5), whatever the
// language settings of the player's computer, and text is escaped, so the
// result is always valid JSON. Field names are lower_case_with_underscores.
// ---------------------------------------------------------------------------
using System.Globalization;
using System.Text;

public class AnalyticsEvent
{
    private readonly StringBuilder json = new StringBuilder();

    public string Name { get; private set; }

    public AnalyticsEvent(string eventName)
    {
        Name = eventName;
        json.Append('{');
        AppendKey("event");
        AppendText(json, eventName);
    }

    // Any text as a JSON string, e.g. Quote("say \"hi\"") gives "say \"hi\""
    // with the quotes escaped. Used by AnalyticsSender for the batch fields.
    public static string Quote(string text)
    {
        StringBuilder quoted = new StringBuilder();
        AppendText(quoted, text == null ? "" : text);
        return quoted.ToString();
    }

    public void Add(string key, string value)
    {
        AppendKey(key);
        if (value == null)
        {
            json.Append("null");
        }
        else
        {
            AppendText(json, value);
        }
    }

    public void Add(string key, int value)
    {
        AppendKey(key);
        json.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    public void Add(string key, float value)
    {
        AppendKey(key);
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            json.Append("null"); // not allowed in JSON
        }
        else
        {
            json.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    public void Add(string key, bool value)
    {
        AppendKey(key);
        json.Append(value ? "true" : "false");
    }

    // The finished JSON object, e.g. {"event":"crate","kind":"Health"}.
    public string ToJson()
    {
        return json.ToString() + "}";
    }

    private void AppendKey(string key)
    {
        if (json.Length > 1)
        {
            json.Append(',');
        }
        AppendText(json, key);
        json.Append(':');
    }

    // Text in double quotes. Quotes, backslashes, invisible control
    // characters and the halves of emoji (surrogates) are escaped (written as \" \\ \n \u0001 ...).
    private static void AppendText(StringBuilder to, string text)
    {
        to.Append('"');
        foreach (char character in text)
        {
            if (character == '"')
            {
                to.Append("\\\"");
            }
            else if (character == '\\')
            {
                to.Append("\\\\");
            }
            else if (character == '\n')
            {
                to.Append("\\n");
            }
            else if (character == '\r')
            {
                to.Append("\\r");
            }
            else if (character == '\t')
            {
                to.Append("\\t");
            }
            else if (character < ' ' || char.IsSurrogate(character))
            {
                to.Append("\\u");
                to.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                to.Append(character);
            }
        }
        to.Append('"');
    }
}
