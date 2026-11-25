using System;
using System.Collections.Generic;
using System.Linq;

namespace RepairMe
{
    /// <summary>
    /// Caches the position profile names shown in the settings combo so the list is not rebuilt every frame.
    /// The cache refreshes when the profile count changes or <see cref="Invalidate"/> is called.
    /// </summary>
    internal sealed class ResolutionOptionsCache
    {
        private string[] _options = Array.Empty<string>();
        private int _count = -1;

        internal string[] Get(Dictionary<string, PositionProfile> profiles)
        {
            if (_count != profiles.Count)
            {
                _options = profiles.Keys.ToArray();
                _count = _options.Length;
            }

            return _options;
        }

        internal void Invalidate() => _count = -1;
    }
}
