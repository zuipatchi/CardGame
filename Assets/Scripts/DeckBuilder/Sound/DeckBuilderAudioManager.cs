using System;
using System.Threading;
using Common.SoundManagement;
using Common.Store;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace DeckBuilder.Sound
{
    /// <summary>
    /// デッキ構築シーンの BGM を再生する。
    /// </summary>
    public sealed class DeckBuilderAudioManager : IAsyncStartable
    {
        private readonly SoundPlayer _soundPlayer;
        private readonly SoundStore _soundStore;

        [Inject]
        public DeckBuilderAudioManager(SoundPlayer soundPlayer, SoundStore soundStore)
        {
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            try
            {
                await _soundStore.Loaded.AttachExternalCancellation(cancellation);
                _soundPlayer.PlayBGM(_soundStore.MaouAcoustic);
            }
            catch (OperationCanceledException) { }
        }
    }
}
