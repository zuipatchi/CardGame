using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;

namespace Common.Cpu
{
    // CPU ロスター（CpuRosterSO）を起動時にロードして保持する Common 常駐ストア。
    // Home の相手選択オーバーレイと Main の対戦相手デッキの両方から参照する。
    public sealed class CpuRosterStore : IStartable
    {
        // ロスター未登録時に相手選択オーバーレイへ並べるプレースホルダーの数。
        public const int PlaceholderCount = 8;

        private readonly string _rosterAddressable = "Card/CpuRoster";

        private readonly UniTaskCompletionSource _loadedTcs = new();
        public UniTask Loaded => _loadedTcs.Task;

        public CpuRosterSO Roster => _roster;
        private CpuRosterSO _roster;

        public void Start()
        {
            LoadAsync().Forget();
        }

        private async UniTask LoadAsync()
        {
            try
            {
                _roster = await Addressables.LoadAssetAsync<CpuRosterSO>(_rosterAddressable).ToUniTask();
            }
            catch (Exception e)
            {
                // ロスター未登録でも既存の CpuDeck.asset でプレースホルダー対戦できるよう、ここでは握りつぶす。
                Debug.LogWarning($"CPU ロスターのロードをスキップ: {e.Message}");
            }
            _loadedTcs.TrySetResult();
        }

        // 相手選択オーバーレイに並べる相手数。未登録・空ならプレースホルダー数を返す。
        public int OpponentCount => _roster != null && _roster.Opponents.Count > 0
            ? _roster.Opponents.Count
            : PlaceholderCount;

        // 指定 index の相手データ。未登録・範囲外なら null（呼び出し側でプレースホルダー扱いにする）。
        public CpuOpponentData GetOpponent(int index)
        {
            if (_roster == null || index < 0 || index >= _roster.Opponents.Count)
            {
                return null;
            }
            return _roster.Opponents[index];
        }

        // 選択画面・対戦画面で共通して使う表示名。データがあればその名前、なければ "CPU n"。
        public string DisplayName(int index)
        {
            CpuOpponentData data = GetOpponent(index);
            if (data != null && !string.IsNullOrEmpty(data.Name))
            {
                return data.Name;
            }
            return $"CPU {index + 1}";
        }
    }
}
