using Common.Option;
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
        private readonly CompositeDisposable _disposables = new();

        [Inject]
        public void Construct(OptionModel optionModel)
        {
            _optionModel = optionModel;
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

        public void PlaySE(AudioClip clip)
        {
            if (_seAudioSource == null || clip == null)
            {
                return;
            }
            _seAudioSource.PlayOneShot(clip);
        }

        // フレーバーテキストの読み上げを再生する。
        // 連続再生時は前の読み上げを止めてから鳴らす（重なって聞こえないようにするため）。
        public void PlayVoice(AudioClip clip)
        {
            if (_voiceAudioSource == null || clip == null)
            {
                return;
            }
            _voiceAudioSource.Stop();
            _voiceAudioSource.clip = clip;
            _voiceAudioSource.Play();
        }
    }
}
