using System.Collections.Generic;
using UnityEngine;

namespace Home
{
    [CreateAssetMenu(fileName = "DogSpeechLinesSO", menuName = "Card/Home/Dog Speech Lines")]
    public sealed class DogSpeechLinesSO : ScriptableObject
    {
        // VOICEVOX の話者 ID。犬セリフ音声の一括生成時に使う。0 以下なら生成ツール側の既定話者にフォールバック。
        // 既定 1＝ずんだもん（あまあま）。インスペクタで変更可。
        [SerializeField]
        private int _voiceSpeaker = 1;

        [SerializeField]
        private string[] _idleMessages =
        {
            "わんっ！",
            "あそぼあそぼ！",
            "なにかくれる？",
            "みてみてー！",
            "いいてんきだわん",
            "ねえねえ、{name}、あそぼ！",
            "{name}、だいすきだわん！",
        };

        [SerializeField]
        private string[] _eatMessages =
        {
            "おいしい！！",
            "もっとほしいわん！",
            "ありがとうわん！",
        };

        [SerializeField]
        private string[] _rainMessages =
        {
            "雨だ...ぬれるのいやだわん",
            "はやくおうちにはいりたい...",
            "さむいわん...",
        };

        public string[] IdleMessages => _idleMessages;
        public string[] EatMessages => _eatMessages;
        public string[] RainMessages => _rainMessages;
        public int VoiceSpeaker => _voiceSpeaker;

        // 全カテゴリのセリフ本文を重複なしで返す（犬セリフ音声の一括生成で使う）。
        public IReadOnlyList<string> AllLines
        {
            get
            {
                List<string> result = new List<string>();
                HashSet<string> seen = new HashSet<string>();
                AddRange(result, seen, _idleMessages);
                AddRange(result, seen, _eatMessages);
                AddRange(result, seen, _rainMessages);
                return result;
            }
        }

        private static void AddRange(List<string> result, HashSet<string> seen, string[] lines)
        {
            if (lines == null)
            {
                return;
            }
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line) && seen.Add(line))
                {
                    result.Add(line);
                }
            }
        }
    }
}
