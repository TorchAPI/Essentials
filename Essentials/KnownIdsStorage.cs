using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Essentials
{
    public sealed class KnownIdsStorage
    {
        readonly HashSet<ulong> _steamIds;
        readonly string _filePath;

        public KnownIdsStorage(string filePath)
        {
            _filePath = filePath;
            _steamIds = new HashSet<ulong>();
        }

        public void Read()
        {
            if (!File.Exists(_filePath)) return;

            _steamIds.Clear();
            foreach (var line in File.ReadAllLines(_filePath))
            {
                if (ulong.TryParse(line.Trim(), out var steamId))
                {
                    _steamIds.Add(steamId);
                }
            }
        }

        public bool Contains(ulong steamId)
        {
            return _steamIds.Contains(steamId);
        }

        public void Add(ulong steamId)
        {
            if (_steamIds.Add(steamId))
            {
                var lines = _steamIds.Select(s => $"{s}");
                File.WriteAllLines(_filePath, lines);
            }
        }
    }
}