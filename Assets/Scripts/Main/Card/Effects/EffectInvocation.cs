using System;

namespace Main.Card.Effects
{
    // 効果ハンドラ（EffectHandler.ApplyAsync）へ渡す実行時パラメータ。
    // キャラ効果（CharacterCardData）とイベント効果（EventCardData）の両方を、
    // 値1/値2・特徴・発動側・演出アンカーという共通の形に正規化して渡す。
    public readonly struct EffectInvocation
    {
        // 発動側が自分側か（演出・盤面の左右を決める）。
        public bool IsLocal { get; }

        // 演出のアンカーとなるカード（プレイしたカード／効果発動キャラ／墓地からせり出した一時カード）。
        public CardView SourceCard { get; }

        // 効果の1つ目の数値（CharacterCardData.EffectValue / EventCardData.EventValue）。
        public int Value1 { get; }

        // 効果の2つ目の数値（CharacterCardData.EffectValue2 / EventCardData.EventValue2）。
        public int Value2 { get; }

        // 発動カードの特徴（CardData.Keyword）。キーワード系効果で使う。
        public string Keyword { get; }

        public EffectInvocation(bool isLocal, CardView sourceCard, int value1, int value2, string keyword)
        {
            IsLocal = isLocal;
            SourceCard = sourceCard;
            Value1 = value1;
            Value2 = value2;
            Keyword = keyword;
        }
    }

    // エディタの効果テキスト自動生成（EffectHandler.BuildBody）へ渡す情報。
    // ランタイムには依存しない純データ。ResolveCardName は SummonChar など ID からカード名を引く効果用。
    public readonly struct EffectTextContext
    {
        public int Value1 { get; }
        public int Value2 { get; }
        public CardAttribute Attribute { get; }
        public string Keyword { get; }

        // ID（"C1001" など）からカード名を引くデリゲート。見つからなければ ID をそのまま返す想定。
        public Func<string, string> ResolveCardName { get; }

        public EffectTextContext(int value1, int value2, CardAttribute attribute, string keyword, Func<string, string> resolveCardName)
        {
            Value1 = value1;
            Value2 = value2;
            Attribute = attribute;
            Keyword = keyword;
            ResolveCardName = resolveCardName;
        }
    }

    // エディタで値1/値2 の入力欄ラベルとヒントを切り替えるためのメタデータ。
    public readonly struct EffectValueInfo
    {
        public bool Value1Used { get; }
        public bool Value2Used { get; }
        public string Value1Label { get; }
        public string Value2Label { get; }
        public string Help { get; }

        public EffectValueInfo(bool value1Used, string value1Label, bool value2Used, string value2Label, string help)
        {
            Value1Used = value1Used;
            Value1Label = value1Label;
            Value2Used = value2Used;
            Value2Label = value2Label;
            Help = help;
        }
    }

    // 属性の短縮名（赤/青/…）。エディタの属性ボタンと CostBoost の効果テキストで共用する。
    public static class CardAttributeNames
    {
        public static string Short(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Red:
                    return "赤";
                case CardAttribute.Blue:
                    return "青";
                case CardAttribute.Green:
                    return "緑";
                case CardAttribute.Yellow:
                    return "黄";
                case CardAttribute.Black:
                    return "黒";
                case CardAttribute.Purple:
                    return "紫";
                case CardAttribute.White:
                    return "白";
                default:
                    return "?";
            }
        }
    }
}
