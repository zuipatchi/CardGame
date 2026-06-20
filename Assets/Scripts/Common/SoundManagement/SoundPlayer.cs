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
        // 途中で止められる持続 SE 専用（コイントスなど。演出終了に合わせて StopLoopSE で停止する）。
        private AudioSource _loopSeAudioSource;
        // 現在ループ再生中のクリップに掛ける音量倍率（SEVolume 変更時に再計算するため保持）。
        private float _loopSeVolumeScale = 1f;
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

            _loopSeAudioSource = gameObject.AddComponent<AudioSource>();
            _loopSeAudioSource.playOnAwake = false;
            _loopSeAudioSource.loop = true;

            // 持続 SE も SE 音量に連動させる（再生中に音量を変えても追従する）。
            _optionModel.SEVolume
                .Subscribe(v => _loopSeAudioSource.volume = v / 2 * _loopSeVolumeScale)
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

        // 途中で止められる持続 SE を再生する（コイントスの回転音など）。StopLoopSE で停止するまでループする。
        public void PlayLoopSE(AudioClip clip)
        {
            if (_loopSeAudioSource == null || clip == null)
            {
                return;
            }
            _loopSeVolumeScale = _soundStore != null ? _soundStore.GetSeVolumeScale(clip) : 1f;
            _loopSeAudioSource.clip = clip;
            _loopSeAudioSource.volume = _optionModel.SEVolume.CurrentValue / 2 * _loopSeVolumeScale;
            _loopSeAudioSource.Play();
        }

        // PlayLoopSE で再生中の持続 SE を停止する。
        public void StopLoopSE()
        {
            if (_loopSeAudioSource == null)
            {
                return;
            }
            _loopSeAudioSource.Stop();
            _loopSeAudioSource.clip = null;
            _loopSeVolumeScale = 1f;
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
