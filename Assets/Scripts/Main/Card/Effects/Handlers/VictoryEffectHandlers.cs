using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    // 発動側の墓地にある緑属性カードの枚数だけ勝利点を得る。
    [Preserve]
    public sealed class GainVPPerGreenGraveHandler : EffectHandler
    {
        public override EventType Type => EventType.GainVPPerGreenGrave;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "墓地にある緑カードの数だけ勝利点を得る";

        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            int vp = p.CountGreenInGraveyard(inv.IsLocal);
            await p.PlayFloatingMedalAsync(inv.SourceCard, vp, ct);
            await p.AddVictoryPoints(vp, toLocal: inv.IsLocal, ct);
        }
    }

    // 次に使うカード1枚のコストを0にする（使うまで持続）。
    [Preserve]
    public sealed class NextCardCostFreeHandler : EffectHandler
    {
        public override EventType Type => EventType.NextCardCostFree;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "次に使うカード1枚のコストを0にする";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyNextCardCostFreeAsync(inv.IsLocal, inv.SourceCard, ct);
        }
    }

    // 太郎勝利（特殊勝利）：値1（テキスト）に書いた完全ID（カンマ区切り）のカードが
    // 効果発動時に発動側の手札にすべてそろっていれば勝利する。
    [Preserve]
    public sealed class HandCollectionWinHandler : EffectHandler
    {
        public override EventType Type => EventType.HandCollectionWin;

        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（勝利条件カードID・カンマ区切り）", false, "値2（未使用）",
            "値1=手札にそろえると勝利する完全ID（例 C1001,E2003,C1005）。重複指定は枚数分の所持が必要。値2は未使用。",
            value1IsText: true);

        public override string BuildBody(EffectTextContext ctx)
        {
            string[] ids = ParseIds(ctx.Param);
            if (ids.Length == 0)
            {
                return "手札に指定カードがそろっていれば勝利する";
            }
            List<string> names = new List<string>(ids.Length);
            foreach (string id in ids)
            {
                names.Add($"「{ctx.ResolveCardName(id)}」");
            }
            return $"手札に{string.Join("", names)}がそろっていれば勝利する";
        }

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyHandCollectionWinAsync(ParseIds(inv.Param), inv.IsLocal, inv.SourceCard, ct);
        }

        // カンマ区切りの ID 文字列を、空白除去・空要素除去した配列にする。
        private static string[] ParseIds(string param)
        {
            if (string.IsNullOrEmpty(param))
            {
                return System.Array.Empty<string>();
            }
            string[] raw = param.Split(',');
            List<string> result = new List<string>(raw.Length);
            foreach (string token in raw)
            {
                string trimmed = token.Trim();
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }
            return result.ToArray();
        }
    }

    // 追加でもう1度自分のターンを行う（アクティブプレイヤーが発動したときのみ・1ターン1回）。
    [Preserve]
    public sealed class ExtraTurnHandler : EffectHandler
    {
        public override EventType Type => EventType.ExtraTurn;

        public override EffectValueInfo Values => new EffectValueInfo(
            false, "値1（未使用）", false, "値2（未使用）", "この効果は値を使用しません（0 のままで可）。");

        public override string BuildBody(EffectTextContext ctx) => "追加でもう1度自分のターンを行う";

        public override UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            return p.ApplyExtraTurnAsync(inv.IsLocal, inv.SourceCard, ct);
        }
    }
}
