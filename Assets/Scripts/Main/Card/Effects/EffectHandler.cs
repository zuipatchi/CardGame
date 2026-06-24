using System.Threading;
using Cysharp.Threading.Tasks;

namespace Main.Card.Effects
{
    // 1つの効果種別（EventType）の振る舞いとエディタ用メタデータをまとめた基底クラス。
    //
    // ■ 新しい効果を追加する手順（このパターンの目的）
    //   1. EffectType.cs（enum EventType）に値を1つ追加する
    //   2. EffectHandler を継承したクラスを1つ作る（このフォルダの Handlers/ 配下）
    //   3. クラスに [Preserve] を付ける（IL2CPP ビルドでの型ストリップ対策）
    //   → EffectCatalog がアセンブリを走査して自動登録するため、既存ファイルの switch 編集は不要。
    //
    //   実際の盤面操作（ドロー・ダメージ・召喚など）は MainPresenter 側の internal メソッドを
    //   building-block として呼び出す。新しい盤面操作が必要な場合のみ MainPresenter にメソッドを足す。
    //
    // ■ 演出と適用
    //   ApplyAsync は「事前演出（Play*）＋ 盤面適用（Apply*）」を1メソッド内で完結させる。
    //   勝利点付帯値（VictoryPointBonus）は効果種別に依存しないため、ここでは扱わず呼び出し側で加算する。
    public abstract class EffectHandler
    {
        // この効果が対応する EventType。EffectCatalog のキーになる。
        public abstract EventType Type { get; }

        // キャラカードの効果として有効か（CharacterCardData.EffectType から発動できるか）。
        public virtual bool ValidOnCharacter => true;

        // イベントカードの効果として有効か（EventCardData.EventType から発動できるか）。
        public virtual bool ValidOnEvent => true;

        // エディタの値1/値2 入力欄ラベルとヒント。
        public abstract EffectValueInfo Values { get; }

        // エディタの効果テキスト自動生成に使う本体テキスト。効果なし／未実装は空文字を返す。
        public abstract string BuildBody(EffectTextContext ctx);

        // 効果の実行（事前演出 ＋ 盤面適用）。盤面操作は presenter の internal building-block を呼ぶ。
        public abstract UniTask ApplyAsync(MainPresenter presenter, EffectInvocation inv, CancellationToken ct);

        // キーワードバフ（攻撃力／HP）の効果テキストを作る共有ヘルパー。
        protected static string KeywordBuffBody(string keyword, int value, string statName)
        {
            string subject = string.IsNullOrEmpty(keyword)
                ? "自分以外の味方キャラ"
                : $"自分以外の『{keyword}』を持つ味方キャラ";
            return $"{subject}の{statName}を{value}上げる";
        }

        // 自フィールドから値1体（0=場の味方全員）選ぶ効果テキストの対象部分を作る共有ヘルパー。
        protected static string AlliesTargetPrefix(int value1)
        {
            return value1 <= 0 ? "自分のキャラ全体" : $"自分のキャラ{value1}体";
        }

        // 敵フィールドから値1体（0=敵全員）選ぶ効果テキストの対象部分を作る共有ヘルパー。
        protected static string EnemiesTargetPrefix(int value1)
        {
            return value1 <= 0 ? "相手キャラ全体" : $"相手キャラ{value1}体";
        }
    }
}
