using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // カードを EventValue 枚引く。
    [Preserve]
    public sealed class DrawHandler : EffectHandler
    {
        public override EventType Type => EventType.Draw;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ドロー枚数）", false, "値2（未使用）", "値1=デッキ上から手札に加える枚数。");

        public override string BuildBody(EffectTextContext ctx) => $"カードを{ctx.Value1}枚引く";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlayDrawEffectAsync(inv.SourceCard, inv.Value1, ct);
            await p.ApplyDrawEffectAsync(inv.Value1, inv.IsLocal, ct);
        }
    }

    // 即時に EventValue 枚引き、次のドローフェーズを1回スキップする。
    [Preserve]
    public sealed class DrawSkipNextHandler : EffectHandler
    {
        public override EventType Type => EventType.DrawSkipNext;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ドロー枚数）", false, "値2（未使用）", "値1=即時ドロー枚数。次のドローフェーズを1回スキップする。");

        public override string BuildBody(EffectTextContext ctx) => $"カードを{ctx.Value1}枚引く。次のドローを1回スキップする";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlayDrawEffectAsync(inv.SourceCard, inv.Value1, ct);
            await p.ApplyDrawEffectAsync(inv.Value1, inv.IsLocal, ct);
            p.SetSkipNextDraw(inv.IsLocal);
        }
    }

    // 次の自分のターン開始時に EventValue 枚多く引く（予約）。
    [Preserve]
    public sealed class DrawNextTurnStartHandler : EffectHandler
    {
        public override EventType Type => EventType.DrawNextTurnStart;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ドロー枚数）", false, "値2（未使用）", "値1=次ターン開始時に追加でドローする枚数。");

        public override string BuildBody(EffectTextContext ctx) => $"次の自分のターン開始時に{ctx.Value1}枚多く引く";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlayFloatingLabelAsync($"次ターン DRAW {inv.Value1}", "draw-label", inv.SourceCard, ct);
            p.AddPendingNextDraw(inv.Value1, inv.IsLocal);
        }
    }
}
