using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "EventCards", menuName = "Card/Event Cards")]
    public sealed class EventCardSO : ScriptableObject
    {
        [SerializeField] private List<EventCardData> _cards;

        public IReadOnlyList<EventCardData> Cards => _cards;

#if UNITY_EDITOR
        // リスト順に E### を自動採番する（並び替え時も振り直す）
        private void OnValidate()
        {
            if (CardIdAutoAssigner.AssignIds(_cards, "E"))
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
