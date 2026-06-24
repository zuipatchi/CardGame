using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 値1 が示すキャラ（"C###"）を値2体（0=1体）新規生成して自フィールドに出す。
    [Preserve]
    public sealed class SummonCharHandler : EffectHandler
    {
        public override EventType Type => EventType.SummonChar;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（召喚キャラID数字）", true, "値2（体数）",
            "値1=召喚するキャラIDの数字部分（例 1001→C1001）/ 値2=体数（0=1体）。");

        public override string BuildBody(EffectTextContext ctx)
        {
            string id = $"C{ctx.Value1}";
            string name = ctx.ResolveCardName(id);
            string stats = ctx.ResolveCardStats?.Invoke(id);
            int count = ctx.Value2 <= 0 ? 1 : ctx.Value2;
            // 召喚先のカードを参照して「ATK/HPの「カード名」」と表示する。数値が引けないときは名前のみ。
            return string.IsNullOrEmpty(stats)
                ? $"「{name}」を{count}体召喚する"
                : $"{stats}の「{name}」を{count}体召喚する";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplySummonCharAsync(inv.Value1, inv.Value2, inv.IsLocal, ct);
        }
    }

    // デッキから発動カード自身の特徴を持つキャラを1枚選んで自フィールドに出す。
    [Preserve]
    public sealed class SummonFromDeckByKeywordHandler : EffectHandler
    {
        public override EventType Type => EventType.SummonFromDeckByKeyword;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）",
            "デッキから自身の特徴を持つキャラを1枚選んで場に出す。値は未使用。発動カードに特徴の設定が必要。");

        public override string BuildBody(EffectTextContext ctx) =>
            string.IsNullOrEmpty(ctx.Keyword)
                ? "デッキからキャラを1枚選んで場に出す"
                : $"デッキから『{ctx.Keyword}』を持つキャラを1枚選んで場に出す";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplySummonFromDeckByKeywordAsync(inv.Keyword, inv.IsLocal, ct);
        }
    }

    // 発動側自身の墓地からキャラカードを値1体（0=1体）選んで自フィールドに出す。
    [Preserve]
    public sealed class SummonFromGraveHandler : EffectHandler
    {
        public override EventType Type => EventType.SummonFromGrave;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（出す体数）", false, "値2（未使用）",
            "自分の墓地からキャラを値1体選んで場に出す（0=1体）。墓地から消費し、配置時に OnEnter も発動する。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"自分の墓地からキャラを{(ctx.Value1 <= 0 ? 1 : ctx.Value1)}体選んで場に出す";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplySummonFromGraveAsync(inv.Value1, inv.IsLocal, ct);
        }
    }

    // 自分のキャラを1体選び、そのコピー（バフ・現在HP込み）を値1体（0=1体）自フィールドに出す。
    [Preserve]
    public sealed class CopyFieldCharHandler : EffectHandler
    {
        public override EventType Type => EventType.CopyFieldChar;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（コピー体数）", false, "値2（未使用）",
            "値1=選んだ自分のキャラのコピーを出す体数（0=1体）。バフ・現在HP込みでコピー。");

        public override string BuildBody(EffectTextContext ctx) =>
            $"自分のキャラを1体選び、そのコピーを{(ctx.Value1 <= 0 ? 1 : ctx.Value1)}体出す";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyCopyFieldCharAsync(inv.Value1, inv.IsLocal, ct);
        }
    }
}
