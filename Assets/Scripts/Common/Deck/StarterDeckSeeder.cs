using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;

namespace Common.Deck
{
    // 初回起動時に、初期デッキ（StarterDeckSO）をスロット 0 へ自動生成する Common 常駐エントリポイント。
    // 一度シードしたら以後は何もしない（StarterDeckSeeded フラグ）。既にデッキを持つ既存プレイヤーには
    // 上書きせず、フラグだけ立てて二度とシードしないようにする。
    public sealed class StarterDeckSeeder : IStartable
    {
        private const int StarterSlot = 0;
        private readonly string _starterDeckAddressable = "Deck/StarterDeck";

        private readonly DeckRepository _deckRepository;

        public StarterDeckSeeder(DeckRepository deckRepository)
        {
            _deckRepository = deckRepository;
        }

        public void Start()
        {
            SeedAsync().Forget();
        }

        private async UniTask SeedAsync()
        {
            // 既にシード済みなら何もしない。
            if (_deckRepository.IsStarterSeeded)
            {
                return;
            }

            // 既にデッキを持つ既存プレイヤーには初期デッキを作らず、フラグだけ立てて二度と判定しない。
            if (_deckRepository.HasAnyDeck())
            {
                _deckRepository.MarkStarterSeeded();
                return;
            }

            StarterDeckSO starter;
            try
            {
                starter = await Addressables.LoadAssetAsync<StarterDeckSO>(_starterDeckAddressable).ToUniTask();
            }
            catch (Exception e)
            {
                // 初期デッキ未登録でもゲーム自体は動く（プレイヤーがデッキ構築で作れる）ため握りつぶす。
                // フラグは立てず、次回起動で再試行する。
                Debug.LogWarning($"初期デッキのロードをスキップ: {e.Message}");
                return;
            }

            if (starter != null && starter.CardIds != null && starter.CardIds.Count > 0)
            {
                // SeedSlotWithIds がスロット保存と同時にシード済みフラグを立てる。
                _deckRepository.SeedSlotWithIds(StarterSlot, starter.CardIds);
            }
        }
    }
}
