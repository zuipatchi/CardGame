using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;

namespace Common.Store
{
    public class SoundStore : IStartable
    {
        private readonly UniTaskCompletionSource _loadedTcs = new();
        public UniTask Loaded => _loadedTcs.Task;

        // SE ごとに収録音量がバラバラなので、ロード時に RMS を測って同じ聞こえ方になる音量倍率を求める
        private readonly Dictionary<AudioClip, float> _seVolumeScales = new();

        // 自動正規化のあとに耳で微調整したい SE だけ、クリップ名 -> 追加倍率 で上書きする。
        // 尖った単発音など自動補正が合わないものをここで微調整する（1.0 = 自動のまま、>1 で大きく、<1 で小さく）。
        private static readonly Dictionary<string, float> ManualSeAdjust = new()
        {
            // 例: { "Card", 1.3f },
            { "Battle", 2.2f },
            { "Enter3", 2.8f },
            { "Down", 2.4f },
            { "PlayerTurn", 0.8f },
            { "Card", 0.8f },
            { "CardUse", 0.8f },
            { "Attack", 0.8f },
            { "VictoryPoint", 0.7f },
            { "Cancel1", 0.7f },
        };

        // アドレス
        private readonly string _mainBgmAddressable = "Sound/BGM/CatInPalmBeach";
        private readonly string _maouOrchestraAddressable = "Sound/BGM/MaouOrchestra";
        private readonly string _maouAcousticAddressable = "Sound/BGM/MaouAcoustic";
        private readonly string _maouCyberAddressable = "Sound/BGM/MaouCyber";
        private readonly string _koharuIzmAddressable = "Sound/BGM/KoharuIzm";
        private readonly string _enterSEAddressable = "Sound/SE/Enter1";
        private readonly string _enter2SEAddressable = "Sound/SE/Enter2";
        private readonly string _enter3SEAddressable = "Sound/SE/Enter3";
        private readonly string _cancel1SEAddressable = "Sound/SE/Cancel1";
        private readonly string _resultSEAddressable = "Sound/SE/Result";
        private readonly string _analysisSEAddressable = "Sound/SE/Analysis";
        private readonly string _winSEAddressable = "Sound/SE/Win";
        private readonly string _loseSEAddressable = "Sound/SE/Lose";
        private readonly string _readySEAddressable = "Sound/SE/Ready";
        private readonly string _battleSEAddressable = "Sound/SE/Battle";
        private readonly string _cardSEAddressable = "Sound/SE/Card";
        private readonly string _cardUseSEAddressable = "Sound/SE/CardUse";
        private readonly string _deckDamageSEAddressable = "Sound/SE/DeckDamage";
        private readonly string _victoryPointSEAddressable = "Sound/SE/VictoryPoint";
        private readonly string _downSEAddressable = "Sound/SE/Down";
        private readonly string _attackSEAddressable = "Sound/SE/Attack";
        private readonly string _overLimitSEAddressable = "Sound/SE/OverLimit";
        private readonly string _playerTurnSEAddressable = "Sound/SE/PlayerTurn";
        private readonly string _coinSEAddressable = "Sound/SE/Coin";

        // プロパティ
        public AudioClip MainBGM => _mainBGM;
        public AudioClip MaouOrchestra => _maouOrchestra;
        public AudioClip MaouAcoustic => _maouAcoustic;
        public AudioClip MaouCyber => _maouCyber;
        public AudioClip KoharuIzm => _koharuIzm;
        public AudioClip EnterSE => _enterSE;
        public AudioClip Enter2SE => _enter2SE;
        public AudioClip Enter3SE => _enter3SE;
        public AudioClip Cancel1SE => _cancel1SE;
        public AudioClip ResultSE => _resultSE;
        public AudioClip AnalysisSE => _analysisSE;
        public AudioClip WinSE => _winSE;
        public AudioClip LoseSE => _loseSE;
        public AudioClip ReadySE => _readySE;
        public AudioClip BattleSE => _battleSE;
        public AudioClip CardSE => _cardSE;
        public AudioClip CardUseSE => _cardUseSE;
        public AudioClip DeckDamageSE => _deckDamageSE;
        public AudioClip VictoryPointSE => _victoryPointSE;
        public AudioClip DownSE => _downSE;
        public AudioClip AttackSE => _attackSE;
        public AudioClip OverLimitSE => _overLimitSE;
        public AudioClip PlayerTurnSE => _playerTurnSE;
        public AudioClip CoinSE => _coinSE;

        // メンバー
        private AudioClip _mainBGM = null;
        private AudioClip _maouOrchestra = null;
        private AudioClip _maouAcoustic = null;
        private AudioClip _maouCyber = null;
        private AudioClip _koharuIzm = null;
        private AudioClip _enterSE = null;
        private AudioClip _enter2SE = null;
        private AudioClip _enter3SE = null;
        private AudioClip _cancel1SE = null;
        private AudioClip _resultSE = null;
        private AudioClip _analysisSE = null;
        private AudioClip _winSE = null;
        private AudioClip _loseSE = null;
        private AudioClip _readySE = null;
        private AudioClip _battleSE = null;
        private AudioClip _cardSE = null;
        private AudioClip _cardUseSE = null;
        private AudioClip _deckDamageSE = null;
        private AudioClip _victoryPointSE = null;
        private AudioClip _downSE = null;
        private AudioClip _attackSE = null;
        private AudioClip _overLimitSE = null;
        private AudioClip _playerTurnSE = null;
        private AudioClip _coinSE = null;

        public void Start()
        {
            LoadAssets().Forget();
        }

        private async UniTask LoadAssets()
        {
            try
            {
                _mainBGM = await Addressables.LoadAssetAsync<AudioClip>(_mainBgmAddressable).ToUniTask();
                _maouOrchestra = await Addressables.LoadAssetAsync<AudioClip>(_maouOrchestraAddressable).ToUniTask();
                _maouAcoustic = await Addressables.LoadAssetAsync<AudioClip>(_maouAcousticAddressable).ToUniTask();
                _maouCyber = await Addressables.LoadAssetAsync<AudioClip>(_maouCyberAddressable).ToUniTask();
                _koharuIzm = await Addressables.LoadAssetAsync<AudioClip>(_koharuIzmAddressable).ToUniTask();
                _enterSE = await Addressables.LoadAssetAsync<AudioClip>(_enterSEAddressable).ToUniTask();
                _enter2SE = await Addressables.LoadAssetAsync<AudioClip>(_enter2SEAddressable).ToUniTask();
                _enter3SE = await Addressables.LoadAssetAsync<AudioClip>(_enter3SEAddressable).ToUniTask();
                _cancel1SE = await Addressables.LoadAssetAsync<AudioClip>(_cancel1SEAddressable).ToUniTask();
                _resultSE = await Addressables.LoadAssetAsync<AudioClip>(_resultSEAddressable).ToUniTask();
                _analysisSE = await Addressables.LoadAssetAsync<AudioClip>(_analysisSEAddressable).ToUniTask();
                _winSE = await Addressables.LoadAssetAsync<AudioClip>(_winSEAddressable).ToUniTask();
                _loseSE = await Addressables.LoadAssetAsync<AudioClip>(_loseSEAddressable).ToUniTask();
                _readySE = await Addressables.LoadAssetAsync<AudioClip>(_readySEAddressable).ToUniTask();
                _battleSE = await Addressables.LoadAssetAsync<AudioClip>(_battleSEAddressable).ToUniTask();
                _cardSE = await Addressables.LoadAssetAsync<AudioClip>(_cardSEAddressable).ToUniTask();
                _cardUseSE = await Addressables.LoadAssetAsync<AudioClip>(_cardUseSEAddressable).ToUniTask();
                _deckDamageSE = await Addressables.LoadAssetAsync<AudioClip>(_deckDamageSEAddressable).ToUniTask();
                _victoryPointSE = await Addressables.LoadAssetAsync<AudioClip>(_victoryPointSEAddressable).ToUniTask();
                _downSE = await Addressables.LoadAssetAsync<AudioClip>(_downSEAddressable).ToUniTask();
                _attackSE = await Addressables.LoadAssetAsync<AudioClip>(_attackSEAddressable).ToUniTask();
                _overLimitSE = await Addressables.LoadAssetAsync<AudioClip>(_overLimitSEAddressable).ToUniTask();
                _playerTurnSE = await Addressables.LoadAssetAsync<AudioClip>(_playerTurnSEAddressable).ToUniTask();
                _coinSE = await Addressables.LoadAssetAsync<AudioClip>(_coinSEAddressable).ToUniTask();
                NormalizeSeVolumes();
                _loadedTcs.TrySetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"サウンドアセットのロードに失敗: {e}");
                _loadedTcs.TrySetResult();
            }
        }

        /// <summary>
        /// SE を再生するときに掛ける音量倍率（ロード時の正規化＋手動微調整の結果）を返す。
        /// 正規化できなかった（解析不能な）クリップや未登録のクリップは 1.0（等倍）。
        /// </summary>
        public float GetSeVolumeScale(AudioClip clip)
        {
            if (clip != null && _seVolumeScales.TryGetValue(clip, out float scale))
            {
                return scale;
            }
            return 1f;
        }

        // 各 SE の RMS（実効音量）を測り、全 SE の中央値を目標にして同じ聞こえ方になる倍率を算出する。
        private void NormalizeSeVolumes()
        {
            AudioClip[] seClips =
            {
                _enterSE, _enter2SE, _enter3SE, _cancel1SE, _resultSE, _analysisSE,
                _winSE, _loseSE, _readySE, _battleSE, _cardSE, _cardUseSE,
                _deckDamageSE, _victoryPointSE, _downSE, _attackSE, _overLimitSE, _playerTurnSE,
                _coinSE,
            };

            List<(AudioClip Clip, float Loudness, float Peak)> analyses = new();
            List<string> unreadable = new();
            foreach (AudioClip clip in seClips)
            {
                if (clip == null)
                {
                    continue;
                }
                (float loudness, float peak) = AnalyzeClip(clip);
                if (loudness > 0f)
                {
                    analyses.Add((clip, loudness, peak));
                }
                else
                {
                    // GetData で読めなかった = Load Type が Decompress On Load 以外の可能性が高い
                    unreadable.Add(clip.name);
                }
            }

            if (unreadable.Count > 0)
            {
                Debug.LogWarning($"[SoundStore] SE 音量正規化: 解析できず等倍のままのクリップ（Load Type を Decompress On Load にしてください）: {string.Join(", ", unreadable)}");
            }

            if (analyses.Count == 0)
            {
                return;
            }

            // 平均だと極端に大きい/小さいクリップに引っ張られるので中央値を目標ラウドネスにする
            List<float> sorted = analyses.Select(a => a.Loudness).OrderBy(v => v).ToList();
            float target = sorted[sorted.Count / 2];

            foreach ((AudioClip clip, float loudness, float peak) in analyses)
            {
                float gain = target / loudness;
                // ピークが 1.0 を超えると音割れ（クリッピング）するので、ヘッドルームで上限を制限する
                if (peak > 0f)
                {
                    gain = Mathf.Min(gain, 0.99f / peak);
                }
                gain = Mathf.Clamp(gain, 0.1f, 4f);
                if (ManualSeAdjust.TryGetValue(clip.name, out float manual))
                {
                    gain *= manual;
                }
                _seVolumeScales[clip] = gain;
            }
        }

        // クリップの体感音量（約100msブロックごとの最大RMS）とピーク（最大振幅）を返す。
        // ブロック最大にすることで、短い効果音や末尾の無音による平均の薄まりに左右されにくくする。
        // 解析できない場合は (0, 0)。
        private static (float Loudness, float Peak) AnalyzeClip(AudioClip clip)
        {
            int sampleCount = clip.samples * clip.channels;
            if (sampleCount <= 0)
            {
                return (0f, 0f);
            }

            float[] data = new float[sampleCount];
            try
            {
                // Load Type が Compressed In Memory などだと取得できない。その場合は等倍にフォールバック。
                if (!clip.GetData(data, 0))
                {
                    return (0f, 0f);
                }
            }
            catch (Exception)
            {
                return (0f, 0f);
            }

            int blockSize = Mathf.Max(1, (int)(clip.frequency * 0.1f) * clip.channels);
            float maxBlockRms = 0f;
            float peak = 0f;
            for (int start = 0; start < sampleCount; start += blockSize)
            {
                int end = Mathf.Min(start + blockSize, sampleCount);
                double sumSquares = 0.0;
                for (int i = start; i < end; i++)
                {
                    float sample = data[i];
                    sumSquares += (double)sample * sample;
                    float abs = sample < 0f ? -sample : sample;
                    if (abs > peak)
                    {
                        peak = abs;
                    }
                }
                float blockRms = (float)Math.Sqrt(sumSquares / (end - start));
                if (blockRms > maxBlockRms)
                {
                    maxBlockRms = blockRms;
                }
            }

            return (maxBlockRms, peak);
        }
    }
}
