using System.Collections.Generic;

namespace Common.Deck
{
    public sealed class DeckModel
    {
        public const int MaxCards = 30;

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

        public bool IsReady => Count == MaxCards;
        public bool IsOver => Count > MaxCards;
        public bool IsValid => IsReady;

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

        public int CountOf(string id)
        {
            int count = 0;
            foreach ((string entryId, int _) in _entries)
            {
                if (entryId == id)
                {
                    count++;
                }
            }
            return count;
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

        public void Reorder(IReadOnlyList<string> orderedIds)
        {
            Dictionary<string, List<(string entryId, int entryCost)>> groups =
                new Dictionary<string, List<(string entryId, int entryCost)>>();
            List<string> originalOrder = new List<string>();

            foreach ((string entryId, int entryCost) in _entries)
            {
                if (!groups.ContainsKey(entryId))
                {
                    groups[entryId] = new List<(string entryId, int entryCost)>();
                    originalOrder.Add(entryId);
                }
                groups[entryId].Add((entryId, entryCost));
            }

            _entries.Clear();

            HashSet<string> placed = new HashSet<string>();
            foreach (string id in orderedIds)
            {
                if (!groups.TryGetValue(id, out List<(string entryId, int entryCost)> group))
                {
                    continue;
                }
                foreach ((string eId, int eCost) in group)
                {
                    _entries.Add((eId, eCost));
                }
                placed.Add(id);
            }

            foreach (string id in originalOrder)
            {
                if (placed.Contains(id))
                {
                    continue;
                }
                foreach ((string eId, int eCost) in groups[id])
                {
                    _entries.Add((eId, eCost));
                }
            }
        }
    }
}
