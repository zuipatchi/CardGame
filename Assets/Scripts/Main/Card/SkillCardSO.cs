using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "SkillCards", menuName = "Card/Skill Cards")]
    public sealed class SkillCardSO : ScriptableObject
    {
        [SerializeField] private List<SkillCardData> _cards;

        public IReadOnlyList<SkillCardData> Cards => _cards;
    }
}
