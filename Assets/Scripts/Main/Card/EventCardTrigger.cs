namespace Main.Card
{
    // イベントカードの効果発動タイミング。
    // OnPlay（カードを使ったとき）はプレイ時に即時解決して墓地へ送る（既定・従来の挙動）。
    // OnTurnStart（自分のターン開始時）はプレイ時には解決せず墓地へ送り、墓地を永続トリガーの置き場として
    // 自分のターン開始時（ドロー前）に毎ターン発動し続ける（除去手段なし）。
    public enum EventCardTrigger
    {
        OnPlay,
        OnTurnStart,
    }
}
