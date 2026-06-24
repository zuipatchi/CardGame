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

    // 自フィールドの指定属性キャラの数 N だけ相手デッキにダメージ（ミル）を与え、同じ N 枚を自分の墓地の上からデッキへ戻す。
    [Preserve]
    public sealed class DamageDeckRecoverByColorCharsHandler : EffectHandler
    {
        public override EventType Type => EventType.DamageDeckRecoverByColorChars;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（属性番号 / 0=全キャラ）", false, "値2（未使用）",
            "値1=数える属性の番号（白1/青2/緑3/黄4/赤5/黒6/紫7。0=属性を問わず自分の場の全キャラ）。自分の場のそのキャラ数だけ相手デッキを削り、同じ数だけ墓地から回収する。範囲外なら空振り。");

        public override string BuildBody(EffectTextContext ctx)
        {
            if (ctx.Value1 == 0)
            {
                return "自分の場のキャラの数だけ相手のデッキにダメージを与え、同じ数だけ自分の墓地の上のカードをデッキに戻す";
            }
            CardAttribute? attribute = CardAttributeNames.FromNumber(ctx.Value1);
            string color = attribute == null ? "指定属性" : CardAttributeNames.Short(attribute.Value);
            return $"自分の場の{color}のキャラの数だけ相手のデッキにダメージを与え、同じ数だけ自分の墓地の上のカードをデッキに戻す";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyColorCharDeckDamageAndRecoverAsync(inv.IsLocal, inv.Value1, inv.SourceCard, ct);
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
