using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // ハンデス：相手プレイヤーは手札を値1枚捨てる（捨てるカードは手札の持ち主が選ぶ）。
    // 手札枚数が値1未満なら手札全部・手札が0枚なら空振り。
    [Preserve]
    public sealed class DiscardHandler : EffectHandler
    {
        public override EventType Type => EventType.Discard;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（捨てさせる枚数）", false, "値2（未使用）",
            "値1=相手に手札から捨てさせる枚数。手札枚数より多ければ手札全部。捨てるカードは持ち主が選ぶ。");

        public override string BuildBody(EffectTextContext ctx)
        {
            int count = ctx.Value1 <= 0 ? 1 : ctx.Value1;
            return $"相手の手札を{count}枚捨てさせる";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyDiscardAsync(inv.Value1, inv.IsLocal, ct);
        }
    }

    // 発動側自身の墓地からカード（キャラ・イベント問わず）を値1枚（0=1枚）選んで手札に加える。
    // 場には出さないため OnEnter は発動しない。墓地が空なら空振り。手札上限なら超過分は墓地へバーン。
    [Preserve]
    public sealed class AddToHandFromGraveHandler : EffectHandler
    {
        public override EventType Type => EventType.AddToHandFromGrave;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（加える枚数）", false, "値2（未使用）",
            "自分の墓地からカードを値1枚選んで手札に加える（0=1枚）。墓地から消費する。手札上限なら超過分は墓地へ。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"自分の墓地からカードを{(ctx.Value1 <= 0 ? 1 : ctx.Value1)}枚選んで手札に加える";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyAddToHandFromGraveAsync(inv.Value1, inv.IsLocal, ct);
        }
    }
}
