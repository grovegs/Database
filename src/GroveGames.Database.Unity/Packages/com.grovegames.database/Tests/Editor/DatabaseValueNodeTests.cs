using System.Collections.Generic;
using GroveGames.Serialization;
using NUnit.Framework;

namespace GroveGames.Database.Unity.Editor.Tests
{
    public sealed class DatabaseValueNodeTests
    {
        [Test]
        public void Children_ObjectWithFields_ListsEachField()
        {
            var node = CreateRoot(new TestRecord());

            var children = node.Children;

            Assert.That(children.Count, Is.EqualTo(7));
            Assert.That(children[0].Label, Is.EqualTo("Id"));
            Assert.That(children[0].Kind, Is.EqualTo(DatabaseValueKind.Text));
            Assert.That(children[0].Path, Is.EqualTo("record/Id"));
        }

        [Test]
        public void Children_IgnoredAndComputedMembers_AreSkipped()
        {
            var node = CreateRoot(new TestRecord());

            var labels = new List<string>();

            foreach (var child in node.Children)
            {
                labels.Add(child.Label);
            }

            Assert.That(labels, Has.No.Member("Hidden"));
            Assert.That(labels, Has.No.Member("Doubled"));
            Assert.That(labels, Has.Member("Title"));
        }

        [Test]
        public void TrySetText_IntegerField_SetsFieldAndReportsRoot()
        {
            var record = new TestRecord();
            object? changed = null;
            var node = DatabaseValueNode.CreateRoot("record", "record", record, value => changed = value);

            var isSet = node.Children[0].TrySetText("42");

            Assert.That(isSet, Is.True);
            Assert.That(record.Id, Is.EqualTo(42));
            Assert.That(changed, Is.SameAs(record));
        }

        [Test]
        public void TrySetText_InvalidInteger_ReturnsFalse()
        {
            var record = new TestRecord { Id = 3 };
            var node = CreateRoot(record);

            var isSet = node.Children[0].TrySetText("abc");

            Assert.That(isSet, Is.False);
            Assert.That(record.Id, Is.EqualTo(3));
        }

        [Test]
        public void TrySet_ToggleField_SetsField()
        {
            var record = new TestRecord();
            var node = CreateRoot(record);
            var enabled = node.Children[1];

            var isSet = enabled.TrySet(true);

            Assert.That(enabled.Kind, Is.EqualTo(DatabaseValueKind.Toggle));
            Assert.That(isSet, Is.True);
            Assert.That(record.Enabled, Is.True);
        }

        [Test]
        public void TrySetText_StructMember_WritesBackToParent()
        {
            var record = new TestRecord();
            var node = CreateRoot(record);
            var position = node.Children[2];

            position.Children[0].TrySetText("1.5");

            Assert.That(position.Kind, Is.EqualTo(DatabaseValueKind.Object));
            Assert.That(record.Position.X, Is.EqualTo(1.5f));
        }

        [Test]
        public void TrySetText_ListItem_ReplacesItem()
        {
            var record = new TestRecord();
            record.Scores.Add(1);
            var node = CreateRoot(record);
            var scores = node.Children[3];

            scores.Children[0].TrySetText("9");

            Assert.That(scores.Kind, Is.EqualTo(DatabaseValueKind.List));
            Assert.That(record.Scores[0], Is.EqualTo(9));
        }

        [Test]
        public void TrySetText_NullString_SetsText()
        {
            var record = new TestRecord();
            var node = CreateRoot(record);
            var name = node.Children[4];

            name.TrySetText("Ant");

            Assert.That(name.Kind, Is.EqualTo(DatabaseValueKind.Text));
            Assert.That(record.Name, Is.EqualTo("Ant"));
        }

        [Test]
        public void TrySetText_EmptyNullable_SetsNull()
        {
            var record = new TestRecord { Limit = 5 };
            var node = CreateRoot(record);

            var isSet = node.Children[5].TrySetText(string.Empty);

            Assert.That(isSet, Is.True);
            Assert.That(record.Limit, Is.Null);
        }

        [Test]
        public void TrySetText_ReadOnlyProperty_ReturnsFalse()
        {
            var record = new TestRecord();
            var node = CreateRoot(record);
            var title = node.Children[6];

            var isSet = title.TrySetText("Other");

            Assert.That(title.IsEditable, Is.False);
            Assert.That(isSet, Is.False);
            Assert.That(record.Title, Is.EqualTo("Record"));
        }

        [Test]
        public void TryParse_Float_UsesInvariantCulture()
        {
            var isParsed = DatabaseValueNode.TryParse("2.25", typeof(float), out var value);

            Assert.That(isParsed, Is.True);
            Assert.That(value, Is.EqualTo(2.25f));
        }

        [Test]
        public void TryParse_EnumName_IgnoresCase()
        {
            var isParsed = DatabaseValueNode.TryParse("second", typeof(TestMode), out var value);

            Assert.That(isParsed, Is.True);
            Assert.That(value, Is.EqualTo(TestMode.Second));
        }

        [Test]
        public void TrySetText_ParsableStruct_ShowsAndParsesText()
        {
            var record = new TestAmountRecord { Amount = new TestAmount(14, "a") };
            var node = CreateRoot(record);
            var amount = node.Children[0];

            var isSet = amount.TrySetText("3b");

            Assert.That(amount.Kind, Is.EqualTo(DatabaseValueKind.Text));
            Assert.That(isSet, Is.True);
            Assert.That(record.Amount.ToString(), Is.EqualTo("3b"));
        }

        [Test]
        public void Text_ParsableStruct_UsesToString()
        {
            var node = CreateRoot(new TestAmountRecord { Amount = new TestAmount(14, "a") });

            var text = node.Children[0].Text;

            Assert.That(text, Is.EqualTo("14a"));
        }

        [Test]
        public void TrySetText_InvalidParsableText_ReturnsFalse()
        {
            var record = new TestAmountRecord { Amount = new TestAmount(14, "a") };
            var node = CreateRoot(record);

            var isSet = node.Children[0].TrySetText("abc");

            Assert.That(isSet, Is.False);
            Assert.That(record.Amount.ToString(), Is.EqualTo("14a"));
        }

        private static DatabaseValueNode CreateRoot(object value)
        {
            return DatabaseValueNode.CreateRoot("record", "record", value, _ => { });
        }

        private enum TestMode
        {
            First,
            Second
        }

        private struct TestPosition
        {
            public float X;
            public float Y;
        }

        private readonly struct TestAmount
        {
            private readonly int _value;
            private readonly string _suffix;

            public TestAmount(int value, string suffix)
            {
                _value = value;
                _suffix = suffix;
            }

            public int Value => _value;
            public string Suffix => _suffix;

            public static bool TryParse(string text, out TestAmount amount)
            {
                var digits = 0;

                while (digits < text.Length && char.IsDigit(text[digits]))
                {
                    digits++;
                }

                if (digits == 0 || !int.TryParse(text.Substring(0, digits), out var value))
                {
                    amount = default;
                    return false;
                }

                amount = new TestAmount(value, text.Substring(digits));
                return true;
            }

            public override string ToString()
            {
                return Value + Suffix;
            }
        }

        private sealed class TestAmountRecord
        {
            public TestAmount Amount;
        }

        private sealed class TestRecord
        {
            private readonly string _title;

            public int Id;
            public bool Enabled;
            public TestPosition Position;
            public List<int> Scores = new();
            public string? Name;
            public int? Limit;

            [Ignore]
            public int Hidden;

            public string Title => _title;
            public int Doubled => Id * 2;

            public TestRecord()
            {
                _title = "Record";
            }
        }
    }
}
