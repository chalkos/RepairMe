using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace RepairMe
{
    internal partial class PluginUi
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            var windows = new List<WindowBuilder>();
            foreach (var window in _const.windowSystem.Windows)
            {
                if (window is WindowBuilder builder)
                    windows.Add(builder);
            }

            foreach (var window in windows)
                TryDisposeUiResource(window.Dispose, "dispose window");

            TryDisposeUiResource(_const.windowSystem.RemoveAllWindows, "remove windows");

            _const.settingsWindow = null;
            _const.debugWindow = null;
            _const.overlayBuilder = null;

            TryDisposeUiResource(_const._dtr.RemoveEntries, "remove DTR entries");
            TryDisposeUiResource(_const._alertFonts.Dispose, "dispose alert fonts");
        }

        private static void TryDisposeUiResource(Action cleanup, string operation)
        {
            try
            {
                cleanup();
            }
            catch (Exception ex)
            {
                RepairMe.Log?.Warning(ex, $"[RepairMe] Failed to {operation} during UI disposal");
            }
        }

        private void DrawSettingsWindow()
        {
            _const.settingsWindow ??= CreateSettingsWindow();
            EnsureWindowRegistered(_const.settingsWindow);

            _const.settingsWindow.Draw(ref _const.settingsVisible);
        }

        private void DrawDebugWindow()
        {
            _const.debugWindow ??= CreateDebugWindow();
            EnsureWindowRegistered(_const.debugWindow);

            _const.debugWindow.Draw(ref _const.debugVisible);
        }

        private void SyncWindowVisibility()
        {
            if (_const.settingsWindow != null)
                _const.settingsVisible = _const.settingsWindow.IsOpen;

            if (_const.debugWindow != null)
                _const.debugVisible = _const.debugWindow.IsOpen;
        }

        private WindowItem CreateAlertSfxRow(string label, Func<uint> getValue, Action<uint> setValue, Action save, string playButtonId)
        {
            return UiItem.SameLine(
                UiItem.Width(80, UiItem.TextInput(label,
                    "",
                    () => getValue().ToString(),
                    v =>
                    {
                        if (!uint.TryParse(v, out var parsed))
                            return;

                        setValue(parsed);
                    },
                    save,
                    maxLength: 10,
                    flags: ImGuiInputTextFlags.CharsDecimal)),
                UiItem.Button("play" + playButtonId, () => CreateTransientWindow(getValue())));
        }

        private void CreateTransientWindow(uint? onOpenSfxId = null, uint? onCloseSfxId = null)
        {
            var openSfxId = onOpenSfxId ?? 0u;
            var closeSfxId = onCloseSfxId ?? 0u;
            var soundEnabled = openSfxId > 0 || closeSfxId > 0;
            var transientWindow = new WindowBuilder
            {
                Title = $"RepairMe SFX###repairme-transient-sfx-{Guid.NewGuid():N}",
                DrawOnce = true,
                IsOpen = true,
                DisableWindowSounds = !soundEnabled,
                DisableFadeInFadeOut = true,
                OnOpenSfxId = openSfxId,
                OnCloseSfxId = closeSfxId,
                Flags = ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoBackground
                    | ImGuiWindowFlags.NoInputs
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoBringToFrontOnFocus
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.AlwaysAutoResize,
                HeaderItems = Array.Empty<WindowItem>(),
                Tabs = Array.Empty<SettingsTab>(),
            };

            transientWindow.RemoveAction = RemoveTransientWindow;
            _const.windowSystem.AddWindow(transientWindow);
        }

        private void RemoveTransientWindow(WindowBuilder window)
        {
            window.RemoveAction = null;
            _const.windowSystem.RemoveWindow(window);
            window.MarkDisposed();
        }

        private void EnsureWindowRegistered(WindowBuilder window)
        {
            foreach (var registeredWindow in _const.windowSystem.Windows)
            {
                if (ReferenceEquals(registeredWindow, window))
                    return;
            }

            _const.windowSystem.AddWindow(window);
        }
    }
}
