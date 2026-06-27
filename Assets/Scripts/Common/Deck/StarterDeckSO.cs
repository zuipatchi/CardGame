using System.Collections.Generic;
using UnityEngine;

namespace Common.Deck
{
    // 初回起動時にスロット 0 へ自動生成する初期デッキの中身（カードIDの配列）。
    // 対戦時の参照はカードIDのみ（コストは CardData から再解決される）ため、ここでは ID だけを持つ。
    // StarterDeckSeeder が Addressables キー `Deck/StarterDeck` でロードして使う。
    [CreateAssetMenu(fileName = "StarterDeck", menuName = "Card Game/Starter Deck")]
    public sealed class StarterDeckSO : ScriptableObject
    {
        public List<string> CardIds = new List<string>();
    }
}
