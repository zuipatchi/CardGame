using System;
using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "AttributeDatabase", menuName = "Card/Attribute Database")]
    public class AttributeDatabaseSO : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public CardAttribute Attribute;
            public Sprite Icon;
            public CardAttribute Weakness;
            public CardAttribute Strength;
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

        public CardAttribute GetWeakness(CardAttribute attribute)
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Attribute == attribute)
                {
                    return entry.Weakness;
                }
            }

            return CardAttribute.None;
        }

        public CardAttribute GetStrength(CardAttribute attribute)
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Attribute == attribute)
                {
                    return entry.Strength;
                }
            }

            return CardAttribute.None;
        }
    }
}
