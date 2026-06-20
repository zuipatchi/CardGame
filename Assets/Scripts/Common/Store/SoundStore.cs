using System;
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

        // アドレス
        private readonly string _mainBgmAddressable = "Sound/BGM/CatInPalmBeach";
        private readonly string _maouOrchestraAddressable = "Sound/BGM/MaouOrchestra";
        private readonly string _maouAcousticAddressable = "Sound/BGM/MaouAcoustic";
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
        private readonly string _limitBreakSEAddressable = "Sound/SE/LimitBreak";
        private readonly string _playerTurnSEAddressable = "Sound/SE/PlayerTurn";

        // プロパティ
        public AudioClip MainBGM => _mainBGM;
        public AudioClip MaouOrchestra => _maouOrchestra;
        public AudioClip MaouAcoustic => _maouAcoustic;
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
        public AudioClip LimitBreakSE => _limitBreakSE;
        public AudioClip PlayerTurnSE => _playerTurnSE;

        // メンバー
        private AudioClip _mainBGM = null;
        private AudioClip _maouOrchestra = null;
        private AudioClip _maouAcoustic = null;
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
        private AudioClip _limitBreakSE = null;
        private AudioClip _playerTurnSE = null;

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
                _limitBreakSE = await Addressables.LoadAssetAsync<AudioClip>(_limitBreakSEAddressable).ToUniTask();
                _playerTurnSE = await Addressables.LoadAssetAsync<AudioClip>(_playerTurnSEAddressable).ToUniTask();
                _loadedTcs.TrySetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"サウンドアセットのロードに失敗: {e}");
                _loadedTcs.TrySetResult();
            }
        }
    }
}
