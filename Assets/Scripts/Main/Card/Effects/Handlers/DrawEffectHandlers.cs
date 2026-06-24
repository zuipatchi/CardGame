using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // カードを EventValue（値1）枚引く。値2=1 でオーバーリミット指定（デッキが0枚でも敗北せず「オーバーリミット！」告知）。
    [Preserve]
    public sealed class DrawHandler : EffectHandler
    {
        // 値2がこの値のとき、空デッキから引いても敗北しない（オーバーリミット安全ドロー）。
        private const int OverLimitValue2 = 1;

        public override EventType Type => EventType.Draw;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ドロー枚数）", true, "値2（1=オーバーリミット）",
            "値1=デッキ上から手札に加える枚数。値2=1でオーバーリミット指定（デッキが0枚でも敗北せず「オーバーリミット！」告知だけ行う）。");

        public override string BuildBody(EffectTextContext ctx)
        {
            string body = $"カードを{ctx.Value1}枚引く";
            if (ctx.Value2 == OverLimitValue2)
            {
                body += "（デッキが尽きても敗北しない）";
            }
            return body;
        }

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlayDrawEffectAsync(inv.SourceCard, inv.Value1, ct);
            await p.ApplyDrawEffectAsync(inv.Value1, inv.IsLocal, ct, overLimitSafe: inv.Value2 == OverLimitValue2);
        }
    }

    // コインを振り、表が出るたびにカードを1枚引く（裏が出たら終了）。値1・値2は不使用。
    [Preserve]
    public sealed class CoinDrawHandler : EffectHandler
    {
        public override EventType Type => EventType.CoinDraw;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）",
            "コインを振り、表が出るたびにカードを1枚引く（裏が出たら終了）。値1・値2は使わない。");

        public override string BuildBody(EffectTextContext ctx)
            => "コインを振り、表が出るたびにカードを1枚引く（裏が出たら終了）";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.ApplyCoinDrawEffectAsync(inv.SourceCard, inv.IsLocal, ct);
        }
    }

    // サイコロ（6面）を振り、出た目の数だけカードを引く。値1・値2は不使用。
    [Preserve]
    public sealed class DiceDrawHandler : EffectHandler
    {
        public override EventType Type => EventType.DiceDraw;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）",
            "サイコロ（6面）を振り、出た目の数だけカードを引く。値1・値2は使わない。");

        public override string BuildBody(EffectTextContext ctx)
            => "サイコロを振り、出た目の数だけカードを引く";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.ApplyDiceDrawEffectAsync(inv.SourceCard, inv.IsLocal, ct);
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
