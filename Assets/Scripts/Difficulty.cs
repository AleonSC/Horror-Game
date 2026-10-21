using UnityEngine;

/// <summary>
/// The difficulty picked on the main menu (remembered between sessions): Easy, Normal, Hard, or Custom,
/// where every setting has its own slider. Nothing new for any of them: each setting scales something the
/// game already has, read wherever it's used (so a change on the menu takes effect at once). Normal is the
/// game as designed. The settings, with their Easy / Normal / Hard values, are listed in Settings below.
/// </summary>
public static class Difficulty
{
    public enum Level { Easy, Normal, Hard, Custom }

    /// <summary>One adjustable setting: its slider's range, and its value at each preset.</summary>
    public class Setting
    {
        public string label;
        public float min, max, easy, normal, hard;
        public bool whole;     // a count, not a multiplier
        public string Format(float v) => whole ? Mathf.RoundToInt(v).ToString() : $"×{v:0.00}";
    }

    public static readonly Setting[] Settings =
    {
        new Setting { label = "FLASHLIGHT BATTERY LIFE",    min = 0.25f, max = 3f,  easy = 1.5f,  normal = 1f, hard = 0.75f },
        new Setting { label = "BATTERIES IN THE FOREST",    min = 0f,    max = 3f,  easy = 1.4f,  normal = 1f, hard = 0.7f },
        new Setting { label = "TIME BETWEEN EXTRA SCOURS",  min = 0.3f,  max = 3f,  easy = 1.5f,  normal = 1f, hard = 0.7f },
        new Setting { label = "HOW MANY EXTRA SCOURS",      min = 0f,    max = 2.5f, easy = 0.6f, normal = 1f, hard = 1.4f },
        new Setting { label = "TIME TO FACE A SCOUR DOWN",  min = 0.3f,  max = 3f,  easy = 0.75f, normal = 1f, hard = 1.3f },
        new Setting { label = "HOW FAR THE BLIND ONE HEARS", min = 0.3f, max = 2.5f, easy = 0.7f, normal = 1f, hard = 1.3f },
        new Setting { label = "TIME FOR IT TO CLOSE IN",    min = 0.3f,  max = 3f,  easy = 1.4f,  normal = 1f, hard = 0.7f },
        new Setting { label = "HOW MANY CRAWLERS",          min = 0f,    max = 3f,  easy = 0.5f,  normal = 1f, hard = 1.5f },
        new Setting { label = "CRAWLERS FROM LAMP",         min = 1f,    max = 10f, easy = 3f,    normal = 2f, hard = 1f, whole = true },
        new Setting { label = "TIME FOR MOTHS TO GATHER",   min = 0.3f,  max = 4f,  easy = 1.5f,  normal = 1f, hard = 0.67f },
        new Setting { label = "LAMP CRANKING SPEED",        min = 0.3f,  max = 3f,  easy = 1.35f, normal = 1f, hard = 0.8f },
    };

    const string Key = "Difficulty";
    static bool loaded;
    static Level level = Level.Normal;
    static float[] custom;

    public static Level Current
    {
        get
        {
            if (!loaded) { loaded = true; level = (Level)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Level.Normal), 0, 3); }
            return level;
        }
        set
        {
            loaded = true;
            level = value;
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static string Name =>
        Current == Level.Easy ? "EASY" : Current == Level.Hard ? "HARD" : Current == Level.Custom ? "CUSTOM" : "NORMAL";

    public static string Description =>
        Current == Level.Easy ? "More light, more batteries, fewer things in the dark."
        : Current == Level.Hard ? "Less light, fewer batteries, and they hear more."
        : Current == Level.Custom ? "Your own settings."
        : "The forest as it's meant to be.";

    // ---------------------------------------------------------------- Values

    /// <summary>A setting's value now: its preset's, or the custom slider's.</summary>
    public static float Value(int index)
    {
        var s = Settings[index];
        switch (Current)
        {
            case Level.Easy: return s.easy;
            case Level.Hard: return s.hard;
            case Level.Custom: return CustomValue(index);
            default: return s.normal;
        }
    }

    /// <summary>A custom slider's value (Normal's until it's been moved).</summary>
    public static float CustomValue(int index)
    {
        LoadCustom();
        return custom[index];
    }

    public static void SetCustom(int index, float value)
    {
        LoadCustom();
        var s = Settings[index];
        value = Mathf.Clamp(value, s.min, s.max);
        if (s.whole) value = Mathf.Round(value);
        custom[index] = value;
        PlayerPrefs.SetFloat(Key + ".Custom." + index, value);
    }

    /// <summary>All the custom sliders back to Normal's values.</summary>
    public static void ResetCustom()
    {
        for (int i = 0; i < Settings.Length; i++) SetCustom(i, Settings[i].normal);
        PlayerPrefs.Save();
    }

    public static void SaveCustom() => PlayerPrefs.Save();

    static void LoadCustom()
    {
        if (custom != null) return;
        custom = new float[Settings.Length];
        for (int i = 0; i < Settings.Length; i++) custom[i] = PlayerPrefs.GetFloat(Key + ".Custom." + i, Settings[i].normal);
    }

    /// <summary>How long the flashlight's battery lasts.</summary>
    public static float BatteryLife => Value(0);
    /// <summary>How many batteries are scattered round the forest.</summary>
    public static float Batteries => Value(1);
    /// <summary>The time between the scour clock's extra scours...</summary>
    public static float ScourInterval => Value(2);
    /// <summary>...and how many it brings, at most.</summary>
    public static float ScourCount => Value(3);
    /// <summary>How long it takes to face a scour down.</summary>
    public static float DismissTime => Value(4);
    /// <summary>How far the blind one hears.</summary>
    public static float BlindHearing => Value(5);
    /// <summary>How long the blind one takes to close in round you.</summary>
    public static float BlindCloseIn => Value(6);
    /// <summary>How many crawlers lurk at once (times the usual number).</summary>
    public static float Crawlers => Value(7);
    /// <summary>The lamp they start coming out at.</summary>
    public static int CrawlerStartLamp => Mathf.Max(1, Mathf.RoundToInt(Value(8)));
    /// <summary>How long moths take to gather at the flashlight.</summary>
    public static float MothGather => Value(9);
    /// <summary>How much each crank charges a lamp.</summary>
    public static float LampCrank => Value(10);
}
