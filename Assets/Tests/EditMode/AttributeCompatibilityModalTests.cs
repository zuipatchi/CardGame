using System.Collections.Generic;
using Main.Card;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    public sealed class AttributeCompatibilityModalTests
    {
        private VisualElement _root;
        private AttributeCompatibilityModal _modal;

        [SetUp]
        public void SetUp()
        {
            _root = new VisualElement();
            _modal = new AttributeCompatibilityModal(_root, null);
        }

        [Test]
        public void Showを呼ぶとオーバーレイが追加される()
        {
            _modal.Show();

            Assert.AreEqual(1, _root.childCount);
            VisualElement overlay = _root.Q<VisualElement>(className: "attr-compatibility-overlay");
            Assert.IsNotNull(overlay);
        }

        [Test]
        public void Hideを呼ぶとオーバーレイが削除される()
        {
            _modal.Show();

            _modal.Hide();

            Assert.AreEqual(0, _root.childCount);
        }

        [Test]
        public void テーブルにFire_Poison_Patchiの3行が含まれる()
        {
            _modal.Show();

            List<VisualElement> rows = new List<VisualElement>();
            VisualElement panel = _root.Q<VisualElement>(className: "attr-compatibility-panel");
            panel.Query<VisualElement>(className: "attr-compatibility-row").ToList(rows);

            // ヘッダー行 + データ3行 = 4行
            Assert.AreEqual(4, rows.Count);
        }

        [Test]
        public void 各データ行にセルが3つある()
        {
            _modal.Show();

            List<VisualElement> rows = new List<VisualElement>();
            VisualElement panel = _root.Q<VisualElement>(className: "attr-compatibility-panel");
            panel.Query<VisualElement>(className: "attr-compatibility-row").ToList(rows);

            // インデックス 0 はヘッダー行なのでスキップしてデータ行を確認
            for (int i = 1; i < rows.Count; i++)
            {
                List<VisualElement> cells = new List<VisualElement>();
                rows[i].Query<VisualElement>(className: "attr-compatibility-cell").ToList(cells);
                Assert.AreEqual(3, cells.Count, $"行 {i} のセル数が3つでない");
            }
        }

        [Test]
        public void Showを2回呼ぶとオーバーレイは1つだけ存在する()
        {
            _modal.Show();
            _modal.Show();

            Assert.AreEqual(1, _root.childCount);
        }
    }
}
