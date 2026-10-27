#if GROVEGAMES_DEVELOPERTOOLS
using System;
using System.Collections.Generic;
using GroveGames.DeveloperTools.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GroveGames.Database.Unity.DeveloperTools
{
    public sealed class DatabaseValueView
    {
        private const float ValueFieldWidth = 280f;
        private const float ToggleWidth = 200f;
        private const float ChevronSize = 32f;
        private const float IndentWidth = 4f;
        private const int MaxEnumButtons = 8;
        private const string OnText = "On";
        private const string OffText = "Off";
        private const string TruncatedText = "More items are not shown.";
        private const string EmptyText = "Empty";

        private readonly HashSet<string> _expanded;
        private readonly Font? _boldFont;

        public DatabaseValueView(HashSet<string> expanded, Font? boldFont)
        {
            _expanded = expanded;
            _boldFont = boldFont;
        }

        public VisualElement CreateContents(DatabaseValueNode node)
        {
            if (node.Kind != DatabaseValueKind.Object && node.Kind != DatabaseValueKind.List)
            {
                return Create(node);
            }

            var contents = new VisualElement();
            AddChildren(contents, node);
            return contents;
        }

        public VisualElement Create(DatabaseValueNode node)
        {
            switch (node.Kind)
            {
                case DatabaseValueKind.Object:
                case DatabaseValueKind.List:
                    return CreateGroup(node);
                case DatabaseValueKind.Toggle:
                    return CreateToggle(node);
                case DatabaseValueKind.Enum:
                    return CreateEnum(node);
                case DatabaseValueKind.Text:
                    return CreateText(node);
                default:
                    var row = CreateRow(node, out var header);
                    header.Add(CreateMutedLabel(node.Text));
                    return row;
            }
        }

        private VisualElement CreateGroup(DatabaseValueNode node)
        {
            var group = new VisualElement();
            var content = new VisualElement();
            content.style.marginLeft = DeveloperToolsStyle.Spacing;
            content.style.paddingLeft = DeveloperToolsStyle.Spacing;
            content.style.borderLeftWidth = IndentWidth;
            content.style.borderLeftColor = DeveloperToolsStyle.ControlColor;
            var chevron = new ChevronIcon(ChevronSize);

            var header = new Button(() => ToggleGroup(node, content, chevron));
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.minHeight = DeveloperToolsStyle.RowHeight;
            header.style.color = DeveloperToolsStyle.TextColor;
            header.style.backgroundColor = Color.clear;
            header.style.marginLeft = -DeveloperToolsStyle.Spacing;
            header.style.marginRight = 0f;
            header.style.paddingLeft = DeveloperToolsStyle.Spacing;
            DeveloperToolsStyle.SetBorderWidth(header.style, 0f);
            DeveloperToolsStyle.SetBorderRadius(header.style, DeveloperToolsStyle.CornerRadius);
            DeveloperToolsStyle.AddHighlightFeedback(header);
            chevron.style.marginRight = DeveloperToolsStyle.Spacing;
            chevron.pickingMode = PickingMode.Ignore;
            header.Add(chevron);
            var label = new Label(node.Label) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = DeveloperToolsStyle.ControlFontSize;
            label.style.marginRight = DeveloperToolsStyle.Spacing;
            DeveloperToolsStyle.ApplyBold(label, _boldFont);
            header.Add(label);
            var summary = CreateMutedLabel(Summarize(node));
            summary.pickingMode = PickingMode.Ignore;
            summary.style.flexShrink = 1f;
            header.Add(summary);

            group.Add(header);
            group.Add(content);
            ApplyExpanded(node, content, chevron);
            return group;
        }

        private void ToggleGroup(DatabaseValueNode node, VisualElement content, ChevronIcon chevron)
        {
            if (!_expanded.Remove(node.Path))
            {
                _expanded.Add(node.Path);
            }

            ApplyExpanded(node, content, chevron);
        }

        private void ApplyExpanded(DatabaseValueNode node, VisualElement content, ChevronIcon chevron)
        {
            var isExpanded = _expanded.Contains(node.Path);
            chevron.SetExpanded(isExpanded);
            content.Clear();
            content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (isExpanded)
            {
                AddChildren(content, node);
            }
        }

        private void AddChildren(VisualElement container, DatabaseValueNode node)
        {
            var children = node.Children;

            if (children.Count == 0)
            {
                container.Add(CreateMutedLabel(EmptyText));
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                container.Add(Create(children[i]));
            }

            if (node.IsTruncated)
            {
                container.Add(CreateMutedLabel(TruncatedText));
            }
        }

        private VisualElement CreateToggle(DatabaseValueNode node)
        {
            var row = CreateRow(node, out var header);
            Button? toggle = null;
            toggle = DeveloperToolsStyle.CreateButton(OffText, () =>
            {
                if (node.TrySet(node.Value is not true))
                {
                    ReadToggle(node, toggle!);
                }
            });
            toggle.style.width = ToggleWidth;
            toggle.style.marginRight = 0f;
            toggle.SetEnabled(node.IsEditable);
            ReadToggle(node, toggle);
            header.Add(toggle);
            return row;
        }

        private static void ReadToggle(DatabaseValueNode node, Button toggle)
        {
            var isOn = node.Value is true;
            toggle.text = isOn ? OnText : OffText;
            toggle.style.backgroundColor = isOn ? DeveloperToolsStyle.AccentColor : DeveloperToolsStyle.ControlColor;
        }

        private VisualElement CreateEnum(DatabaseValueNode node)
        {
            var enumType = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
            var values = Enum.GetValues(enumType);

            if (values.Length > MaxEnumButtons || enumType.IsDefined(typeof(FlagsAttribute), false) || !node.IsEditable)
            {
                return CreateText(node);
            }

            var row = CreateRow(node, out _);
            var names = Enum.GetNames(enumType);
            var buttons = DeveloperToolsStyle.CreateRow();
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginBottom = 0f;
            var created = new Button[values.Length];

            for (var i = 0; i < values.Length; i++)
            {
                var value = values.GetValue(i);
                var button = DeveloperToolsStyle.CreateButton(DeveloperOptionsReader.Nicify(names[i]), () =>
                {
                    if (node.TrySet(value))
                    {
                        ReadEnum(node, values, created);
                    }
                });
                button.style.marginTop = DeveloperToolsStyle.Spacing;
                created[i] = button;
                buttons.Add(button);
            }

            ReadEnum(node, values, created);
            row.Add(buttons);
            return row;
        }

        private static void ReadEnum(DatabaseValueNode node, Array values, Button[] buttons)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                var isSelected = Equals(values.GetValue(i), node.Value);
                buttons[i].style.backgroundColor = isSelected ? DeveloperToolsStyle.AccentColor : DeveloperToolsStyle.ControlColor;
            }
        }

        private VisualElement CreateText(DatabaseValueNode node)
        {
            var row = CreateRow(node, out var header);
            var field = new TextField { isDelayed = true, isReadOnly = !node.IsEditable };
            field.SetValueWithoutNotify(node.Text);
            field.RegisterValueChangedCallback(changeEvent =>
            {
                if (!node.TrySetText(changeEvent.newValue ?? string.Empty))
                {
                    Debug.LogWarning($"'{changeEvent.newValue}' is not a valid {node.ValueType.Name} for {node.Label}.");
                }

                field.SetValueWithoutNotify(node.Text);
            });
            DeveloperToolsStyle.StyleInput(field);

            if (node.ValueType == typeof(string))
            {
                row.Add(field);
            }
            else
            {
                field.style.width = ValueFieldWidth;
                header.Add(field);
            }

            return row;
        }

        private static VisualElement CreateRow(DatabaseValueNode node, out VisualElement header)
        {
            var row = new VisualElement();
            row.style.paddingTop = DeveloperToolsStyle.Spacing;
            row.style.paddingBottom = DeveloperToolsStyle.Spacing;

            header = DeveloperToolsStyle.CreateRow();
            header.style.marginBottom = 0f;
            header.style.minHeight = DeveloperToolsStyle.ControlHeight;
            var label = new Label(node.Label);
            label.style.fontSize = DeveloperToolsStyle.ControlFontSize;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexGrow = 1f;
            label.style.flexShrink = 1f;
            label.style.marginRight = DeveloperToolsStyle.Spacing;
            header.Add(label);
            row.Add(header);
            return row;
        }

        private static string Summarize(DatabaseValueNode node)
        {
            var children = node.Children;

            if (node.Kind == DatabaseValueKind.List)
            {
                var count = node.IsTruncated ? children.Count + "+" : children.Count.ToString();
                return children.Count == 1 ? "1 item" : count + " items";
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];

                if (child.Kind != DatabaseValueKind.Object && child.Kind != DatabaseValueKind.List)
                {
                    return child.Label + ": " + child.Text;
                }
            }

            return node.ValueType.Name;
        }

        private static Label CreateMutedLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = DeveloperToolsStyle.FontSize;
            label.style.color = DeveloperToolsStyle.MutedTextColor;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
#endif
