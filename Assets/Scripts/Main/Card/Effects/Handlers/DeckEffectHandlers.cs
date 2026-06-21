using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 相手のデッキの上から値1枚を墓地へ送る（デッキへのダメージ＝ミル）。
    // ミルされたカードが「ダメージトリガー」なら持ち主（相手）がコストなしで使用する。
    [Preserve]
    public sealed class DamageEnemyDeckHandler : EffectHandler
    {
        public override EventType Type => EventType.DamageEnemyDeck;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ミル枚数）", false, "値2（未使用）", "値1=相手デッキの上から墓地へ送る枚数。");

        public override string BuildBody(EffectTextContext ctx) => $"相手のデッキに{ctx.Value1}ダメージを与える";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 発動側(inv.IsLocal)から見た相手のデッキ＝ !inv.IsLocal の持ち主
            return p.MillDeckAsync(deckOwnerIsLocal: !inv.IsLocal, inv.Value1, ct);
        }
    }

    // デッキから発動カード自身の特徴を持つカード（キャラ・イベント問わず）を値1枚選んで手札に加える。
    [Preserve]
    public sealed class AddToHandFromDeckByKeywordHandler : EffectHandler
    {
        public override EventType Type => EventType.AddToHandFromDeckByKeyword;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（加える枚数）", false, "値2（未使用）",
            "値1=デッキから自身の特徴を持つカードを手札に加える枚数。候補が値1より多いときはプレイヤーが選ぶ。発動カードに特徴の設定が必要。");

        public override string BuildBody(EffectTextContext ctx)
        {
            int count = ctx.Value1 <= 0 ? 1 : ctx.Value1;
            return string.IsNullOrEmpty(ctx.Keyword)
                ? $"デッキからカードを{count}枚選んで手札に加える"
                : $"デッキから特徴「{ctx.Keyword}」を持つカードを{count}枚選んで手札に加える";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyAddToHandFromDeckByKeywordAsync(inv.Keyword, inv.Value1, inv.IsLocal, ct);
        }
    }

    // お互いのデッキの上から値1枚ずつを墓地へ送る（デッキへのダメージ＝ミル）。自分 → 相手の順にミルする。
    // ミルされたカードが「ダメージトリガー」なら持ち主がコストなしで使用する。自分のデッキも削れるため両刃。
    [Preserve]
    public sealed class DamageBothDecksHandler : EffectHandler
    {
        public override EventType Type => EventType.DamageBothDecks;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ミル枚数）", false, "値2（未使用）", "値1=自分と相手それぞれのデッキの上から墓地へ送る枚数。");

        public override string BuildBody(EffectTextContext ctx) => $"お互いのデッキに{ctx.Value1}ダメージを与える";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 自分 → 相手の順にミルする。自分のデッキを先に削り、空デッキからミルしようとした瞬間に自分が敗北
            // （MillDeckAsync 内のミル直前 CheckDeckOutWin で _isGameOver が立ち、続く相手のミルは早期 return される）
            await p.MillDeckAsync(deckOwnerIsLocal: inv.IsLocal, inv.Value1, ct);
            await p.MillDeckAsync(deckOwnerIsLocal: !inv.IsLocal, inv.Value1, ct);
        }
    }
}
