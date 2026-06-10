namespace Main.Card
{
    public enum EventType
    {
        None,
        AtkBoost,
        DefBoost,
        Draw,
        Negate,
        BanishChar,
        Recover,
        Switch,
        Evolve,
        // 手札からコストとして支払うときに、コスト値（EventValue / EffectValue）分のコストとして数える。
        // キャラは CharacterEffectTrigger.OnUsedAsCost と併用、イベントは EventType 単体で判定。通常プレイ時は無効果。
        CostBoost,
    }
}
