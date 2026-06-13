namespace Common.Deck
{
    // デッキ構築ルールの保持。Common 常駐の Singleton として登録し、
    // DeckBuilder / CpuDeckBuilder / Home で共有する。
    public sealed class DeckRuleModel
    {
        // 同一カード（ID 基準）をデッキに入れられる上限枚数。
        public const int MaxCopiesPerId = 3;

        // 同名（同一 ID）カードの枚数制限を有効にするか。
        // 既定で有効。Editor 再生時のみ Home の Toggle で切り替えられる
        // （ビルドでは切り替え UI が存在しないため常に有効）。
        public bool LimitSameCards { get; set; } = true;
    }
}
