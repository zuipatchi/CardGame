using Common.Username;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class UsernameValidatorTests
    {
        [Test]
        public void 空文字は無効でエラーメッセージなし()
        {
            bool result = UsernameValidator.IsValid(string.Empty, out string error);

            Assert.IsFalse(result);
            Assert.IsEmpty(error);
        }

        [Test]
        public void 空白のみは無効でエラーメッセージなし()
        {
            bool result = UsernameValidator.IsValid("   ", out string error);

            Assert.IsFalse(result);
            Assert.IsEmpty(error);
        }

        [Test]
        public void 半角16文字は有効()
        {
            bool result = UsernameValidator.IsValid("abcdefghijklmnop", out string error);

            Assert.IsTrue(result);
            Assert.IsEmpty(error);
        }

        [Test]
        public void 半角17文字は無効でエラーメッセージあり()
        {
            bool result = UsernameValidator.IsValid("abcdefghijklmnopq", out string error);

            Assert.IsFalse(result);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void 全角8文字は有効()
        {
            bool result = UsernameValidator.IsValid("あいうえおかきく", out string error);

            Assert.IsTrue(result);
            Assert.IsEmpty(error);
        }

        [Test]
        public void 全角9文字は無効でエラーメッセージあり()
        {
            bool result = UsernameValidator.IsValid("あいうえおかきくけ", out string error);

            Assert.IsFalse(result);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void 半角8文字と全角4文字は合計重み16で有効()
        {
            bool result = UsernameValidator.IsValid("abcdefghあいうえ", out string error);

            Assert.IsTrue(result);
            Assert.IsEmpty(error);
        }

        [Test]
        public void 半角7文字と全角5文字は合計重み17で無効()
        {
            bool result = UsernameValidator.IsValid("abcdefgあいうえお", out string error);

            Assert.IsFalse(result);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void 半角カナは半角扱いで有効範囲内()
        {
            // ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿ = 15半角カナ (重み 15)
            bool result = UsernameValidator.IsValid("ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿ", out string error);

            Assert.IsTrue(result);
            Assert.IsEmpty(error);
        }

        [Test]
        public void nullは無効でエラーメッセージなし()
        {
            bool result = UsernameValidator.IsValid(null, out string error);

            Assert.IsFalse(result);
            Assert.IsEmpty(error);
        }
    }
}
