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
        // 空 ID・重複 ID に E### を自動採番する（既存の一意な ID は変更しない）
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
