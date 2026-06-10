using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CharacterCards", menuName = "Card/Character Cards")]
    public sealed class CharacterCardSO : ScriptableObject
    {
        [SerializeField] private List<CharacterCardData> _cards;

        public IReadOnlyList<CharacterCardData> Cards => _cards;

#if UNITY_EDITOR
        // 空 ID・重複 ID に C### を自動採番する（既存の一意な ID は変更しない）
        private void OnValidate()
        {
            if (CardIdAutoAssigner.AssignIds(_cards, "C"))
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
