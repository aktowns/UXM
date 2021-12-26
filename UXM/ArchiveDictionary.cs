using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UXM
{
    internal class ArchiveDictionary
    {
        private const uint PRIME = 37;

        private readonly Dictionary<uint, string> _hashes;

        public ArchiveDictionary(string dictionary)
        {
            _hashes = new Dictionary<uint, string>();
            foreach (var line in Regex.Split(dictionary, "[\r\n]+"))
            {
                var trimmed = line.Trim();
                if (trimmed.Length <= 0) continue;
                var hash = ComputeHash(trimmed);
                _hashes[hash] = trimmed;
            }
        }

        private static uint ComputeHash(string path)
        {
            var hashable = path.Trim().Replace('\\', '/').ToLowerInvariant();
            if (!hashable.StartsWith("/"))
                hashable = '/' + hashable;
            return hashable.Aggregate(0u, (i, c) => i * PRIME + c);
        }

        public bool GetPath(uint hash, out string path)
        {
            return _hashes.TryGetValue(hash, out path);
        }
    }
}
