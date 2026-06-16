namespace Main.Game
{
    // ゲーム開始時の初期手札枚数のルール定数（純データ）。
    // コイントスで決まった先攻/後攻に応じて配る枚数が変わる（先攻3枚・後攻4枚）。
    // 先攻の手札が少ないのは、両プレイヤーの初手がドローなしであることと合わせた先攻有利の補正。
    // MainPresenter（オフライン配牌）と NetworkGameService（オンラインのホスト配牌）の双方で共用する。
    public static class MulliganRule
    {
        // 先攻プレイヤーの初期手札枚数
        public const int FirstPlayerHandSize = 3;

        // 後攻プレイヤーの初期手札枚数
        public const int SecondPlayerHandSize = 4;

        // 先攻かどうかに応じた初期手札枚数を返す
        public static int InitialHandSize(bool isFirst)
        {
            return isFirst ? FirstPlayerHandSize : SecondPlayerHandSize;
        }
    }
}
