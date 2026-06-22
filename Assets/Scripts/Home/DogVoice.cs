namespace Home
{
    /// <summary>
    /// 犬のセリフ読み上げ音声（VOICEVOX 事前生成）の Addressables アドレスを決める共通ロジック。
    /// セリフ本文から安定したキーを算出し、ランタイム（DogVoiceStore）とエディタ生成ツール
    /// （FlavorVoiceGeneratorWindow）の双方で同じアドレスを参照できるようにする。
    /// 本文をキーにするため、セリフの並び替えに強く、本文を編集すれば自動で別アドレス扱いになる。
    /// </summary>
    public static class DogVoice
    {
        // アドレス例: "Voice/Dog/1a2b3c4d"
        public const string AddressPrefix = "Voice/Dog/";

        /// <summary>
        /// セリフ本文（{name} を含む生テンプレート）から安定したキー（16進8桁）を作る。
        /// C# 標準の string.GetHashCode は実行ごとに値が変わるため、決定的な FNV-1a を使う。
        /// </summary>
        public static string Key(string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine))
            {
                return string.Empty;
            }

            uint hash = 2166136261u;
            foreach (char c in rawLine)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash.ToString("x8");
        }

        /// <summary>
        /// 読み上げ用テキスト。VOICEVOX が「{name}」を文字どおり読んでしまうためプレースホルダを除去し、
        /// 残った不自然な読点・空白を整える。
        /// </summary>
        public static string ToSpeechText(string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine))
            {
                return string.Empty;
            }

            string text = rawLine.Replace("{name}", string.Empty);
            while (text.Contains("、、"))
            {
                text = text.Replace("、、", "、");
            }
            return text.Trim('、', ' ', '　');
        }
    }
}
