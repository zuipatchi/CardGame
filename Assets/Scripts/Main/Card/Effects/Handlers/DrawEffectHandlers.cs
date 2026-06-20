using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // カードを EventValue 枚引く。値2でドロー方式を切り替える（0=通常 / 1=コインドロー / 2=サイコロドロー）。
    [Preserve]
    public sealed class DrawHandler : EffectHandler
    {
        public override EventType Type => EventType.Draw;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ドロー枚数）", true, "値2（0=通常/1=コイン/2=サイコロ）",
            "値1=デッキ上から手札に加える枚数。値2=1でコインドロー（表の間1枚ずつ引き裏で終了）、値2=2でサイコロドロー（6面を振り出た目だけ引く）。コイン／サイコロ時は値1は無視。");

        public override string BuildBody(EffectTextContext ctx) => ctx.Value2 switch
        {
            1 => "コインを振り、表が出るたびにカードを1枚引く（裏が出たら終了）",
            2 => "サイコロを振り、出た目の数だけカードを引く",
            _ => $"カードを{ctx.Value1}枚引く",
        };

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            if (inv.Value2 == 1)
            {
                await p.ApplyCoinDrawEffectAsync(inv.SourceCard, inv.IsLocal, ct);
                return;
            }
            if (inv.Value2 == 2)
            {
                await p.ApplyDiceDrawEffectAsync(inv.SourceCard, inv.IsLocal, ct);
                return;
            }
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
