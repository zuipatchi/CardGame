using UnityEngine;

namespace Home
{
    [CreateAssetMenu(fileName = "DogSpeechLinesSO", menuName = "Card/Home/Dog Speech Lines")]
    public sealed class DogSpeechLinesSO : ScriptableObject
    {
        [SerializeField]
        private string[] _idleMessages =
        {
            "わんっ！",
            "あそぼあそぼ！",
            "なにかくれる？",
            "みてみてー！",
            "いいてんきだわん",
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
    }
}
