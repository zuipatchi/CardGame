using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 相手キャラを値1体（0=1体）選び、所有者の手札へ戻す。対象数が敵の数以上なら全員。
    [Preserve]
    public sealed class BounceHandler : EffectHandler
    {
        public override EventType Type => EventType.Bounce;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（戻す体数）", false, "値2（未使用）", "値1=相手の手札へ戻す敵キャラの体数。");

        public override string BuildBody(EffectTextContext ctx) => $"相手キャラ{ctx.Value1}体を持ち主の手札に戻す";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyBounceAsync(inv.Value1, inv.IsLocal, ct);
        }
    }

    // 相手フィールドのキャラ全員を所有者の手札へ戻す。
    [Preserve]
    public sealed class BounceAllHandler : EffectHandler
    {
        public override EventType Type => EventType.BounceAll;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "相手キャラ全体を持ち主の手札に戻す";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyBounceAllAsync(inv.IsLocal, ct);
        }
    }
}
