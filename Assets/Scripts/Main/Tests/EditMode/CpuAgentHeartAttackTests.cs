using System.Collections.Generic;
using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Main.Tests.EditMode
{
    public sealed class CpuAgentHeartAttackTests
    {
        [Test]
        public void 最高ATKのキャラがハート攻撃者として選ばれる()
        {
            List<CardData> ownChars = new List<CardData>
            {
                new CharacterCardData("C901", "弱いキャラ", 0, 1, 1, CardAttribute.Red),
                new CharacterCardData("C902", "強いキャラ", 0, 5, 1, CardAttribute.Blue),
                new CharacterCardData("C903", "中くらいのキャラ", 0, 3, 1, CardAttribute.Green)
            };

            int result = CpuAgent.ChooseHeartAttacker(ownChars);

            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void ATK0のキャラしかいない場合は攻撃者が選ばれない()
        {
            List<CardData> ownChars = new List<CardData>
            {
                new CharacterCardData("C901", "ATK0キャラA", 0, 0, 1, CardAttribute.Red),
                new CharacterCardData("C902", "ATK0キャラB", 0, 0, 1, CardAttribute.Blue)
            };

            int result = CpuAgent.ChooseHeartAttacker(ownChars);

            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void キャラがいない場合は攻撃者が選ばれない()
        {
            List<CardData> ownChars = new List<CardData>();

            int result = CpuAgent.ChooseHeartAttacker(ownChars);

            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void 同ATKの場合は先頭のキャラが選ばれる()
        {
            List<CardData> ownChars = new List<CardData>
            {
                new CharacterCardData("C901", "キャラA", 0, 3, 1, CardAttribute.Red),
                new CharacterCardData("C902", "キャラB", 0, 3, 1, CardAttribute.Blue)
            };

            int result = CpuAgent.ChooseHeartAttacker(ownChars);

            Assert.That(result, Is.EqualTo(0));
        }
    }
}
