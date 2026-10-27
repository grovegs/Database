#if GROVEGAMES_DEVELOPERTOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GroveGames.DeveloperTools.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GroveGames.Database.Unity.DeveloperTools
{
    public sealed class DatabasePage : IDeveloperToolsPage
    {
        public const int PageOrder = 200;

        private const string StoreExtension = ".db";
        private const string StorePattern = "*.db";
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const float ChevronSize = 32f;
        private const float BytesPerKilobyte = 1024f;
        private const string NotInspectableText = "This database does not support inspection.";
        private const string EmptyText = "No stores yet.";
        private const string NotLoadedText = "Not loaded in this session.";
        private const string EmptyCollectionText = "No items.";

        private readonly IDatabaseInspector? _inspector;
        private readonly Font? _boldFont;
        private readonly List<string> _names;
        private readonly List<string> _scheduled;
        private readonly HashSet<string> _expanded;
        private readonly DatabaseValueView _valueView;
        private readonly Label _path;
        private readonly Button _resetAll;
        private readonly Label _status;
        private readonly Label _empty;
        private readonly ScrollView _list;

        public string Title => "Database";
        public VisualElement Root { get; }

        public DatabasePage(IDatabase database, DeveloperToolsSettings settings)
        {
            _inspector = database as IDatabaseInspector;
            _boldFont = settings.BoldFont;
            _names = new List<string>();
            _scheduled = new List<string>();
            _expanded = new HashSet<string>(StringComparer.Ordinal);
            _valueView = new DatabaseValueView(_expanded, _boldFont);

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.flexShrink = 1f;
            Root.style.minHeight = 0f;

            _path = CreateMutedLabel(string.Empty);
            _path.style.marginBottom = DeveloperToolsStyle.Spacing;
            Root.Add(_path);

            var toolbar = DeveloperToolsStyle.CreateRow();
            toolbar.style.flexWrap = Wrap.Wrap;
            var refresh = DeveloperToolsStyle.CreateButton("Refresh", Rebuild);
            refresh.style.marginBottom = DeveloperToolsStyle.Spacing;
            toolbar.Add(refresh);
            _resetAll = DeveloperToolsStyle.CreateButton(string.Empty, HandleResetAllClicked);
            _resetAll.style.marginBottom = DeveloperToolsStyle.Spacing;
            toolbar.Add(_resetAll);
            Root.Add(toolbar);

            _status = new Label();
            _status.style.fontSize = DeveloperToolsStyle.FontSize;
            _status.style.color = DeveloperToolsStyle.AccentColor;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.marginBottom = DeveloperToolsStyle.Spacing;
            DeveloperToolsStyle.ApplyBold(_status, _boldFont);
            Root.Add(_status);

            _empty = CreateMutedLabel(string.Empty);
            _empty.style.marginTop = DeveloperToolsStyle.LargeSpacing;
            Root.Add(_empty);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            _list.style.flexShrink = 1f;
            _list.style.minHeight = 0f;
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Root.Add(_list);
        }

        public void Refresh(bool force)
        {
            if (force)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            _list.Clear();

            if (_inspector == null)
            {
                _path.style.display = DisplayStyle.None;
                _resetAll.style.display = DisplayStyle.None;
                _status.style.display = DisplayStyle.None;
                ShowEmpty(NotInspectableText);
                return;
            }

            var directory = _inspector.Directory;
            _path.text = directory;
            DatabaseReset.GetScheduled(directory, _scheduled);
            CollectNames(directory);

            var isResettingAll = _scheduled.Contains(DatabaseReset.AllStores);
            _resetAll.text = isResettingAll ? "Keep all data" : "Delete all on next launch";
            _resetAll.style.backgroundColor = isResettingAll ? DeveloperToolsStyle.AccentColor : DeveloperToolsStyle.ControlColor;
            _status.text = isResettingAll
                ? "All data will be deleted on next launch."
                : $"{_scheduled.Count} {(_scheduled.Count == 1 ? "store" : "stores")} will be deleted on next launch.";
            _status.style.display = _scheduled.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (_names.Count == 0)
            {
                ShowEmpty(EmptyText);
                return;
            }

            _empty.style.display = DisplayStyle.None;

            for (var i = 0; i < _names.Count; i++)
            {
                _list.Add(CreateStore(directory, _names[i], isResettingAll));
            }
        }

        private void CollectNames(string directory)
        {
            _names.Clear();

            try
            {
                if (Directory.Exists(directory))
                {
                    foreach (var path in Directory.GetFiles(directory, StorePattern))
                    {
                        _names.Add(Path.GetFileNameWithoutExtension(path));
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to list the database stores: {exception.Message}");
            }

            foreach (var name in _inspector!.OpenStoreNames)
            {
                if (!_names.Contains(name))
                {
                    _names.Add(name);
                }
            }

            _names.Sort(StringComparer.Ordinal);
        }

        private VisualElement CreateStore(string directory, string name, bool isResettingAll)
        {
            var isExpanded = _expanded.Contains(name);
            var isOpen = _inspector!.TryInspect(name, out var value);
            var isScheduled = isResettingAll || _scheduled.Contains(name);

            var card = new VisualElement();
            card.style.backgroundColor = DeveloperToolsStyle.SurfaceColor;
            card.style.marginTop = DeveloperToolsStyle.Spacing;
            card.style.paddingLeft = DeveloperToolsStyle.LargeSpacing;
            card.style.paddingRight = DeveloperToolsStyle.LargeSpacing;
            card.style.paddingBottom = DeveloperToolsStyle.Spacing;
            DeveloperToolsStyle.SetBorderRadius(card.style, DeveloperToolsStyle.CornerRadius);

            var header = new Button(() => ToggleExpanded(name));
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = DeveloperToolsStyle.ControlHeight;
            header.style.color = DeveloperToolsStyle.TextColor;
            header.style.backgroundColor = Color.clear;
            header.style.marginLeft = -DeveloperToolsStyle.Spacing;
            header.style.marginRight = -DeveloperToolsStyle.Spacing;
            header.style.paddingLeft = DeveloperToolsStyle.Spacing;
            DeveloperToolsStyle.SetBorderWidth(header.style, 0f);
            DeveloperToolsStyle.SetBorderRadius(header.style, DeveloperToolsStyle.CornerRadius);
            DeveloperToolsStyle.AddHighlightFeedback(header);
            var chevron = new ChevronIcon(ChevronSize);
            chevron.style.marginRight = DeveloperToolsStyle.Spacing;
            chevron.SetExpanded(isExpanded);
            header.Add(chevron);
            var title = new Label(name) { pickingMode = PickingMode.Ignore };
            title.style.fontSize = DeveloperToolsStyle.TitleFontSize;
            DeveloperToolsStyle.ApplyBold(title, _boldFont);
            header.Add(title);
            card.Add(header);

            var details = CreateMutedLabel(string.Empty);
            details.text = DescribeFile(Path.Combine(directory, name + StoreExtension), isOpen);
            details.style.marginBottom = DeveloperToolsStyle.Spacing;
            card.Add(details);

            var actions = DeveloperToolsStyle.CreateRow();
            actions.style.flexWrap = Wrap.Wrap;
            var delete = DeveloperToolsStyle.CreateButton(isScheduled ? "Keep" : "Delete on next launch", () => ToggleScheduled(directory, name));
            delete.style.backgroundColor = isScheduled ? DeveloperToolsStyle.AccentColor : DeveloperToolsStyle.ControlColor;
            delete.style.marginBottom = DeveloperToolsStyle.Spacing;
            delete.SetEnabled(!isResettingAll);
            actions.Add(delete);
            card.Add(actions);

            if (!isExpanded)
            {
                return card;
            }

            if (!isOpen)
            {
                card.Add(CreateMutedLabel(NotLoadedText));
                return card;
            }

            if (_inspector!.IsCollection(name) && value is IEnumerable items)
            {
                var index = 0;

                foreach (var item in items)
                {
                    var label = "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    var node = DatabaseValueNode.CreateRoot(label, name + "/" + label, item, changed => Write(name, changed));
                    card.Add(_valueView.Create(node));
                    index++;
                }

                if (index == 0)
                {
                    card.Add(CreateMutedLabel(EmptyCollectionText));
                }
            }
            else
            {
                var node = DatabaseValueNode.CreateRoot(name, name, value, changed => Write(name, changed));
                card.Add(_valueView.CreateContents(node));
            }

            var copy = DeveloperToolsStyle.CreateButton("Copy", () => Copy(name));
            copy.style.marginTop = DeveloperToolsStyle.Spacing;
            copy.style.alignSelf = Align.FlexStart;
            card.Add(copy);
            return card;
        }

        private void Write(string name, object? value)
        {
            if (value == null)
            {
                return;
            }

            try
            {
                _inspector!.TryWrite(name, value);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to write the database store '{name}': {exception.Message}");
            }
        }

        private void Copy(string name)
        {
            if (_inspector!.TryInspect(name, out var value))
            {
                GUIUtility.systemCopyBuffer = DatabaseValueFormatter.Format(value);
            }
        }

        private void ToggleExpanded(string name)
        {
            if (!_expanded.Remove(name))
            {
                _expanded.Add(name);
            }

            Rebuild();
        }

        private void ToggleScheduled(string directory, string name)
        {
            if (_scheduled.Contains(name))
            {
                DatabaseReset.Unschedule(directory, name);
            }
            else
            {
                DatabaseReset.Schedule(directory, name);
            }

            Rebuild();
        }

        private void HandleResetAllClicked()
        {
            var directory = _inspector!.Directory;

            if (_scheduled.Contains(DatabaseReset.AllStores))
            {
                DatabaseReset.Unschedule(directory, DatabaseReset.AllStores);
            }
            else
            {
                DatabaseReset.Schedule(directory, DatabaseReset.AllStores);
            }

            Rebuild();
        }

        private void ShowEmpty(string text)
        {
            _empty.text = text;
            _empty.style.display = DisplayStyle.Flex;
        }

        private static string DescribeFile(string path, bool isOpen)
        {
            var state = isOpen ? "Loaded" : "Not loaded";

            try
            {
                var file = new FileInfo(path);

                if (!file.Exists)
                {
                    return $"{state}  |  Not saved yet";
                }

                var size = (file.Length / BytesPerKilobyte).ToString("0.0", CultureInfo.InvariantCulture);
                return $"{state}  |  {size} KB  |  {file.LastWriteTime.ToString(TimeFormat, CultureInfo.InvariantCulture)}";
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to read the database store file: {exception.Message}");
                return state;
            }
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
