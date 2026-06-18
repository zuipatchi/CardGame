using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 発動側の墓地にある緑属性カードの枚数だけ勝利点を得る。
    [Preserve]
    public sealed class GainVPPerGreenGraveHandler : EffectHandler
    {
        public override EventType Type => EventType.GainVPPerGreenGrave;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "墓地にある緑カードの数だけ勝利点を得る";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            int vp = p.CountGreenInGraveyard(inv.IsLocal);
            await p.PlayFloatingMedalAsync(inv.SourceCard, vp, ct);
            await p.AddVictoryPoints(vp, toLocal: inv.IsLocal, ct);
        }
    }

    // 次に使うカード1枚のコストを0にする（使うまで持続）。
    [Preserve]
    public sealed class NextCardCostFreeHandler : EffectHandler
    {
        public override EventType Type => EventType.NextCardCostFree;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "次に使うカード1枚のコストを0にする";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyNextCardCostFreeAsync(inv.IsLocal, inv.SourceCard, ct);
        }
    }

    // 追加でもう1度自分のターンを行う（アクティブプレイヤーが発動したときのみ・1ターン1回）。
    [Preserve]
    public sealed class ExtraTurnHandler : EffectHandler
    {
        public override EventType Type => EventType.ExtraTurn;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "追加でもう1度自分のターンを行う";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyExtraTurnAsync(inv.IsLocal, inv.SourceCard, ct);
        }
    }
}
