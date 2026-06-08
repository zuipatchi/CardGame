using System.Collections;
using System.Reflection;
using Common.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.PlayMode
{
    public class UsernameModalTests
    {
        private const string UsernameKey = "Username";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.Save();
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetSceneByName("Common").isLoaded);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.Save();
            typeof(CommonSceneLoader)
                .GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ユーザーネーム未登録のときモーダルが表示される()
        {
            VisualElement overlay = FindOverlay();

            Assert.IsNotNull(overlay, "Overlay が見つかりません");
            Assert.AreEqual(DisplayStyle.Flex, overlay.resolvedStyle.display, "モーダルが表示されていません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 名前入力して決定するとモーダルが閉じる()
        {
            TextField nameField = FindNameField();
            Button confirmButton = FindConfirmButton();
            Assert.IsNotNull(nameField, "NameField が見つかりません");
            Assert.IsNotNull(confirmButton, "ConfirmButton が見つかりません");

            nameField.value = "NewUser";
            yield return null;

            using (ClickEvent submitEvt = ClickEvent.GetPooled())
            {
                submitEvt.target = confirmButton;
                confirmButton.SendEvent(submitEvt);
            }
            yield return null;

            VisualElement overlay = FindOverlay();
            Assert.AreEqual(DisplayStyle.None, overlay.resolvedStyle.display, "決定後にモーダルが閉じていません");
        }

        [UnityTest]
        public IEnumerator 名前入力して決定するとPlayerPrefsに保存される()
        {
            TextField nameField = FindNameField();
            Button confirmButton = FindConfirmButton();
            Assert.IsNotNull(nameField, "NameField が見つかりません");
            Assert.IsNotNull(confirmButton, "ConfirmButton が見つかりません");

            nameField.value = "StoredUser";
            yield return null;

            using (ClickEvent submitEvt = ClickEvent.GetPooled())
            {
                submitEvt.target = confirmButton;
                confirmButton.SendEvent(submitEvt);
            }
            yield return null;

            string saved = PlayerPrefs.GetString(UsernameKey, null);
            Assert.AreEqual("StoredUser", saved, "PlayerPrefs に名前が保存されていません");
        }

        private static VisualElement FindOverlay()
        {
            return FindInTitleScene<VisualElement>("Overlay");
        }

        private static TextField FindNameField()
        {
            return FindInTitleScene<TextField>("NameField");
        }

        private static Button FindConfirmButton()
        {
            return FindInTitleScene<Button>("ConfirmButton");
        }

        private static T FindInTitleScene<T>(string elementName) where T : VisualElement
        {
            Scene scene = SceneManager.GetSceneByName("Title");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    T element = doc.rootVisualElement?.Q<T>(elementName);
                    if (element != null)
                    {
                        return element;
                    }
                }
            }
            return null;
        }
    }

    public class UsernameModalWithSavedUsernameTests
    {
        private const string UsernameKey = "Username";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.SetString(UsernameKey, "ExistingUser");
            PlayerPrefs.Save();
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetSceneByName("Common").isLoaded);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.Save();
            typeof(CommonSceneLoader)
                .GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ユーザーネーム登録済みのときモーダルが表示されない()
        {
            VisualElement overlay = FindInTitleScene<VisualElement>("Overlay");

            Assert.IsNotNull(overlay, "Overlay が見つかりません");
            Assert.AreEqual(DisplayStyle.None, overlay.resolvedStyle.display, "登録済みなのにモーダルが表示されています");
            yield return null;
        }

        private static T FindInTitleScene<T>(string elementName) where T : VisualElement
        {
            Scene scene = SceneManager.GetSceneByName("Title");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    T element = doc.rootVisualElement?.Q<T>(elementName);
                    if (element != null)
                    {
                        return element;
                    }
                }
            }
            return null;
        }
    }
}
