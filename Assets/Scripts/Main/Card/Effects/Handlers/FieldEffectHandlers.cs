using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 自分のキャラ1体を手札に戻し、そのキャラと同じコストのキャラを手札からコストなしで配置する。イベント専用。
    [Preserve]
    public sealed class SwitchHandler : EffectHandler
    {
        public override EventType Type => EventType.Switch;
        public override bool ValidOnCharacter => false;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "自分のキャラ1体を手札に戻し、そのキャラと同じコストのキャラを手札からコストなしで配置する";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 演出（パーティクル＋SWITCH! ラベル）は手札に戻すキャラを確定したあとに、
            // 対象キャラの上で再生する。複数候補があるときに選択前へ演出が出ないよう
            // ApplySwitchEffectAsync 内で行う。
            return p.ApplySwitchEffectAsync(inv.IsLocal, ct);
        }
    }

    // 自分のキャラ1体を生贄にして、より高コストのキャラをコストなしで配置する。イベント専用。
    [Preserve]
    public sealed class EvolveHandler : EffectHandler
    {
        public override EventType Type => EventType.Evolve;
        public override bool ValidOnCharacter => false;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "自分のキャラ1体を生贄にして、より高コストのキャラをコストなしで配置する";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            FieldView field = p.FieldFor(inv.IsLocal);
            CardView evolveChar = field.Characters.Count > 0 ? field.Characters[0] : null;
            if (evolveChar != null)
            {
                await p.PlayFloatingLabelAsync("進化！", "evolve-label", evolveChar, ct);
            }
            await p.ApplyEvolveEffectAsync(inv.IsLocal, ct);
        }
    }

    // 墓地の上から値1枚を回収してデッキへ戻し、シャッフルする。イベント専用。
    [Preserve]
    public sealed class RecoverHandler : EffectHandler
    {
        public override EventType Type => EventType.Recover;
        public override bool ValidOnCharacter => false;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（回収枚数）", false, "値2（未使用）", "値1=墓地の上から回収してデッキへ戻す枚数。");

        public override string BuildBody(EffectTextContext ctx) => $"墓地から上のカード{ctx.Value1}枚をデッキに戻してシャッフルする";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlayRecoverEffectAsync(inv.SourceCard, inv.Value1, ct);
            await p.ApplyRecoverEffectAsync(inv.Value1, inv.IsLocal, ct);
        }
    }

    // 自フィールドの指定属性キャラ数 N だけ、自分の墓地の上から N 枚をデッキへ戻してシャッフルする（Recover の動的枚数版）。
    [Preserve]
    public sealed class RecoverByColorCharsHandler : EffectHandler
    {
        public override EventType Type => EventType.RecoverByColorChars;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（属性番号 / 0=全キャラ）", false, "値2（未使用）",
            "値1=数える属性の番号（白1/青2/緑3/黄4/赤5/黒6/紫7。0=属性を問わず自分の場の全キャラ）。自分の場のそのキャラ数だけ自分の墓地の上のカードをデッキへ戻す。範囲外なら空振り。");

        public override string BuildBody(EffectTextContext ctx)
        {
            if (ctx.Value1 == 0)
            {
                return "自分の場のキャラの数だけ墓地の上のカードをデッキに戻してシャッフルする";
            }
            CardAttribute? attribute = CardAttributeNames.FromNumber(ctx.Value1);
            string color = attribute == null ? "指定属性" : CardAttributeNames.Short(attribute.Value);
            return $"自分の場の{color}のキャラの数だけ墓地の上のカードをデッキに戻してシャッフルする";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyRecoverByCharCountAsync(inv.IsLocal, inv.Value1, inv.SourceCard, ct);
        }
    }
}
