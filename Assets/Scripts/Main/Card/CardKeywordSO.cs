using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    // カードに設定できる「特徴（キーワード）」のマスターリスト。
    // カードエディタの特徴ドロップダウンはこの SO の一覧から生成する（エディタ専用の用途）。
    // カード側は CardData._keyword に文字列を保持し、マッチング（同じ特徴か）は文字列一致で判定するため、
    // 実行時にこの SO をロードする必要はない（追加の特徴はこの SO に登録するだけでコード変更不要）。
    [CreateAssetMenu(fileName = "CardKeywords", menuName = "Card/Card Keywords")]
    public sealed class CardKeywordSO : ScriptableObject
    {
        // 登録済みの特徴名（例: 獣 / 竜 / 機械 …）。空文字＝特徴なしは登録しない。
        [SerializeField] private List<string> _keywords = new List<string>();

        public IReadOnlyList<string> Keywords => _keywords;
    }
}
