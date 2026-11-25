using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RepairMe
{
    internal interface IOverlayHost
    {
        bool UnlockedUiMode { get; }
        ImGuiWindowFlags PrepareWindow(Vector2 position);
        void PushWindowEditingStyle(bool minSize = false);
        void PopWindowEditingStyle(bool minSize = false);
        void MigratePositions(ref Vector2 position);
        void CheckDrag(ref Vector2 position);
        void DrawBar(BarState state, int orientation, Vector2 size, float rounding, float borderSize, Vector4 borderColor);
        void DrawTextOverlay(string text, Vector4 color, Vector4 background, ref Vector2 position, float? scale = null, Action? clickAction = null);
    }

    internal sealed class OverlayBuilder
    {
        public OverlayItem[] Items = Array.Empty<OverlayItem>();

        public void Draw()
        {
            foreach (var item in Items)
            {
                if (item.ShouldRender())
                    item.Render();
            }
        }
    }

    internal abstract class OverlayItem
    {
        public abstract bool ShouldRender();
        public abstract void Render();
    }

    internal readonly record struct BarState(
        float Progress,
        Vector4 FgColor,
        Vector4 BgColor,
        bool ShowPoints,
        float[] Points,
        Vector4 PointsColor
    );

    internal sealed class BarOverlayItem : OverlayItem
    {
        private readonly IOverlayHost _host;
        private readonly Func<bool> _shouldRender;
        private readonly Func<Vector2> _positionProvider;
        private readonly Action<Vector2> _positionWriter;
        private readonly string _windowName;
        private readonly Func<Vector2> _sizeProvider;
        private readonly Func<int> _orientationProvider;
        private readonly Func<float> _roundingProvider;
        private readonly Func<float> _borderSizeProvider;
        private readonly Func<Vector4> _borderColorProvider;
        private readonly Func<BarState> _stateProvider;

        public BarOverlayItem(IOverlayHost host,
            Func<bool> shouldRender,
            Func<Vector2> positionProvider,
            Action<Vector2> positionWriter,
            string windowName,
            Func<Vector2> sizeProvider,
            Func<int> orientationProvider,
            Func<float> roundingProvider,
            Func<float> borderSizeProvider,
            Func<Vector4> borderColorProvider,
            Func<BarState> stateProvider)
        {
            _host = host;
            _shouldRender = shouldRender;
            _positionProvider = positionProvider;
            _positionWriter = positionWriter;
            _windowName = windowName;
            _sizeProvider = sizeProvider;
            _orientationProvider = orientationProvider;
            _roundingProvider = roundingProvider;
            _borderSizeProvider = borderSizeProvider;
            _borderColorProvider = borderColorProvider;
            _stateProvider = stateProvider;
        }

        public override bool ShouldRender() => _shouldRender();

        public override void Render()
        {
            var position = _positionProvider();
            var size = _sizeProvider();
            var windowFlags = _host.PrepareWindow(position);
            _host.PushWindowEditingStyle();
            try
            {
                ImGui.SetNextWindowSize(size + ImGui.GetStyle().WindowPadding * 2);
                if (ImGui.Begin(_windowName, windowFlags))
                {
                    _host.MigratePositions(ref position);
                    _host.CheckDrag(ref position);
                    _positionWriter(position);

                    var state = _stateProvider();
                    var borderColor = _host.UnlockedUiMode ? new Vector4(1, 0, 0, 1) : _borderColorProvider();

                    _host.DrawBar(
                        state,
                        _orientationProvider(),
                        size,
                        _roundingProvider(),
                        _borderSizeProvider(),
                        borderColor);
                }
            }
            finally
            {
                ImGui.End();
                _host.PopWindowEditingStyle();
            }
        }
    }

    internal sealed class TextOverlayItem : OverlayItem
    {
        private readonly IOverlayHost _host;
        private readonly Func<bool> _shouldRender;
        private readonly Func<string> _textProvider;
        private readonly Func<Vector2> _positionProvider;
        private readonly Action<Vector2> _positionWriter;
        private readonly string _windowName;
        private readonly Func<Vector4> _colorProvider;
        private readonly Func<Vector4> _backgroundProvider;
        private readonly bool _useMinimumWindowSize;
        private readonly Func<float?> _scaleProvider;
        private readonly Func<Action?> _clickActionProvider;

        public TextOverlayItem(IOverlayHost host,
            Func<bool> shouldRender,
            Func<string> textProvider,
            Func<Vector2> positionProvider,
            Action<Vector2> positionWriter,
            string windowName,
            Func<Vector4> colorProvider,
            Func<Vector4> backgroundProvider,
            bool useMinimumWindowSize,
            Func<float?> scaleProvider,
            Func<Action?> clickActionProvider)
        {
            _host = host;
            _shouldRender = shouldRender;
            _textProvider = textProvider;
            _positionProvider = positionProvider;
            _positionWriter = positionWriter;
            _windowName = windowName;
            _colorProvider = colorProvider;
            _backgroundProvider = backgroundProvider;
            _useMinimumWindowSize = useMinimumWindowSize;
            _scaleProvider = scaleProvider;
            _clickActionProvider = clickActionProvider;
        }

        public override bool ShouldRender() => _shouldRender();

        public override void Render()
        {
            var position = _positionProvider();
            var windowFlags = _host.PrepareWindow(position);
            _host.PushWindowEditingStyle(_useMinimumWindowSize);
            try
            {
                if (ImGui.Begin(_windowName, windowFlags))
                {
                    _host.MigratePositions(ref position);
                    _host.DrawTextOverlay(
                        _textProvider(),
                        _colorProvider(),
                        _backgroundProvider(),
                        ref position,
                        _scaleProvider(),
                        _clickActionProvider());
                    _positionWriter(position);
                }
            }
            finally
            {
                ImGui.End();
                _host.PopWindowEditingStyle(_useMinimumWindowSize);
            }
        }
    }
}
