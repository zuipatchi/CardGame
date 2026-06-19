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

    // 発動側が自フィールドから値1体（0=場の味方全員）選び、それぞれの攻撃力を値2、永続的に上げる。
    [Preserve]
    public sealed class AtkBoostHandler : EffectHandler
    {
        public override EventType Type => EventType.AtkBoost;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（対象数）", true, "値2（攻撃力の上昇量）",
            "値1=自フィールドから選ぶ味方の体数（0=場の味方全員・対象数が味方の数以上なら全員）/ 値2=各対象の攻撃力を永続的に上げる量。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"{AlliesTargetPrefix(ctx.Value1)}の攻撃力を{ctx.Value2}上げる";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 値1=対象数、値2=上昇量
            return p.ApplyBuffSelectedAlliesAsync(inv.Value2, inv.Value1, isAttack: true, inv.IsLocal, ct);
        }
    }

    // 発動側が自フィールドから値1体（0=場の味方全員）選び、それぞれの HP（現在・最大）を値2、永続的に上げる。
    [Preserve]
    public sealed class HpBoostHandler : EffectHandler
    {
        public override EventType Type => EventType.HpBoost;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（対象数）", true, "値2（HPの上昇量）",
            "値1=自フィールドから選ぶ味方の体数（0=場の味方全員・対象数が味方の数以上なら全員）/ 値2=各対象のHP（現在・最大）を永続的に上げる量。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"{AlliesTargetPrefix(ctx.Value1)}のHPを{ctx.Value2}上げる";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 値1=対象数、値2=上昇量
            return p.ApplyBuffSelectedAlliesAsync(inv.Value2, inv.Value1, isAttack: false, inv.IsLocal, ct);
        }
    }

    // 発動側が自フィールドから値1体（0=場の味方全員）選び、値2で指定したキーワード能力を永続付与する。
    [Preserve]
    public sealed class GrantKeywordHandler : EffectHandler
    {
        public override EventType Type => EventType.GrantKeyword;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（対象数）", true, "値2（付与する能力 1=守護/2=速攻/3=飛行/4=防人/5=強襲/6=デッキ攻撃×）",
            "値1=自フィールドから選ぶ味方の体数（0=場の味方全員・対象数が味方の数以上なら全員）/ 値2=付与するキーワード能力（1=守護・2=速攻・3=飛行・4=防人・5=強襲・6=デッキ攻撃×。範囲外は空振り）。");

        public override string BuildBody(EffectTextContext ctx)
        {
            string keyword = GrantableKeywordExtensions.FromValue(ctx.Value2).DisplayName();
            if (string.IsNullOrEmpty(keyword))
            {
                return "";
            }
            return $"{AlliesTargetPrefix(ctx.Value1)}に{keyword}を付与する";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 値1=対象数、値2=付与するキーワード
            return p.ApplyGrantKeywordAsync(inv.Value2, inv.Value1, inv.IsLocal, ct);
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
