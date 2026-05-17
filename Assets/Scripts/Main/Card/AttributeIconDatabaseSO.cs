using System;
using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "AttributeIconDatabase", menuName = "Card/Attribute Icon Database")]
    public sealed class AttributeIconDatabaseSO : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public CardAttribute Attribute;
            public Sprite Icon;
        }

        [SerializeField] private List<Entry> _entries;

        public Sprite GetIcon(CardAttribute attribute)
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Attribute == attribute)
                {
                    return entry.Icon;
                }
            }

            return null;
        }
    }
}
