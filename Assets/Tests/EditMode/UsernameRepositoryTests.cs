using Common.Username;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class UsernameRepositoryTests
    {
        private const string SaveKey = "Username";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void 保存したユーザーネームをLoadで取得できる()
        {
            UsernameRepository repository = new UsernameRepository();

            repository.Save("TestUser");
            string result = repository.Load();

            Assert.AreEqual("TestUser", result);
        }

        [Test]
        public void 何も保存していないときLoadはnullを返す()
        {
            UsernameRepository repository = new UsernameRepository();

            string result = repository.Load();

            Assert.IsNull(result);
        }

        [Test]
        public void 上書き保存した場合は新しい値が返る()
        {
            UsernameRepository repository = new UsernameRepository();

            repository.Save("FirstName");
            repository.Save("SecondName");
            string result = repository.Load();

            Assert.AreEqual("SecondName", result);
        }
    }
}
