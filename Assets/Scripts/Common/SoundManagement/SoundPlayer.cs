using Common.Option;
using Common.Store;
using R3;
using UnityEngine;
using VContainer;

namespace Common.SoundManagement
{
    /// <summary>
    /// AudioClip を再生するクラス
    /// OptionModel の volume で音量を管理している
    /// </summary>
    public class SoundPlayer : MonoBehaviour
    {
        private AudioSource _bgmAudioSource;
        private AudioSource _seAudioSource;
        // フレーバーテキスト読み上げ専用。SE と分けることで、新しい読み上げ時に前の読み上げだけを止められる。
        private AudioSource _voiceAudioSource;
        private OptionModel _optionModel;
        private SoundStore _soundStore;
        private readonly CompositeDisposable _disposables = new();

        [Inject]
        public void Construct(OptionModel optionModel, SoundStore soundStore)
        {
            _optionModel = optionModel;
            _soundStore = soundStore;
        }

        private void Start()
        {
            _bgmAudioSource = gameObject.AddComponent<AudioSource>();
            _bgmAudioSource.loop = true;

            _optionModel.BGMVolume
                .Subscribe(v => _bgmAudioSource.volume = v / 2)
                .AddTo(_disposables);

            _seAudioSource = gameObject.AddComponent<AudioSource>();
            _seAudioSource.playOnAwake = false;
            _seAudioSource.loop = false;

            _optionModel.SEVolume
                .Subscribe(v => _seAudioSource.volume = v / 2)
                .AddTo(_disposables);

            // 読み上げ音声は SE とは独立した音量（VoiceVolume）に連動させる
            _voiceAudioSource = gameObject.AddComponent<AudioSource>();
            _voiceAudioSource.playOnAwake = false;
            _voiceAudioSource.loop = false;

            _optionModel.VoiceVolume
                .Subscribe(v => _voiceAudioSource.volume = v / 2)
                .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        public void PlayBGM(AudioClip clip)
        {
            if (_bgmAudioSource == null || clip == null)
            {
                return;
            }
            _bgmAudioSource.clip = clip;
            _bgmAudioSource.Play();
        }

        public void StopBGM()
        {
            if (_bgmAudioSource == null)
            {
                return;
            }
            _bgmAudioSource.Stop();
        }

        public void PlaySE(AudioClip clip)
        {
            if (_seAudioSource == null || clip == null)
            {
                return;
            }
            // SE ごとの収録音量差を打ち消す倍率を掛けて、どの音源でも同じ聞こえ方にする
            float volumeScale = _soundStore != null ? _soundStore.GetSeVolumeScale(clip) : 1f;
            _seAudioSource.PlayOneShot(clip, volumeScale);
        }

        // フレーバーテキストの読み上げを再生する。
        // 前の読み上げが終わる前でも止めずに重ねて鳴らす（一時的に複数同時に流れてもよい）。
        public void PlayVoice(AudioClip clip)
        {
            if (_voiceAudioSource == null || clip == null)
            {
                return;
            }
            _voiceAudioSource.PlayOneShot(clip);
        }
    }
}
