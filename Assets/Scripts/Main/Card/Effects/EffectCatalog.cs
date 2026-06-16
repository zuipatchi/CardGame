using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Main.Card.Effects
{
    // EventType → EffectHandler の登録簿。Main アセンブリ内の EffectHandler 派生型を
    // リフレクションで走査して自動登録するため、効果を追加してもこのファイルの編集は不要。
    //
    // 起動時（初回アクセス時）に「全 EventType にちょうど1つのハンドラがあるか」を検証し、
    // 欠落・重複があれば即座に例外を投げる（手動確認のプレイ開始時に必ず気づける）。
    public static class EffectCatalog
    {
        private static readonly Dictionary<EventType, EffectHandler> Handlers = BuildHandlers();

        private static Dictionary<EventType, EffectHandler> BuildHandlers()
        {
            Dictionary<EventType, EffectHandler> map = new Dictionary<EventType, EffectHandler>();

            IEnumerable<Type> handlerTypes = typeof(EffectHandler).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(EffectHandler).IsAssignableFrom(t));

            foreach (Type type in handlerTypes)
            {
                EffectHandler handler = (EffectHandler)Activator.CreateInstance(type);
                if (map.ContainsKey(handler.Type))
                {
                    throw new InvalidOperationException(
                        $"EffectCatalog: {handler.Type} のハンドラが重複しています（{map[handler.Type].GetType().Name} と {type.Name}）。");
                }
                map[handler.Type] = handler;
            }

            List<EventType> missing = new List<EventType>();
            foreach (EventType type in (EventType[])Enum.GetValues(typeof(EventType)))
            {
                if (!map.ContainsKey(type))
                {
                    missing.Add(type);
                }
            }
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"EffectCatalog: ハンドラ未登録の EventType があります → {string.Join(", ", missing)}。" +
                    "対応する EffectHandler 派生クラスを Effects/Handlers/ に追加してください。");
            }

            return map;
        }

        // 指定 EventType のハンドラを返す。未登録なら null（検証済みのため通常は起こらない）。
        public static EffectHandler Get(EventType type)
        {
            return Handlers.TryGetValue(type, out EffectHandler handler) ? handler : null;
        }

        // 全ハンドラ（主にデバッグ・検証用）。
        public static IEnumerable<EffectHandler> All => Handlers.Values;
    }
}
