using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 相手フィールドのキャラ全員に EventValue ダメージを与える。
    [Preserve]
    public sealed class DamageAllEnemiesHandler : EffectHandler
    {
        public override EventType Type => EventType.DamageAllEnemies;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（ダメージ量）", false, "値2（未使用）", "値1=敵フィールド全員へ与えるダメージ。");

        public override string BuildBody(EffectTextContext ctx) => $"相手キャラ全体に{ctx.Value1}ダメージを与える";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyDamageAllEnemiesAsync(inv.Value1, inv.IsLocal, ct);
        }
    }

    // 相手キャラを値1体（0=1体）選び、それぞれに値2ダメージを与える。
    [Preserve]
    public sealed class DamageEnemyHandler : EffectHandler
    {
        public override EventType Type => EventType.DamageEnemy;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（対象数）", true, "値2（ダメージ量）", "値1=選ぶ対象数 / 値2=各対象へのダメージ。");

        public override string BuildBody(EffectTextContext ctx) => $"相手キャラ{ctx.Value1}体に{ctx.Value2}ダメージを与える";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 値1=対象数、値2=ダメージ
            return p.ApplyDamageEnemyAsync(inv.Value2, inv.Value1, inv.IsLocal, ct);
        }
    }

    // 発動側が敵フィールドから値1体（0=敵全員）選び、それぞれの攻撃力を値2、永続的に下げる（0未満にはならない）。
    [Preserve]
    public sealed class DebuffAttackHandler : EffectHandler
    {
        public override EventType Type => EventType.DebuffAttack;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（対象数）", true, "値2（攻撃力の減少量）",
            "値1=敵フィールドから選ぶ相手の体数（0=敵全員・対象数が敵の数以上なら全員）/ 値2=各対象の攻撃力を永続的に下げる量（0未満にはならない）。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"{EnemiesTargetPrefix(ctx.Value1)}の攻撃力を{ctx.Value2}下げる";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            // 値1=対象数、値2=減少量
            return p.ApplyDebuffSelectedEnemiesAsync(inv.Value2, inv.Value1, inv.IsLocal, ct);
        }
    }

    // 相手キャラを値1体（0=1体）選んで破壊する。対象数が敵の数以上なら全員。
    [Preserve]
    public sealed class BanishCharHandler : EffectHandler
    {
        public override EventType Type => EventType.BanishChar;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（対象数）", false, "値2（未使用）", "値1=破壊する敵キャラの体数（0=1体）。対象数が敵の数以上なら全員。");

        public override string BuildBody(EffectTextContext ctx) => $"相手のキャラを{(ctx.Value1 <= 0 ? 1 : ctx.Value1)}体選んで破壊する";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyBanishCharAsync(inv.Value1, inv.IsLocal, ct);
        }
    }
}
