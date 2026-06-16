using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 効果なし。勝利点付帯値だけ得るカードは None ＋ 付帯値で作る。解決時は何もしない。
    [Preserve]
    public sealed class NoneHandler : EffectHandler
    {
        public override EventType Type => EventType.None;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "効果なし。勝利点付帯値だけ得るカードはこれ＋付帯値で作る。");

        public override string BuildBody(EffectTextContext ctx) => string.Empty;

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct) => UniTask.CompletedTask;
    }

    // コストとして支払うときに値1個分として数える（属性一致時のみ）。
    // 効果解決ではなくコスト計算（CardData.CostPaymentValue）で扱うため、解決時は何もしない。
    [Preserve]
    public sealed class CostBoostHandler : EffectHandler
    {
        public override EventType Type => EventType.CostBoost;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（コスト換算値）", false, "値2（未使用）", "値1=コストとして支払うときに数える値（属性一致時のみ）。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"{CardAttributeNames.Short(ctx.Attribute)}コスト{ctx.Value1}個分として扱う";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct) => UniTask.CompletedTask;
    }
}
