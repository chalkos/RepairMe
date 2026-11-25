using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

// =====================================================================
// Shared ImGui Value-Binding Pattern (applies to ToggleItem, TextInputItem,
// SliderIntItem, DragFloatItem, DragFloat2Item):
//   1. Read value: `var v = GetValue()` — fetch current state from config.
//   2. ImGui modifies `v` via `ref v` — widget writes back edited value.
//   3. If ImGui reports change: `OnChange(v)` / `SetValue(v)` — persist new value.
//
// Works for value types (bool, int, float, Vector2) because `ref v` allows
// ImGui to modify the local copy; return value of the widget call detects changes.
// For strings, `ref` works because ImGui replaces the reference with a new string.
// =====================================================================

namespace RepairMe
{
    // =====================================================================
    // Declarative Settings Types
    // =====================================================================

    /// <summary>
    /// Top-level container. Holds tabs and config reference.
    // </summary>
    internal sealed class WindowBuilder : Window, IDisposable
    {
        private string title = "RepairMe";
        private Vector2? initialSize;
        private ImGuiCond initialSizeCondition;
        private bool disposed;

        public WindowBuilder()
            : base("RepairMe")
        {
            DisableWindowSounds = true;
        }

        public string Title
        {
            get => title;
            set
            {
                title = value;
                WindowName = value;
            }
        }

        public SettingsTab[] Tabs = null!;
        public bool UseTabBar;
        public Vector2? InitialSize
        {
            get => initialSize;
            set
            {
                initialSize = value;
                Size = value;
            }
        }

        public ImGuiCond InitialSizeCondition
        {
            get => initialSizeCondition;
            set
            {
                initialSizeCondition = value;
                SizeCondition = value;
            }
        }

        public WindowItem[] HeaderItems = Array.Empty<WindowItem>();
        public bool DrawOnce;
        public Action<WindowBuilder>? RemoveAction;

        /// <summary>
        /// Maintains the previous ref-bool call site while delegating actual drawing to WindowSystem.
        /// </summary>
        public void Draw(ref bool visible)
        {
            IsOpen = visible;
        }

        /// <summary>
        /// Draws tab content with stable hidden ID for tab bar support.
        /// </summary>
        public override void Draw()
        {
            DrawContents();

            if (DrawOnce)
                IsOpen = false;
        }

        /// <summary>
        /// Draws tab content with stable hidden ID for tab bar support.
        /// </summary>
        private void DrawContents()
        {
            foreach (var item in HeaderItems)
                item.Draw();

            var tabs = Tabs;
            if (tabs == null || tabs.Length == 0)
                return;

            if (!UseTabBar || tabs.Length == 1)
            {
                tabs[0].Draw();
                return;
            }

            // Multiple tabs with tab bar enabled: use stable hidden ID
            var stableId = Title.Replace(" ", "_");
            if (ImGui.BeginTabBar(stableId))
            {
                foreach (var tab in tabs)
                {
                    var tabLabel = tab.Name.Replace(" ", "_");
                    if (ImGui.BeginTabItem(tabLabel))
                    {
                        tab.Draw();
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }

        public override void OnSafeToRemove()
        {
            if (DrawOnce)
                RemoveAction?.Invoke(this);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            RemoveAction?.Invoke(this);
        }

        public void MarkDisposed()
        {
            disposed = true;
        }
    }

    /// <summary>
    /// A single tab within a WindowBuilder.
    // </summary>
    internal sealed class SettingsTab
    {
        public string Name = null!;
        public SectionHeader[] Sections = null!;

        public void Draw()
        {
            foreach (var section in Sections)
                section.Draw();
        }
    }

    /// <summary>
    /// A collapsible section (renders as CollapsingHeader).
    // </summary>
    internal sealed class SectionHeader
    {
        public string Title = null!;
        public ImGuiTreeNodeFlags Flags;
        public WindowItem[] Items = null!;

        public void Draw()
        {
            if (ImGui.CollapsingHeader(Title + "##" + Title.Replace(" ", "_"), Flags))
            {
                foreach (var item in Items)
                    item.Draw();
            }
        }
    }

    /// <summary>
    /// Base type for all window items.
    // </summary>
    internal abstract class WindowItem
    {
        public abstract void Draw();
    }

    /// <summary>
    /// Renders ImGui.Spacing().
    // </summary>
    internal sealed class SpacingItem : WindowItem
    {
        public override void Draw() => ImGui.Spacing();
    }

    /// <summary>
    /// Runs an arbitrary ImGui draw callback for content that has no dedicated item type.
    /// </summary>
    internal sealed class CustomItem : WindowItem
    {
        private readonly Action _draw;

        public CustomItem(Action draw) => _draw = draw;

        public override void Draw() => _draw();
    }

    /// <summary>
    /// Renders a checkbox toggle.
    // </summary>
    internal sealed class ToggleItem : WindowItem
    {
        public string Label = null!;
        public Func<bool> GetValue = null!;
        public Action<bool> OnChange = null!;

        public ToggleItem(string label, Func<bool> getValue, Action<bool> onChange)
        {
            Label = label;
            GetValue = getValue;
            OnChange = onChange;
        }

        /// <summary>
        /// Draws checkbox toggle using shared ImGui value-binding pattern (see file header).
        /// </summary>
        public override void Draw()
        {
            bool v = GetValue();
            if (ImGui.Checkbox(Label, ref v))
                OnChange(v);
        }
    }

    /// <summary>
    /// Renders an input text field.
    // </summary>
    internal sealed class TextInputItem : WindowItem
    {
        public string Label = null!;
        public string Hint = null!;
        public Func<string> GetValue = null!;
        public Action<string> SetValue = null!;
        public int MaxLength;
        public ImGuiInputTextFlags Flags;

        public TextInputItem(string label, string hint, Func<string> getValue, Action<string> setValue, int maxLength = 1000, ImGuiInputTextFlags flags = 0)
        {
            Label = label;
            Hint = hint;
            GetValue = getValue;
            SetValue = setValue;
            MaxLength = maxLength;
            Flags = flags;
        }

        /// <summary>
        /// Draws text input using shared ImGui value-binding pattern (see file header).
        /// </summary>
        public override void Draw()
        {
            var val = GetValue();
            // ESC can set val to null; user clearing text sets val to "" — allow empty, block null
            if (ImGui.InputTextWithHint(Label, Hint, ref val, MaxLength, Flags) && val != null)
                SetValue(val);
        }
    }

    /// <summary>
    /// Renders a combo box using getter/setter pattern for external config binding.
    // </summary>
    internal sealed class ComboItem : WindowItem
    {
        public string Label = null!;
        public Func<int> GetValue = null!;
        public Action<int>? SetValue;
        public Action<int>? OnChange;
        public Func<string[]> ItemsProvider = null!;

        public ComboItem(string label, Func<int> getValue, string[] items, Action<int>? setValue = null, Action<int>? onChange = null)
            : this(label, getValue, () => items, setValue, onChange)
        {
        }

        public ComboItem(string label, Func<int> getValue, Func<string[]> itemsProvider, Action<int>? setValue = null, Action<int>? onChange = null)
        {
            Label = label;
            GetValue = getValue;
            ItemsProvider = itemsProvider;
            SetValue = setValue;
            OnChange = onChange;
        }

        /// <summary>
        /// Draws combo box using shared ImGui value-binding pattern (see file header).
        /// </summary>
        public override void Draw()
        {
            var items = ItemsProvider();
            int v = GetValue();
            int itemCount = items?.Length ?? 0;
            if (itemCount > 0)
            {
                // Clamp value to valid range
                if (v < 0) v = 0;
                else if (v >= itemCount) v = itemCount - 1;
                if (ImGui.Combo(Label, ref v, items, itemCount))
                {
                    SetValue?.Invoke(v);
                    OnChange?.Invoke(v);
                }
            }
        }
    }

    /// <summary>
    /// Factory helpers for declarative window items.
    /// </summary>
    internal static class UiItem
    {
        public static SpacingItem Spacing()
            => new();

        public static CustomItem Custom(Action draw)
            => new(draw);

        public static TextItem Text(string text)
            => new(text);

        public static DynamicTextItem DynamicText(Func<string> get)
            => new(get);

        public static ToggleItem Checkbox(string label, Func<bool> get, Action<bool> set, Action? afterChange = null)
            => new(label, get, value =>
            {
                set(value);
                afterChange?.Invoke();
            });

        public static TextInputItem TextInput(string label, string hint, Func<string> get, Action<string> set,
            Action? afterChange = null, int maxLength = 1000, ImGuiInputTextFlags flags = 0)
            => new(label, hint, get, value =>
            {
                set(value);
                afterChange?.Invoke();
            }, maxLength, flags);

        public static ComboItem Combo(string label, Func<int> get, string[] items, Action<int> set, Action? afterChange = null)
            => new(label, get, items, value =>
            {
                set(value);
                afterChange?.Invoke();
            });

        public static ComboItem Combo(string label, Func<int> get, Func<string[]> itemsProvider, Action<int> set, Action? afterChange = null)
            => new(label, get, itemsProvider, value =>
            {
                set(value);
                afterChange?.Invoke();
            });

        public static SliderIntItem SliderInt(string label, Func<int> get, Action<int> set, int min, int max,
            string format = "%d", Action? afterChange = null)
            => new(label, get, min, max, format, value =>
            {
                set(value);
                afterChange?.Invoke();
            });

        public static DragFloatItem DragFloat(string label, Func<float> get, Action<float> set,
            Action? afterChange = null, float speed = 1f, float min = float.MinValue,
            float max = float.MaxValue, string format = "%.0f")
            => new(label, get, value =>
            {
                set(value);
                afterChange?.Invoke();
            }, speed, min, max, format);

        public static DragFloat2Item DragFloat2(string label, Func<Vector2> get, Action<Vector2> set,
            Action? afterChange = null, float speed = 1f, float min = 1f,
            float max = float.MaxValue, string format = "%.0f")
            => new(label, get, value =>
            {
                set(value);
                afterChange?.Invoke();
            }, speed, min, max, format);

        public static ColorPickerItem Color(string label, Func<Vector4> get, Action<Vector4> set, Action? afterChange = null)
            => new(label, get, value =>
            {
                set(value);
                afterChange?.Invoke();
            });

        public static BlankItem Blank()
            => new();

        public static ColorTableItem ColorTable(string tableKey, int columns, params WindowItem[] items)
            => new(tableKey, columns, items);

        public static TableRootItem Table(string tableKey, ImGuiTableFlags flags, string[] columnHeaders,
            Func<TableRootItem.TableRow[]> rowDataProvider)
            => new(tableKey, flags, columnHeaders, rowDataProvider);

        public static TableRootItem Table(string tableKey, ImGuiTableFlags flags, TableRootItem.TableColumn[] columns,
            Func<TableRootItem.TableRow[]> rowDataProvider)
            => new(tableKey, flags, columns, rowDataProvider);

        public static TableRootItem.TableColumn TableColumn(string header,
            ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.None, float initWidthOrWeight = 0f)
            => new(header, flags, initWidthOrWeight);

        public static TableRootItem.TableRow TableRow(string[] cells)
            => new(cells);

        public static ButtonItem Button(string label, Action onClick)
            => new(label, onClick);

        public static IconButtonItem IconButton(FontAwesomeIcon icon, string tooltip, Action onClick)
            => new(icon, tooltip, onClick);

        public static ItemWidth Width(float width, WindowItem child)
            => new(width, child);

        public static InlineGroupItem SameLine(params WindowItem[] items)
            => new(items);

        public static SettingsTab Tab(string name, params SectionHeader[] sections)
            => new()
            {
                Name = name,
                Sections = sections,
            };

        public static SectionHeader Section(string title, ImGuiTreeNodeFlags flags, params WindowItem[] items)
            => new()
            {
                Title = title,
                Flags = flags,
                Items = items,
            };
    }

    /// <summary>
    /// Renders a slider (int). Re-reads value each frame via getValue callback.
    // </summary>
    internal sealed class SliderIntItem : WindowItem
    {
        public string Label = null!;
        public Func<int> GetValue = null!;
        public Action<int>? OnChange;
        public int Min, Max;
        public string Format = null!;

        public SliderIntItem(string label, Func<int> getValue, int min, int max, string format = "%d", Action<int>? onChange = null)
        {
            Label = label;
            GetValue = getValue;
            Min = min;
            Max = max;
            Format = format;
            OnChange = onChange;
        }

        /// <summary>
        /// Draws integer slider using shared ImGui value-binding pattern (see file header).
        /// </summary>
        public override void Draw()
        {
            int v = GetValue();
            if (ImGui.SliderInt(Label, ref v, Min, Max, Format))
                OnChange?.Invoke(v);
        }
    }

    /// <summary>
    /// Renders a drag float (single value).
    // </summary>
    internal sealed class DragFloatItem : WindowItem
    {
        public string Label = null!;
        public Func<float> GetValue = null!;
        public Action<float> SetValue = null!;
        public float Speed = 1f;
        public float Min = float.MinValue;
        public float Max = float.MaxValue;
        public string Format = "%.0f";

        public DragFloatItem(string label, Func<float> getValue, Action<float> setValue, float speed = 1f, float min = float.MinValue, float max = float.MaxValue, string format = "%.0f")
        {
            Label = label;
            GetValue = getValue;
            SetValue = setValue;
            Speed = speed;
            Min = min;
            Max = max;
            Format = format;
        }

        /// <summary>
        /// Draws float drag control using shared ImGui value-binding pattern (see file header).
        /// NOTE: PushItemWidth/PopItemWidth wraps the widget. If combined with DragFloat2Item
        /// (which also calls PushItemWidth/PopItemWidth), nested calls would stack. Not an
        /// issue currently, but be aware if these items are ever used together in a table.
        /// </summary>
        public override void Draw()
        {
            ImGui.PushItemWidth(80);
            try
            {
                float v = GetValue();
                if (ImGui.DragFloat(Label, ref v, Speed, Min, Max, Format))
                    SetValue(v);
            }
            finally
            {
                ImGui.PopItemWidth();
            }
        }
    }

    /// <summary>
    /// Renders a drag float2 (two integer-like floats, e.g. position or size).
    // </summary>
    internal sealed class DragFloat2Item : WindowItem
    {
        public string Label = null!;
        public Func<Vector2> GetValue = null!;
        public Action<Vector2> SetValue = null!;
        public float Speed = 1f;
        public float Min = 1f;
        public float Max = float.MaxValue;
        public string Format = "%.0f";

        public DragFloat2Item(string label, Func<Vector2> getValue, Action<Vector2> setValue, float speed = 1f, float min = 1f, float max = float.MaxValue, string format = "%.0f")
        {
            Label = label;
            GetValue = getValue;
            SetValue = setValue;
            Speed = speed;
            Min = min;
            Max = max;
            Format = format;
        }

        /// <summary>
        /// Draws float2 drag control using shared ImGui value-binding pattern (see file header).
        /// NOTE: PushItemWidth/PopItemWidth wraps the widget. Same stacking caveat as DragFloatItem —
        /// nested calls would stack if used together with DragFloatItem in a table.
        /// </summary>
        public override void Draw()
        {
            ImGui.PushItemWidth(80);
            try
            {
                var v = GetValue();
                if (ImGui.DragFloat2(Label, ref v, Speed, Min, Max, Format))
                    SetValue(v);
            }
            finally
            {
                ImGui.PopItemWidth();
            }
        }
    }

    /// <summary>
    /// Renders a color picker.
    // </summary>
    internal sealed class ColorPickerItem : WindowItem
    {
        public string Label = null!;
        public Func<Vector4> GetValue = null!;
        public Action<Vector4> SetValue = null!;

        public ColorPickerItem(string label, Func<Vector4> getValue, Action<Vector4> setValue)
        {
            Label = label;
            GetValue = getValue;
            SetValue = setValue;
        }

        public override void Draw()
        {
            var color = GetValue();
            if (ImGui.ColorEdit4(Label, ref color, ImGuiColorEditFlags.NoInputs))
                SetValue(color);
        }
    }

    /// <summary>
    /// Renders plain text (no format string interpretation).
    // </summary>
    internal sealed class TextItem : WindowItem
    {
        public string Text = null!;

        public TextItem(string text) => Text = text;

        public override void Draw() => ImGui.TextUnformatted(Text);
    }

    /// <summary>
    /// Renders dynamic text via a callback, allowing values to change without rebuilding the item.
    // </summary>
    internal sealed class DynamicTextItem : WindowItem
    {
        public Func<string> GetValue = null!;

        public DynamicTextItem(Func<string> getValue) => GetValue = getValue;

        public override void Draw() => ImGui.TextUnformatted(GetValue());
    }

    /// <summary>
    /// Renders a button with an action.
    // </summary>
    internal sealed class ButtonItem : WindowItem
    {
        public string Label = null!;
        public Action? OnClick;

        public ButtonItem(string label, Action onClick)
        {
            Label = label;
            OnClick = onClick;
        }

        public override void Draw()
        {
            if (ImGui.Button(Label))
                OnClick?.Invoke();
        }
    }

    /// <summary>
    /// Renders an icon button with a tooltip/action.
    // </summary>
    internal sealed class IconButtonItem : WindowItem
    {
        public FontAwesomeIcon Icon;
        public string Tooltip = null!;
        public Action? OnClick;

        public IconButtonItem(FontAwesomeIcon icon, string tooltip, Action onClick)
        {
            Icon = icon;
            Tooltip = tooltip;
            OnClick = onClick;
        }

        public override void Draw()
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var iconText = Icon.ToIconString();
            var clicked = ImGui.Button($"{iconText}##{iconText}-{Tooltip}");
            ImGui.PopFont();

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(Tooltip);
                ImGui.EndTooltip();
            }

            if (clicked)
                OnClick?.Invoke();
        }
    }

    /// <summary>
    /// Renders NextItemWidth + child item.
    // </summary>
    internal sealed class ItemWidth : WindowItem
    {
        public float Width;
        public WindowItem Child = null!;

        public ItemWidth(float width, WindowItem child)
        {
            Width = width;
            Child = child;
        }

        public override void Draw()
        {
            ImGui.SetNextItemWidth(Width);
            Child.Draw();
        }
    }

    /// <summary>
    /// Used to trigger an intentionally empty element (a blank table cell)
    /// </summary>
    internal sealed class BlankItem : WindowItem
    {
        public BlankItem() { }

        public override void Draw()
        {
            // intentionally empty
        }
    }

    /// <summary>
    /// Renders a table with color items.
    /// Accepts WindowItem[] to allow mixing ColorPickerItem and TableBlankItem.
    /// </summary>
    internal sealed class ColorTableItem : WindowItem
    {
        public string TableKey = null!;
        public WindowItem[] Items = null!;
        public int Columns = 2;

        public ColorTableItem(string tableKey, int columns, WindowItem[] items)
        {
            TableKey = tableKey;
            Items = items;
            Columns = columns;
        }

        public override void Draw()
        {
            if (ImGui.BeginTable(TableKey + "##colortable", Columns))
            {
                foreach (var item in Items)
                {
                    ImGui.TableNextColumn();
                    item.Draw();
                }
                ImGui.EndTable();
            }
        }
    }

    /// <summary>
    /// Renders child items on one line.
    // </summary>
    internal sealed class InlineGroupItem : WindowItem
    {
        public WindowItem[] Items = null!;

        public InlineGroupItem(params WindowItem[] items) => Items = items;

        public override void Draw()
        {
            if (Items == null || Items.Length == 0)
                return;

            Items[0].Draw();
            for (var i = 1; i < Items.Length; i++)
            {
                ImGui.SameLine();
                Items[i].Draw();
            }
        }
    }


    /// <summary>
    /// Encapsulates BeginTable/SetupColumn/HeadersRow/NextRow/SetColumnIndex/TextUnformatted/EndTable.
    /// Column count is derived from Columns.Length for safety.
    /// All data is declared inline via Func<TableRow[]>.
    /// </summary>
    internal sealed class TableRootItem : WindowItem
    {
        public string TableKey = null!;
        public ImGuiTableFlags Flags;
        public TableColumn[] Columns = null!;
        public Func<TableRow[]> RowDataProvider = null!;

        public TableRootItem(string tableKey, ImGuiTableFlags flags,
            string[] columnHeaders, Func<TableRow[]> rowDataProvider)
            : this(tableKey, flags, Array.ConvertAll(columnHeaders, header => new TableColumn(header)), rowDataProvider)
        {
        }

        public TableRootItem(string tableKey, ImGuiTableFlags flags,
            TableColumn[] columns, Func<TableRow[]> rowDataProvider)
        {
            TableKey = tableKey;
            Flags = flags;
            Columns = columns;
            RowDataProvider = rowDataProvider;
        }

        public override void Draw()
        {
            var columnCount = Columns.Length;
            if (columnCount == 0)
                return;

            if (ImGui.BeginTable(TableKey + "##table", columnCount, Flags))
            {
                for (int i = 0; i < Columns.Length; i++)
                {
                    var column = Columns[i];
                    ImGui.TableSetupColumn(column.Header, column.Flags, column.InitWidthOrWeight);
                }

                ImGui.TableHeadersRow();

                var rows = RowDataProvider();
                foreach (var row in rows)
                {
                    ImGui.TableNextRow();
                    var cellCount = Math.Min(Columns.Length, row.Cells.Length);
                    for (int c = 0; c < cellCount; c++)
                    {
                        ImGui.TableSetColumnIndex(c);
                        ImGui.TextUnformatted(row.Cells[c]);
                    }
                }

                ImGui.EndTable();
            }
        }

        public sealed class TableColumn
        {
            public string Header;
            public ImGuiTableColumnFlags Flags;
            public float InitWidthOrWeight;

            public TableColumn(string header, ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.None, float initWidthOrWeight = 0f)
            {
                Header = header;
                Flags = flags;
                InitWidthOrWeight = initWidthOrWeight;
            }
        }

        public sealed class TableRow
        {
            public string[] Cells;
            public TableRow(string[] cells) => Cells = cells;
        }
    }

    /// <summary>
    /// Renders multiple child items computed via a Func each frame.
    /// </summary>
    internal sealed class CompositeItem : WindowItem
    {
        public Func<WindowItem[]> ChildrenProvider = null!;

        public CompositeItem(Func<WindowItem[]> childrenProvider) => ChildrenProvider = childrenProvider;

        public override void Draw()
        {
            foreach (var child in ChildrenProvider())
                child.Draw();
        }
    }
}
