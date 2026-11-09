using System;
using System.Collections.Generic;

namespace UnitySisters.Table
{
    public interface IReadOnlyCharacterAITable
    {
        bool TryGet(string key, out CharacterAIData data);
    }

    public sealed class CharacterAITable : IReadOnlyCharacterAITable
    {
        private readonly Dictionary<string, CharacterAIData> rows = new(StringComparer.Ordinal);
        public void Register(IReadOnlyList<CharacterAIData> data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var next = new Dictionary<string, CharacterAIData>(data.Count, StringComparer.Ordinal);
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i] == null) throw new ArgumentException("Missing AI table row.");
                data[i].Validate();
                if (!next.TryAdd(data[i].Key, data[i])) throw new ArgumentException("Duplicate character AI key.");
            }
            rows.Clear();
            foreach (var pair in next) rows.Add(pair.Key, pair.Value);
        }
        public void Clear() { rows.Clear(); }
        bool IReadOnlyCharacterAITable.TryGet(string key, out CharacterAIData data)
        {
            data = null;
            return !string.IsNullOrEmpty(key) && rows.TryGetValue(key, out data) && data != null;
        }
    }
}
