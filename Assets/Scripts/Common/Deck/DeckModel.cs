using System.Collections.Generic;

namespace Common.Deck
{
    public sealed class DeckModel
    {
        public const int TargetCost = 30;

        private readonly List<(string id, int cost)> _entries = new List<(string id, int cost)>();

        public IReadOnlyList<(string id, int cost)> Entries => _entries;
        public int Count => _entries.Count;
        public int TotalCost
        {
            get
            {
                int total = 0;
                foreach ((string _, int cost) in _entries)
                {
                    total += cost;
                }
                return total;
            }
        }

        public bool IsReady => TotalCost == TargetCost;
        public bool IsOver => TotalCost > TargetCost;

        public IReadOnlyList<string> CardIds
        {
            get
            {
                List<string> ids = new List<string>(_entries.Count);
                foreach ((string id, int _) in _entries)
                {
                    ids.Add(id);
                }
                return ids;
            }
        }

        public void Add(string id, int cost)
        {
            _entries.Add((id, cost));
        }

        public bool Remove(string id)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].id == id)
                {
                    _entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
