using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>The main menu, the film behind it and the narration (section 7), in Assets/Config/UI/PresentationConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Presentation", fileName = "PresentationConfig")]
    public sealed class PresentationConfig : GameConfig
    {
        [Header("Main menu")]
        [Tooltip("The title on the main menu. Empty: the project's product name.")]
        public string title = "";
        [Tooltip("Show the main menu when the game starts (off: straight into the forest, as before).")]
        public bool showMainMenu = true;
        [Range(2f, 30f), Tooltip("Seconds per shot of the film behind the menu.")]
        public float shotTime = 9f;

        [Header("Intro (after START)")]
        [Tooltip("Shown one at a time (the first over black, the rest over the forest once you wake). *word* is red; **word** is red and lingers; ***word*** lingers long.")]
        [TextArea(1, 2)]
        public string[] intro =
        {
            "*WAKE UP.* THERE IS LITTLE TIME.",
            "YOU *MUST* MOVE.",
            "**HE** IS AWARE OF YOUR INTRUSION.",
            "FIND THE *DOOR.*",
            "KEEP THE FOREST *BRIGHT.*",
            "THEY DON'T LIKE BEING *SEEN.*",
            "BUT BE CAREFUL...",
            "...THEY LIKE **YOU** EVEN LESS.",
        };
        [Range(0.5f, 10f), Tooltip("Seconds each line holds (plus its fades).")]
        public float lineTime = 2.2f;
        [Range(0, 20), Tooltip("Lines shown over black before you wake in the forest; you can move during the rest.")]
        public int linesBeforeWaking = 1;
        [Tooltip("The intro's lines two at a time in the middle of a dark screen (the second fading in under the first), and between each pair the screen glitches out to your own eyes in the forest for a moment (you can move, blurred and swaying), then glitches back for the next two; you wake after the last. Off: the old intro (the first line over black, the rest along the bottom as you wake).")]
        public bool glitchIntro = true;
        [Range(0.3f, 8f), Tooltip("The glitching intro: seconds of seeing through your eyes (able to move, blurred and swaying) between each pair of lines.")]
        public float glitchPeek = 2.5f;
        [Range(0f, 1f), Tooltip("How blurred and wobbly you are in those glimpses (0 clear, 1 as on waking).")]
        public float glitchPeekGroggy = 0.6f;
        [Range(0f, 15f), Tooltip("Seconds you wake groggy for: slow, swaying, blurred vision clearing.")]
        public float wakeGroggyTime = 9f;
        [Range(0.2f, 6f), Tooltip("Seconds for the forest to fade in when you wake.")]
        public float wakeFadeTime = 4f;

        [Header("Lamp narration")]
        [Tooltip("Lines shown when that many lamps are lit. \"-word\" is red. Frantic: red, bold and shaking.")]
        public LampLines[] lampLines =
        {
            new LampLines { lamp = 2, lines = new[] { "Have you seen the \"-Scours?\"", "They \"Really\" don't like light.", "\"Use it.\"" } },
            new LampLines { lamp = 3, lines = new[] { "This is \"-The Auscult.\"", "\"This\" one... is \"-Blind.\"", "A rare Scour \"-mutation.\"", "Don't let him \"-hear you.\"" } },
            new LampLines { lamp = 5, frantic = true, lines = new[] { "\"NO IT IS TOO LATE!!!\"", "\"YOU MUST HURRY!!!\"", "\"FOR -HE- HAS SEEN YOU!!!\"", "\"LIGHT THE REMAINING!!!!\"", "\"FIND THE DOOR!!!\"" } },
            new LampLines { lamp = 7, lines = new[] { "\"...\"", "\"I do not know of this entity.\"", "\"I have never witnessed it.\"", "\"I must seek safety, please understand.\"", "\"Good luck.\"" } },
        };

        public string Title => string.IsNullOrWhiteSpace(title) ? Application.productName : title;
    }

    [Serializable]
    public struct LampLines
    {
        public int lamp;
        public bool frantic;
        [TextArea(1, 2)] public string[] lines;
    }
}
