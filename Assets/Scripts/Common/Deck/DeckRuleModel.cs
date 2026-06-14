#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Common.Deck
{
    // デッキ構築ルールの保持。Common 常駐の Singleton として登録し、
    // DeckBuilder / CpuDeckBuilder / Home で共有する。
    public sealed class DeckRuleModel
    {
        // 同一カード（ID 基準）をデッキに入れられる上限枚数。
        public const int MaxCopiesPerId = 3;

#if UNITY_EDITOR
        // Editor 再生時のトグル状態を永続化するキー（EditorPrefs）。
        private const string LimitSameCardsPrefKey = "DeckRule.LimitSameCards";
#endif

        // 同名（同一 ID）カードの枚数制限を有効にするか。
        // 既定で有効。Editor 再生時のみ Home の Toggle で切り替えられ、
        // 前回の状態は EditorPrefs に保存されて再生をまたいで引き継がれる
        // （ビルドでは切り替え UI が存在せず EditorPrefs も読まないため常に有効）。
        public bool LimitSameCards { get; set; } = true;

#if UNITY_EDITOR
        public DeckRuleModel()
        {
            LimitSameCards = EditorPrefs.GetBool(LimitSameCardsPrefKey, true);
        }

        // Editor 再生時のトグル変更を永続化する。
        public void SaveLimitSameCards()
        {
            EditorPrefs.SetBool(LimitSameCardsPrefKey, LimitSameCards);
        }
#endif
    }
}
