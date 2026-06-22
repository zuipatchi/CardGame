using System;
using System.Collections.Generic;
using UnityEngine;

namespace Common.Cpu
{
    // CPU の思考の強さ。Inspector で相手ごとに設定する。
    // Beginner（初級）：従来どおり、手札の先頭から支払える順にランダム的に使う。
    // Intermediate（中級）：CostBoost／ダメージトリガー持ちのカードは場に出さず、コスト支払いに優先的に回す。
    // Advanced（上級）：当面は中級と同じ挙動（独自ロジックは将来追加）。
    // ※ メンバーは末尾に追加すること（既存の serialized 整数値がズレるため並べ替え・中間削除をしない）。
    public enum CpuDifficulty
    {
        Beginner,
        Intermediate,
        Advanced,
    }

    // CPU 対戦相手のロスター。Home の相手選択オーバーレイで表示し、Main が対戦相手のデッキとして使う。
    // Home（Main を参照しない）からも参照するため Common に置き、デッキは Main の CpuDeckSO ではなく
    // カードIDの配列で保持する（Main 側で CardDatabase.BuildDeck によってデッキを組む）。
    [CreateAssetMenu(fileName = "CpuRoster", menuName = "Card Game/CPU Roster")]
    public sealed class CpuRosterSO : ScriptableObject
    {
        public List<CpuOpponentData> Opponents = new List<CpuOpponentData>();
    }

    [Serializable]
    public sealed class CpuOpponentData
    {
        // 選択画面・対戦画面に表示する相手の名前。空なら呼び出し側で "CPU n" を使う。
        public string Name;
        // 選択画面に表示するポートレート画像。未設定ならカードはプレースホルダー表示になる。
        public Texture2D Portrait;
        // この相手の思考の強さ（既定＝初級）。
        public CpuDifficulty Difficulty;
        // 相手のデッキ（カードIDの並び。30枚を想定）。空なら既存の CpuDeck.asset にフォールバックする。
        public List<string> CardIds = new List<string>();
    }
}
