using Game.UI;
using NUnit.Framework;

namespace Game.Tests
{
    public class NarrationTests
    {
        const string Red = "<color=#D9140D>";

        [Test]
        public void Intro_StarredWordsAreRed_InCapitals()
        {
            Assert.AreEqual(Red + "WAKE UP.</color> THERE IS LITTLE TIME.", NarrationText.Intro("*wake up.* there is little time."));
            Assert.AreEqual("...THEY LIKE " + Red + "YOU</color> EVEN LESS.", NarrationText.Intro("...THEY LIKE **YOU** EVEN LESS."));
            Assert.AreEqual(Red + "OPEN</color>", NarrationText.Intro("***open"));
        }

        [Test]
        public void Lamp_DashQuotedIsRed_OtherQuotesItalic_WholeLineQuotesHidden()
        {
            Assert.AreEqual("Have you seen the " + Red + "Scours?</color>", NarrationText.Lamp("Have you seen the \"-Scours?\"", false));
            Assert.AreEqual("They <i>Really</i> don't like light.", NarrationText.Lamp("They \"Really\" don't like light.", false));
            Assert.AreEqual("Use it.", NarrationText.Lamp("\"Use it.\"", false));
        }

        [Test]
        public void Lamp_Frantic_IsAllRed_WithTheDashedWordWhite()
        {
            Assert.AreEqual(Red + "FOR <color=#FFFFFF>HE</color> HAS SEEN YOU!!!</color>", NarrationText.Lamp("\"FOR -HE- HAS SEEN YOU!!!\"", true));
        }

        [Test]
        public void Lamp_HyphenatedWord_IsLeftAlone()
        {
            Assert.AreEqual("A half-lit path.", NarrationText.Lamp("A half-lit path.", false));
        }
    }
}
