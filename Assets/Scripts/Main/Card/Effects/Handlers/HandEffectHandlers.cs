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
}
