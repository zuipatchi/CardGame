namespace Common.Tutorial
{
    // チュートリアルの種類。Home の選択モーダルで選び、MainPresenter が ID ごとに台本を分岐する。
    public enum TutorialId
    {
        // きほんのあそびかた：カードの出し方・コストの払い方・ターン終了を体験する。
        BasicLoop,
        // 攻撃の仕方：召喚酔い・タップした相手にしか攻撃できないこと・反撃ダメージを受けないことを体験する。
        AttackBasics,
        // 勝ち方（デッキ切れ）：相手のデッキを0枚にして勝つ。
        DeckOutWin,
        // 勝ち方（制圧）：自分の場にキャラを8体ならべて勝つ。
        FieldCharsWin,
        // 勝ち方（勝利点）：勝利点を20点ためて勝つ。
        VictoryPointsWin,

        // キーワード能力（1キーワード1つ）。
        GuardianKw,      // 守護
        HasteKw,         // 速攻
        FlyingKw,        // 飛行
        SakimoriKw,      // 防人
        AssaultKw,       // 強襲
        NoDeckAttackKw,  // デッキ攻撃×
    }

    // チュートリアル（誘導つきスクリプト対戦）の起動情報を保持する Common 常駐モデル。
    // Home の選択モーダルで Id をセットして IsActive を true にし、Main へ遷移する。
    // MainPresenter が起動時に読み取って即 IsActive を false に戻す（消費型）。
    // これにより、チュートリアルを中断しても次の通常 CPU 戦・オンライン戦にフラグが残らない。
    public sealed class TutorialModel
    {
        public bool IsActive { get; set; }
        public TutorialId Id { get; set; }
    }
}
