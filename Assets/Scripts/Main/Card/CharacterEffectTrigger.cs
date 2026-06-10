namespace Main.Card
{
    // キャラカードの効果発動タイミング。
    // OnEnter（登場時）と OnAttack（攻撃時）を実装済み。OnDestroy は将来の拡張用に定義のみ。
    public enum CharacterEffectTrigger
    {
        None,
        OnEnter,
        OnAttack,
        OnDestroy,
    }
}
