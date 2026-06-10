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
        // リスト順に C### を自動採番する（並び替え時も振り直す）
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
