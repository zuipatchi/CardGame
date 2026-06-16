using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 自分以外の同じ特徴を持つ味方キャラの攻撃力を値1上げる（発動時に一度だけ永続加算）。
    [Preserve]
    public sealed class BuffAttackByKeywordHandler : EffectHandler
    {
        public override EventType Type => EventType.BuffAttackByKeyword;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（攻撃力の上昇量）", false, "値2（未使用）",
            "値1=同じ特徴を持つ味方キャラ（自分以外）の攻撃力を上げる量。発動キャラに特徴の設定が必要。");

        public override string BuildBody(EffectTextContext ctx) => KeywordBuffBody(ctx.Keyword, ctx.Value1, "攻撃力");

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyBuffByKeywordAsync(inv.SourceCard, inv.Value1, isAttack: true, inv.IsLocal, ct);
        }
    }

    // 自分以外の同じ特徴を持つ味方キャラの HP（現在・最大）を値1上げる。
    [Preserve]
    public sealed class BuffHpByKeywordHandler : EffectHandler
    {
        public override EventType Type => EventType.BuffHpByKeyword;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（HPの上昇量）", false, "値2（未使用）",
            "値1=同じ特徴を持つ味方キャラ（自分以外）のHP（現在・最大）を上げる量。発動キャラに特徴の設定が必要。");

        public override string BuildBody(EffectTextContext ctx) => KeywordBuffBody(ctx.Keyword, ctx.Value1, "HP");

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyBuffByKeywordAsync(inv.SourceCard, inv.Value1, isAttack: false, inv.IsLocal, ct);
        }
    }

    // 自フィールドのキャラ全員の HP を値1回復する（0=最大HPまで全回復）。
    [Preserve]
    public sealed class HealAllAlliesHandler : EffectHandler
    {
        public override EventType Type => EventType.HealAllAllies;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（回復量）", false, "値2（未使用）", "値1=自フィールド全員の回復量（0=最大HPまで全回復）。");

        public override string BuildBody(EffectTextContext ctx) =>
            ctx.Value1 <= 0 ? "自分のキャラ全体のHPを全回復する" : $"自分のキャラ全体のHPを{ctx.Value1}回復する";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyHealAllAlliesAsync(inv.Value1, inv.IsLocal, ct);
        }
    }
}
