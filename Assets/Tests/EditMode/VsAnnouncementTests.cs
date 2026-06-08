using NUnit.Framework;

namespace Tests.EditMode
{
    public class VsAnnouncementTests
    {
        private static string ComputeLocalDisplayName(string localUsername)
        {
            return string.IsNullOrEmpty(localUsername) ? "ゲスト" : localUsername;
        }

        private static string ComputeOpponentDisplayName(bool isOnline, string opponentUsername)
        {
            return isOnline ? opponentUsername : "CPU";
        }

        [Test]
        public void CPU対戦では相手表示名がCPUになる()
        {
            string opponentName = ComputeOpponentDisplayName(isOnline: false, string.Empty);

            Assert.AreEqual("CPU", opponentName);
        }

        [Test]
        public void ユーザーネーム未設定時はゲストにフォールバックする()
        {
            string localName = ComputeLocalDisplayName(string.Empty);

            Assert.AreEqual("ゲスト", localName);
        }

        [Test]
        public void ユーザーネーム設定済みの場合はそのまま表示される()
        {
            string localName = ComputeLocalDisplayName("ちっぱ");

            Assert.AreEqual("ちっぱ", localName);
        }

        [Test]
        public void オンライン対戦では相手のユーザーネームが表示される()
        {
            string opponentName = ComputeOpponentDisplayName(isOnline: true, "はなこ");

            Assert.AreEqual("はなこ", opponentName);
        }
    }
}
