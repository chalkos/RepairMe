using System;
using System.Collections.Generic;
using Dalamud.Interface.ManagedFontAtlas;

namespace RepairMe
{
    /// <summary>
    /// Holds alert font cache state. Encapsulates the scale-to-font dictionary,
    /// lock object, and disposal logic.
    /// </summary>
    internal sealed class AlertFontState
    {
        // Each scale builds its own font atlas, so snap to the slider step and cap the cache (LRU).
        private const int MaxHandles = 12;
        private readonly Dictionary<float, (IFontHandle Handle, long LastUsed)> _handles = new();
        private readonly object _lock = new();
        private long _tick;

        public IFontHandle? GetOrAdd(float scale, Func<float, IFontHandle> factory)
        {
            lock (_lock)
            {
                var roundedScale = MathF.Round(scale * 20f) / 20f;
                _tick++;
                if (_handles.TryGetValue(roundedScale, out var existing))
                {
                    _handles[roundedScale] = (existing.Handle, _tick);
                    return existing.Handle;
                }

                if (_handles.Count >= MaxHandles)
                {
                    var oldest = default(KeyValuePair<float, (IFontHandle Handle, long LastUsed)>);
                    var found = false;
                    foreach (var entry in _handles)
                    {
                        if (found && entry.Value.LastUsed >= oldest.Value.LastUsed) continue;
                        oldest = entry;
                        found = true;
                    }

                    oldest.Value.Handle.Dispose();
                    _handles.Remove(oldest.Key);
                }

                var result = factory(roundedScale);
                _handles[roundedScale] = (result, _tick);
                return result;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var entry in _handles.Values)
                    entry.Handle.Dispose();
                _handles.Clear();
            }
        }
    }
}
