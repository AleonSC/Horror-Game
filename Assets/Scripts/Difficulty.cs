using UnityEngine;

/// <summary>
/// The difficulty picked on the main menu (remembered between sessions): Easy, Normal or Hard. Nothing new
/// for any of them: each just scales things the game already has, read wherever they're used (so a change
/// on the menu takes effect at once). Normal is the game as designed.
///
///                          Easy     Normal   Hard
///   flashlight battery     1.5x     1x       0.75x
///   batteries scattered    1.4x     1x       0.7x
///   extra scours           fewer, slower to come  /  more, sooner
///   facing a scour down    quicker  -        slower
///   the blind one hears    0.7x     1x       1.3x  as far
///   and closes in          slower   -        faster
///   crawlers               2, from lamp 3  /  4, lamp 2  /  6, lamp 1
///   moths gather in        1.5x     1x       0.67x the time
///   cranking a lamp        quicker  -        slower
/// </summary>
public static class Difficulty
{
    public enum Level { Easy, Normal, Hard }

    const string Key = "Difficulty";
    static bool loaded;
    static Level level = Level.Normal;

    public static Level Current
    {
        get
        {
            if (!loaded) { loaded = true; level = (Level)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Level.Normal), 0, 2); }
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

    public static string Name => Current == Level.Easy ? "EASY" : Current == Level.Hard ? "HARD" : "NORMAL";

    public static string Description =>
        Current == Level.Easy ? "More light, more batteries, fewer things in the dark."
        : Current == Level.Hard ? "Less light, fewer batteries, and they hear more."
        : "The forest as it's meant to be.";

    static float Pick(float easy, float normal, float hard) => Current == Level.Easy ? easy : Current == Level.Hard ? hard : normal;

    /// <summary>How long the flashlight's battery lasts.</summary>
    public static float BatteryLife => Pick(1.5f, 1f, 0.75f);
    /// <summary>How many batteries are scattered round the forest.</summary>
    public static float Batteries => Pick(1.4f, 1f, 0.7f);
    /// <summary>The time between the scour clock's extra scours...</summary>
    public static float ScourInterval => Pick(1.5f, 1f, 0.7f);
    /// <summary>...and how many it brings, at most.</summary>
    public static float ScourCount => Pick(0.6f, 1f, 1.4f);
    /// <summary>How long it takes to face a scour down.</summary>
    public static float DismissTime => Pick(0.75f, 1f, 1.3f);
    /// <summary>How far the blind one hears.</summary>
    public static float BlindHearing => Pick(0.7f, 1f, 1.3f);
    /// <summary>How long the blind one takes to close in round you.</summary>
    public static float BlindCloseIn => Pick(1.4f, 1f, 0.7f);
    /// <summary>How many crawlers lurk at once...</summary>
    public static float Crawlers => Pick(0.5f, 1f, 1.5f);
    /// <summary>...and how many lamps later (or earlier) they start than normal.</summary>
    public static int CrawlerLampOffset => Current == Level.Easy ? 1 : Current == Level.Hard ? -1 : 0;
    /// <summary>How long moths take to gather at the flashlight.</summary>
    public static float MothGather => Pick(1.5f, 1f, 0.67f);
    /// <summary>How much each crank charges a lamp.</summary>
    public static float LampCrank => Pick(1.35f, 1f, 0.8f);
}
