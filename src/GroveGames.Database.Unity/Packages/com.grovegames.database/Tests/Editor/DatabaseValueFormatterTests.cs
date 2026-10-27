using System.Collections.Generic;
using NUnit.Framework;

namespace GroveGames.Database.Unity.Editor.Tests
{
    public sealed class DatabaseValueFormatterTests
    {
        [Test]
        public void Format_ObjectWithFields_WritesEachField()
        {
            var value = new TestRecord { Id = 3, Name = "Ant" };

            var text = DatabaseValueFormatter.Format(value);

            Assert.That(text, Is.EqualTo("{\n  Id: 3,\n  Name: \"Ant\"\n}"));
        }

        [Test]
        public void Format_List_WritesEachItem()
        {
            var value = new List<int> { 1, 2 };

            var text = DatabaseValueFormatter.Format(value);

            Assert.That(text, Is.EqualTo("[\n  1,\n  2\n]"));
        }

        [Test]
        public void Format_EmptyList_WritesEmptyBrackets()
        {
            var text = DatabaseValueFormatter.Format(new List<int>());

            Assert.That(text, Is.EqualTo("[]"));
        }

        [Test]
        public void Format_Null_WritesNull()
        {
            var text = DatabaseValueFormatter.Format(null);

            Assert.That(text, Is.EqualTo("null"));
        }

        [Test]
        public void Format_FloatValue_UsesInvariantCulture()
        {
            var text = DatabaseValueFormatter.Format(1.5f);

            Assert.That(text, Is.EqualTo("1.5"));
        }

        [Test]
        public void Format_ParsableValue_UsesToString()
        {
            var text = DatabaseValueFormatter.Format(new TestAmount());

            Assert.That(text, Is.EqualTo("14a"));
        }

        private readonly struct TestAmount
        {
            public double Mantissa => 1.4d;
            public long Exponent => 1L;

            public static bool TryParse(string text, out TestAmount amount)
            {
                amount = default;
                return text == "14a";
            }

            public override string ToString()
            {
                return "14a";
            }
        }

        private sealed class TestRecord
        {
            public int Id;
            public string Name = string.Empty;
        }
    }
}
