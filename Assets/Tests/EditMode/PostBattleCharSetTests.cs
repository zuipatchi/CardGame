using Main.Card;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public class PostBattleCharSetTests
    {
        private static readonly string TemplatePath = "Assets/AddressableAssets/Card/Card.uxml";

        private static CardView MakeCharacter()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return new CardView(template, new CharacterCardData("C001", "戦士", 2, 2));
        }

        [Test]
        public void 両スロットにキャラがいる場合はフェーズをスキップする条件が成立する()
        {
            CharacterSlotView playerSlot = new CharacterSlotView();
            CharacterSlotView opponentSlot = new CharacterSlotView();
            playerSlot.PlaceCard(MakeCharacter());
            opponentSlot.PlaceCard(MakeCharacter());

            bool shouldSkip = playerSlot.CurrentCard != null && opponentSlot.CurrentCard != null;

            Assert.IsTrue(shouldSkip);
        }

        [Test]
        public void プレイヤーのスロットが空の場合はスキップしない()
        {
            CharacterSlotView playerSlot = new CharacterSlotView();
            CharacterSlotView opponentSlot = new CharacterSlotView();
            opponentSlot.PlaceCard(MakeCharacter());

            bool shouldSkip = playerSlot.CurrentCard != null && opponentSlot.CurrentCard != null;

            Assert.IsFalse(shouldSkip);
        }

        [Test]
        public void スロットが埋まっているプレイヤーは強制パスになる条件が成立する()
        {
            CharacterSlotView slot = new CharacterSlotView();
            slot.PlaceCard(MakeCharacter());

            bool forcedPass = slot.CurrentCard != null;

            Assert.IsTrue(forcedPass);
        }

        [Test]
        public void スロットが空のプレイヤーは強制パスにならない()
        {
            CharacterSlotView slot = new CharacterSlotView();

            bool forcedPass = slot.CurrentCard != null;

            Assert.IsFalse(forcedPass);
        }

        [Test]
        public void 元々空だったスロットに新たにカードが置かれた場合のみコスト払い対象になる()
        {
            CharacterSlotView slot = new CharacterSlotView();
            bool hadChar = slot.CurrentCard != null; // false

            slot.PlaceCard(MakeCharacter());

            bool shouldPayCost = !hadChar && slot.CurrentCard != null;
            Assert.IsTrue(shouldPayCost);
        }

        [Test]
        public void 元々キャラがいたスロットはコスト払い対象にならない()
        {
            CharacterSlotView slot = new CharacterSlotView();
            slot.PlaceCard(MakeCharacter());
            bool hadChar = slot.CurrentCard != null; // true（フェーズ開始前にキャラあり）

            // 強制パスなので新たな PlaceCard は呼ばれないが、条件式の確認として
            bool shouldPayCost = !hadChar && slot.CurrentCard != null;
            Assert.IsFalse(shouldPayCost);
        }
    }
}
