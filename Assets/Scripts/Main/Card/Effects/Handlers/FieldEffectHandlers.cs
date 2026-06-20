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
                await p.PlayFloatingLabelAsync("EVOLVE", "evolve-label", evolveChar, ct);
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

        public override string BuildBody(EffectTextContext ctx) => $"墓地から上の{ctx.Value1}枚をデッキに戻す";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlayRecoverEffectAsync(inv.SourceCard, inv.Value1, ct);
            await p.ApplyRecoverEffectAsync(inv.Value1, inv.IsLocal, ct);
        }
    }
}
