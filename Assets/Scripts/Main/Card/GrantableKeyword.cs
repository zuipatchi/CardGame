namespace Main.Card
{
    // GrantKeyword 効果で味方キャラに付与できるキーワード能力。
    // 効果の値2（EventValue2 / EffectValue2）の数値で指定する（FromValue で変換）。
    // 整数値は .asset が参照するため、既存値を変えないこと。
    public enum GrantableKeyword
    {
        None = 0,
        Guardian = 1,
        Haste = 2,
        Flying = 3,
        Sakimori = 4,
        Assault = 5,
        NoDeckAttack = 6,
        Archer = 7,
        Deadly = 8,
    }

    public static class GrantableKeywordExtensions
    {
        // 効果の値2（数値）を GrantableKeyword へ変換する。範囲外は None（空振り）。
        public static GrantableKeyword FromValue(int value)
        {
            switch (value)
            {
                case 1:
                    return GrantableKeyword.Guardian;
                case 2:
                    return GrantableKeyword.Haste;
                case 3:
                    return GrantableKeyword.Flying;
                case 4:
                    return GrantableKeyword.Sakimori;
                case 5:
                    return GrantableKeyword.Assault;
                case 6:
                    return GrantableKeyword.NoDeckAttack;
                case 7:
                    return GrantableKeyword.Archer;
                case 8:
                    return GrantableKeyword.Deadly;
                default:
                    return GrantableKeyword.None;
            }
        }

        // 効果テキスト・トースト用の日本語名（守護 / 速攻 / 飛行 / 防人 / 強襲 / デッキ攻撃× / 射手）。None は空文字。
        public static string DisplayName(this GrantableKeyword keyword)
        {
            switch (keyword)
            {
                case GrantableKeyword.Guardian:
                    return "守護";
                case GrantableKeyword.Haste:
                    return "速攻";
                case GrantableKeyword.Flying:
                    return "飛行";
                case GrantableKeyword.Sakimori:
                    return "防人";
                case GrantableKeyword.Assault:
                    return "強襲";
                case GrantableKeyword.NoDeckAttack:
                    return "デッキ攻撃×";
                case GrantableKeyword.Archer:
                    return "射手";
                case GrantableKeyword.Deadly:
                    return "必殺";
                default:
                    return "";
            }
        }
    }
}
