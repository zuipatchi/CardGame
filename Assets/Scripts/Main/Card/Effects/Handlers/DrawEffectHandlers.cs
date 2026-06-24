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
                body += "（デッキが0枚でも敗北せずオーバーリミットになる）";
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

    // 即時に EventValue（値1）枚引き、ドローフェーズを1回スキップする。
    // 値2=1 で「相手の」次のドローフェーズをスキップ（0=自分の次のドローフェーズ）。値1=0 なら即時ドローは行わない。
    [Preserve]
    public sealed class DrawSkipNextHandler : EffectHandler
    {
        // 値2がこの値のとき、自分でなく相手の次のドローフェーズをスキップする。
        private const int SkipOpponentValue2 = 1;

        public override EventType Type => EventType.DrawSkipNext;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ドロー枚数・0で引かない）", true, "値2（1=相手の次ドローをスキップ）",
            "値1=即時ドロー枚数（0なら引かない）。値2=0で自分・1で相手の次のドローフェーズを1回スキップする。");

        public override string BuildBody(EffectTextContext ctx)
        {
            string drawPart = ctx.Value1 > 0 ? $"カードを{ctx.Value1}枚引く。" : string.Empty;
            string skipPart = ctx.Value2 == SkipOpponentValue2
                ? "相手の次のドローフェーズを1回スキップする"
                : "次の自分のドローフェーズを1回スキップする";
            return drawPart + skipPart;
        }

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            if (inv.Value1 > 0)
            {
                await p.PlayDrawEffectAsync(inv.SourceCard, inv.Value1, ct);
                await p.ApplyDrawEffectAsync(inv.Value1, inv.IsLocal, ct);
            }

            // 値2=1 のときは相手（!IsLocal）、それ以外は発動側（IsLocal）の次ドローをスキップ予約する。
            bool skipOpponent = inv.Value2 == SkipOpponentValue2;
            string label = skipOpponent ? "相手の次ドロースキップ" : "次ドロースキップ";
            await p.PlayFloatingLabelAsync(label, "skip-draw-label", inv.SourceCard, ct);
            p.SetSkipNextDraw(skipOpponent ? !inv.IsLocal : inv.IsLocal);
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
