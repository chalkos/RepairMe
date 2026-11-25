using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using UiConstants = RepairMe.RepairMeConstants.Ui;

namespace RepairMe
{
    internal partial class PluginUi : IOverlayHost
    {
        bool IOverlayHost.UnlockedUiMode => _const.isUiUnlocked;
        ImGuiWindowFlags IOverlayHost.PrepareWindow(Vector2 position) => PrepareWindow(position);
        void IOverlayHost.PushWindowEditingStyle(bool minSize) => PushWindowEditingStyle(minSize);
        void IOverlayHost.PopWindowEditingStyle(bool minSize) => PopWindowEditingStyle(minSize);
        void IOverlayHost.MigratePositions(ref Vector2 position) => MigratePositions(ref position);
        void IOverlayHost.CheckDrag(ref Vector2 position) => CheckDrag(ref position);
        void IOverlayHost.DrawBar(BarState state, int orientation, Vector2 size, float rounding, float borderSize, Vector4 borderColor)
            => DrawBar(state.Progress, orientation, size, state.FgColor, state.BgColor, state.ShowPoints, state.Points, state.PointsColor, rounding, borderSize, borderColor);
        void IOverlayHost.DrawTextOverlay(string text, Vector4 color, Vector4 background, ref Vector2 position, float? scale, Action? clickAction)
            => DrawTextOverlay(text, color, background, ref position, scale, clickAction);

        private void CheckDrag(ref Vector2 pos)
        {
            // https://github.com/UnknownX7/QoLBar/blob/f73515daf812058d0af58cdbd0942249474e9cf8/UI/BarUI.cs
            if (_const.isDrawingFirstFrame) return;

            var dragging = !_const.isDragging
                ? ImGui.IsWindowFocused() && ImGui.IsWindowHovered() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                  ImGui.IsMouseDragging(ImGuiMouseButton.Left, 5)
                : _const.isDragging && !ImGui.IsMouseReleased(ImGuiMouseButton.Left);

            if (dragging && !_const.isDragging)
                _const.isDragging = true;

            if (_const.isDragging && ImGui.IsWindowFocused())
            {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0);
                ImGui.ResetMouseDragDelta();
                pos.X += delta.X;
                pos.Y += delta.Y;
            }

            if (!dragging && _const.isDragging)
            {
                _const.isDragging = false;
                Conf.Save();
            }
        }

        private ImGuiWindowFlags PrepareWindow(Vector2 pos)
        {
            var wFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
                         ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;

            ImGuiHelpers.ForceNextWindowMainViewport();

            if (Conf.PositionsMigrated && !_const.isDragging)
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + pos);

            if (_const.isUiUnlocked) return wFlags;

            wFlags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground;

            return wFlags;
        }

        private void PushWindowEditingStyle(bool minSize = false)
        {
            if (minSize)
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0, ImGui.GetFontSize()));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3f);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1f, 0f, 0f, 0.3f));
        }

        private void PopWindowEditingStyle(bool minSize = false)
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(minSize ? 2 : 1);
        }

        private BarState SelectConditionBarState(float conditionValue)
        {
            bool alt = Conf.AltCharacters.ContainsKey(PlayerState.ContentId);
            if (alt && conditionValue <= Conf.ThresholdConditionCriticalAlt
                || !alt && conditionValue <= Conf.ThresholdConditionCritical)
                return new BarState(conditionValue / 100f,
                    Conf.BarConditionCriticalColor, Conf.BarConditionCriticalBackground,
                    false, UiConstants.EmptyPoints, Vector4.Zero);
            if (alt && conditionValue <= Conf.ThresholdConditionLowAlt
                || !alt && conditionValue <= Conf.ThresholdConditionLow)
                return new BarState(conditionValue / 100f,
                    Conf.BarConditionLowColor, Conf.BarConditionLowBackground,
                    false, UiConstants.EmptyPoints, Vector4.Zero);
            return new BarState(conditionValue / 100f,
                Conf.BarConditionOkColor, Conf.BarConditionOkBackground,
                false, UiConstants.EmptyPoints, Vector4.Zero);
        }

        private BarState SelectSpiritbondBarState(float spiritbondValue)
        {
            if (spiritbondValue < UiConstants.SpiritbondFullThreshold)
                return new BarState(spiritbondValue / 100f,
                    Conf.BarSpiritbondProgressColor, Conf.BarSpiritbondProgressBackground,
                    Conf.BarSpiritbondShowAllItems, _const.spiritbondPoints, Conf.BarSpiritbondPointsColor);
            return new BarState(spiritbondValue / 100f,
                Conf.BarSpiritbondFullColor, Conf.BarSpiritbondFullBackground,
                Conf.BarSpiritbondShowAllItems, _const.spiritbondPoints, Conf.BarSpiritbondPointsColor);
        }

        private void DrawBar(float progress, int orientation, Vector2 size, Vector4 fgColor,
            Vector4 bgColor, bool showPoints, float[] points, Vector4 pointsColor,
            float rounding, float borderSize, Vector4 borderColor)
        {
            float innerOffset = borderSize;
            float outerOffset = MathF.Ceiling(borderSize / 2f) - 0.5f;
            var screenPos = ImGui.GetCursorScreenPos();
            var pointTopLeftOuter = screenPos + Vector2.One * outerOffset;
            var sizeOuter = size - Vector2.One * outerOffset * 2;
            var pointTopLeft = screenPos + Vector2.One * innerOffset;
            var drawSize = size - Vector2.One * innerOffset * 2;

            var bdl = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());

            if (borderSize > 0)
                bdl.AddRect(pointTopLeftOuter,
                    new Vector2(pointTopLeftOuter.X + sizeOuter.X, pointTopLeftOuter.Y + sizeOuter.Y),
                    ImGui.GetColorU32(borderColor), rounding, ImDrawFlags.Closed, borderSize);

            bdl.AddRectFilled(pointTopLeft,
                new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y),
                ImGui.GetColorU32(bgColor), rounding);

            // Inline ComputeClipRect
            (Vector2 clipMin, Vector2 clipMax) = orientation switch
            {
                0 => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X * progress, pointTopLeft.Y + drawSize.Y)),
                1 => (new Vector2(pointTopLeft.X + drawSize.X * (1 - progress), pointTopLeft.Y),
                       new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y)),
                2 => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y * progress)),
                3 => (new Vector2(pointTopLeft.X, pointTopLeft.Y + drawSize.Y * (1 - progress)),
                       new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y)),
                _ => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y))
            };
            bdl.PushClipRect(clipMin, clipMax);
            bdl.AddRectFilled(pointTopLeft,
                new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y),
                ImGui.GetColorU32(fgColor), rounding);
            bdl.PopClipRect();

            if (!showPoints) return;

            for (int i = 0; i < points.Length; i++)
            {
                if (progress < points[i] || points[i] < 0) continue;

                // Inline ComputePointClipRect
                (Vector2 pointClipMin, Vector2 pointClipMax) = orientation switch
                {
                    0 => (new Vector2(pointTopLeft.X + drawSize.X * points[i], pointTopLeft.Y),
                           new Vector2(pointTopLeft.X + drawSize.X * points[i] + UiConstants.BarPointSize, pointTopLeft.Y + drawSize.Y)),
                    1 => (new Vector2(pointTopLeft.X + drawSize.X * (1 - points[i]), pointTopLeft.Y),
                           new Vector2(pointTopLeft.X + drawSize.X * (1 - points[i]) + UiConstants.BarPointSize, pointTopLeft.Y + drawSize.Y)),
                    2 => (new Vector2(pointTopLeft.X, pointTopLeft.Y + drawSize.Y * points[i]),
                           new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y * points[i] + UiConstants.BarPointSize)),
                    3 => (new Vector2(pointTopLeft.X, pointTopLeft.Y + drawSize.Y * (1 - points[i])),
                           new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y * (1 - points[i]) + UiConstants.BarPointSize)),
                    _ => (pointTopLeft, new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y))
                };
                bdl.PushClipRect(pointClipMin, pointClipMax);
                bdl.AddRectFilled(pointTopLeft,
                    new Vector2(pointTopLeft.X + drawSize.X, pointTopLeft.Y + drawSize.Y),
                    ImGui.GetColorU32(pointsColor), rounding);
                bdl.PopClipRect();
            }
        }

        private void DrawTextOverlay(string text, Vector4 color, Vector4 background,
            ref Vector2 position, float? alertScale = null, Action? clickAction = null)
        {
            var fontHandle = alertScale.HasValue ? GetOrCreateAlertFont(alertScale.Value) : null;

            var baseTextSize = ImGui.CalcTextSize(text);
            var scaledTextSize = baseTextSize * (alertScale ?? 1.0f);
            var padding = Vector2.One * 2;
            padding.X += UiConstants.ProgressLabelPadding * 2;
            var childSize = scaledTextSize + padding;

            var childFlags = (_const.isUiUnlocked || clickAction != null) ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 5f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, background);

            try
            {
                if (ImGui.BeginChild("##child", childSize, false, childFlags))
                {
                    if (_const.isUiUnlocked)
                        CheckDrag(ref position);

                    var textPos = new Vector2(
                        (childSize.X - scaledTextSize.X) * 0.5f,
                        (childSize.Y - scaledTextSize.Y) * 0.5f);

                    using (fontHandle?.Push())
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.SetCursorPos(textPos);
                        ImGui.TextUnformatted(text);
                        ImGui.PopStyleColor();
                    }

                    if (!_const.isUiUnlocked && clickAction != null && ImGui.IsWindowHovered() &&
                        ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                        clickAction.Invoke();
                }
            }
            finally
            {
                ImGui.EndChild();
                ImGui.PopStyleColor();
                ImGui.PopStyleVar();
            }
        }

        private static string FormatPercentText(float value, float? extraValue, bool showSign, bool showDecimals)
        {
            string Format(float v) => showSign && showDecimals ? $"{v:F2}%"
                : showSign ? $"{Math.Floor(v):F0}%"
                : showDecimals ? $"{v:F2}"
                : $"{Math.Floor(v):F0}";

            string text = Format(value);

            if (extraValue != null)
                text = Format(extraValue.Value) + " / " + text;

            return text;
        }

        private IFontHandle? GetOrCreateAlertFont(float scale)
        {
            return _const._alertFonts.GetOrAdd(scale, roundedScale =>
            {
                var targetFontSize = UiBuilder.DefaultFontSizePx * roundedScale;
                return _pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
                    e.OnPreBuild(tk =>
                        tk.AddDalamudDefaultFont(targetFontSize)));
            });
        }

        private void MigratePositions(ref Vector2 position)
        {
            if (Conf.PositionsMigrated || !_const.isDrawingFirstFrame) return;

            var pos = ImGui.GetWindowPos() - ImGui.GetMainViewport().Pos;
            position.X = pos.X;
            position.Y = pos.Y;
        }
    }
}
