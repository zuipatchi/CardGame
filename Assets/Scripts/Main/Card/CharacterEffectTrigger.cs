namespace Main.Card
{
    // キャラカードの効果発動タイミング。
    // 今回は OnEnter（登場時）のみ処理を実装。OnAttack / OnDestroy は将来の拡張用に定義のみ。
    public enum CharacterEffectTrigger
    {
        None,
        OnEnter,
        OnAttack,
        OnDestroy,
    }
}
