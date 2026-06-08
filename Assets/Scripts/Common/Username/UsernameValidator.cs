namespace Common.Username
{
    public static class UsernameValidator
    {
        private const int MaxWeight = 16;
        private const string TooLongMessage = "16文字（全角8文字）まで入力できます";

        public static bool IsValid(string input, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = string.Empty;
                return false;
            }

            int weight = CalcWeight(input.Trim());
            if (weight > MaxWeight)
            {
                errorMessage = TooLongMessage;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static int CalcWeight(string text)
        {
            int total = 0;
            foreach (char c in text)
            {
                total += IsHalfWidth(c) ? 1 : 2;
            }
            return total;
        }

        private static bool IsHalfWidth(char c)
        {
            // ASCII printable
            if (c >= 0x0020 && c <= 0x007E)
            {
                return true;
            }
            // Half-width Katakana
            if (c >= 0xFF61 && c <= 0xFF9F)
            {
                return true;
            }
            return false;
        }
    }
}
