using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CardDetailModal
    {
        // キーワード能力の効果説明文（タップで展開）。ダメージトリガーはキャラ/イベント両方で使うため定数化する。
        private const string TriggerOnGraveDesc = "デッキから墓地へ送られると、コストを払わず即発動する";

        private readonly VisualElement _root;
        private VisualElement _overlay;
        private Label _keywordDescLabel;
        private VisualElement _activeKeywordBlock;

        public CardDetailModal(VisualElement root)
        {
            _root = root;
        }

        public void Show(CardData data)
        {
            Show(data, null, null);
        }

        // actionLabel を渡すと、パネル下部に任意のアクションボタン（押すと onAction(data) → 閉じる）を表示する。
        // デッキ構築の「デッキのシンボルに設定」などに使う。未指定なら従来どおりボタンなし。
        public void Show(CardData data, string actionLabel, Action<CardData> onAction)
        {
            Hide();
            _activeKeywordBlock = null;

            _overlay = new VisualElement();
            _overlay.AddToClassList("card-detail-overlay");
            // 外側クリックで閉じる。ClickEvent（down/up が同一要素で合成される高レベルイベント）は
            // オンライン相手ターン中のフレームヒッチで合成されず無反応になることがあるため、
            // より確実に発火する PointerDownEvent で閉じる。
            _overlay.RegisterCallback<PointerDownEvent>(_ => Hide());

            VisualElement panel = new VisualElement();
            panel.AddToClassList("card-detail-panel");
            // パネル内のクリックでは閉じない（オーバーレイへ伝播させない）。
            panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            VisualElement imageArea = new VisualElement();
            imageArea.AddToClassList("card-detail-image");
            if (data.Image != null)
            {
                imageArea.style.backgroundImage = new StyleBackground(data.Image);
            }

            imageArea.AddToClassList(GetAttributeImageClass(data.Attribute));
            panel.Add(imageArea);

            VisualElement stats = new VisualElement();
            stats.AddToClassList("card-detail-stats");

            VisualElement headerRow = new VisualElement();
            headerRow.AddToClassList("card-detail-header-row");

            string typeName = data is CharacterCardData ? "CHARACTER" : "EVENT";
            string typeClass = data is CharacterCardData ? "card-detail-type--character" :
                               "card-detail-type--event";
            Label typeLabel = new Label(typeName);
            typeLabel.AddToClassList("card-detail-type");
            typeLabel.AddToClassList(typeClass);
            headerRow.Add(typeLabel);

            headerRow.Add(CreateAttributeBadge(data.Attribute));
            stats.Add(headerRow);

            Label nameLabel = new Label(data.CardName);
            nameLabel.AddToClassList("card-detail-name");
            stats.Add(nameLabel);

            // 特徴（キーワード）。設定されているカードのみ、名前の下にタグ表示する。
            if (!string.IsNullOrEmpty(data.Keyword))
            {
                Label keywordLabel = new Label(data.Keyword);
                keywordLabel.AddToClassList("card-detail-keyword");
                stats.Add(keywordLabel);
            }

            VisualElement divider = new VisualElement();
            divider.AddToClassList("card-detail-divider");
            stats.Add(divider);

            List<VisualElement> statBlocks = new List<VisualElement>();
            statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--cost", data.Cost.ToString(), "コスト"));

            if (data is CharacterCardData charData)
            {
                statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--atk", charData.Attack.ToString(), "攻撃力"));
                statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--hp", charData.Hp.ToString(), "体力"));
                if (charData.Guardian)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--guardian", "守護",
                        "守護を持つキャラを優先して攻撃しなければならない"));
                }
                if (charData.Haste)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--haste", "速攻",
                        "場に出たターンから攻撃できる（召喚酔いしない）"));
                }
                if (charData.Flying)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--flying", "飛行",
                        "飛行・防人からしか攻撃されず、守護を無視して攻撃できる"));
                }
                if (charData.Sakimori)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--sakimori", "防人",
                        "飛行を持つキャラは防人を優先して攻撃しなければならない。防人は飛行を持つキャラに攻撃できる"));
                }
                if (charData.Assault)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--assault", "強襲",
                        "タップしていない相手キャラにも攻撃できる"));
                }
                if (charData.NoDeckAttack)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--no-deck-attack", "デッキ攻撃×",
                        "相手デッキを直接攻撃できない"));
                }
                if (charData.Archer)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--archer", "射手",
                        "飛行を持つ相手キャラに攻撃できる"));
                }
                if (charData.TriggerOnGrave)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--trigger-on-grave", "ダメージトリガー",
                        TriggerOnGraveDesc));
                }
                if (!string.IsNullOrEmpty(charData.Description))
                {
                    Label descLabel = new Label(charData.Description);
                    descLabel.AddToClassList("card-detail-description");
                    stats.Add(descLabel);
                }
            }
            else if (data is EventCardData eventData)
            {
                if (eventData.TriggerOnGrave)
                {
                    statBlocks.Add(CreateKeywordStatBlock("card-detail-stat-icon--trigger-on-grave", "ダメージトリガー",
                        TriggerOnGraveDesc));
                }
                if (!string.IsNullOrEmpty(eventData.Description))
                {
                    Label descLabel = new Label(eventData.Description);
                    descLabel.AddToClassList("card-detail-description");
                    stats.Add(descLabel);
                }
            }

            // 2列の明示的な行コンテナで組む（flex-wrap は折り返し時に高さを正しく報告せず、
            // 下に置いたフレーバーテキストと重なるため使用しない）
            VisualElement statsGrid = new VisualElement();
            statsGrid.AddToClassList("card-detail-stats-grid");
            for (int i = 0; i < statBlocks.Count; i += 2)
            {
                VisualElement gridRow = new VisualElement();
                gridRow.AddToClassList("card-detail-stats-row");
                gridRow.Add(statBlocks[i]);
                if (i + 1 < statBlocks.Count)
                {
                    gridRow.Add(statBlocks[i + 1]);
                }
                statsGrid.Add(gridRow);
            }
            stats.Add(statsGrid);

            // キーワードアイコンをタップしたときに効果説明を表示する共有ラベル（初期は非表示）。
            _keywordDescLabel = new Label();
            _keywordDescLabel.AddToClassList("card-detail-keyword-desc");
            _keywordDescLabel.AddToClassList("card-detail-keyword-desc--hidden");
            stats.Add(_keywordDescLabel);

            if (!string.IsNullOrEmpty(data.FlavorText))
            {
                Label flavorLabel = new Label(data.FlavorText);
                flavorLabel.AddToClassList("card-detail-flavor");
                stats.Add(flavorLabel);
            }

            if (!string.IsNullOrEmpty(actionLabel) && onAction != null)
            {
                Button actionButton = new Button();
                actionButton.text = actionLabel;
                actionButton.AddToClassList("card-detail-action");
                actionButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                actionButton.clicked += () =>
                {
                    onAction(data);
                    Hide();
                };
                stats.Add(actionButton);
            }

            panel.Add(stats);

            // 右上の ✕ 閉じるボタン（外側クリックに頼らない確実な閉じ導線）。
            // UI Toolkit は後から追加した兄弟が上に描画されるため、画像・スタットを追加した後に
            // 最後に追加して最前面に置く（先頭に追加するとラベル類に隠れてホバー/クリックを拾えない）。
            Button closeButton = new Button();
            closeButton.text = "✕";
            closeButton.AddToClassList("card-detail-close");
            closeButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            closeButton.clicked += () => Hide();
            panel.Add(closeButton);

            _overlay.Add(panel);
            _root.Add(_overlay);
        }

        public void Hide()
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.RemoveFromHierarchy();
            _overlay = null;
        }

        private static VisualElement CreateAttributeBadge(CardAttribute attribute)
        {
            VisualElement icon = new VisualElement();
            icon.AddToClassList("card-detail-attr-icon");
            icon.AddToClassList(GetAttributeIconClass(attribute));
            return icon;
        }

        private static string GetAttributeIconClass(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "card-detail-attr-icon--red",
                CardAttribute.Blue => "card-detail-attr-icon--blue",
                CardAttribute.Green => "card-detail-attr-icon--green",
                CardAttribute.Yellow => "card-detail-attr-icon--yellow",
                CardAttribute.Black => "card-detail-attr-icon--black",
                CardAttribute.Purple => "card-detail-attr-icon--purple",
                CardAttribute.White => "card-detail-attr-icon--white",
                _ => "card-detail-attr-icon--white"
            };
        }

        private static string GetAttributeImageClass(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "card-detail-image--attr-red",
                CardAttribute.Blue => "card-detail-image--attr-blue",
                CardAttribute.Green => "card-detail-image--attr-green",
                CardAttribute.Yellow => "card-detail-image--attr-yellow",
                CardAttribute.Black => "card-detail-image--attr-black",
                CardAttribute.Purple => "card-detail-image--attr-purple",
                CardAttribute.White => "card-detail-image--attr-white",
                _ => "card-detail-image--attr-white"
            };
        }

        // タップで効果説明を展開できるキーワード能力用のスタットブロックを作る。
        private VisualElement CreateKeywordStatBlock(string iconClass, string labelText, string description)
        {
            VisualElement block = CreateIconStatBlock(iconClass, string.Empty, labelText);
            block.AddToClassList("card-detail-stat-block--keyword");
            block.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleKeywordDesc(block, description);
            });
            return block;
        }

        // キーワードアイコンのタップに応じて説明を開閉する。同じ枠を再タップで閉じ、別の枠をタップで切り替える。
        private void ToggleKeywordDesc(VisualElement block, string description)
        {
            if (_activeKeywordBlock == block)
            {
                block.RemoveFromClassList("card-detail-stat-block--active");
                _activeKeywordBlock = null;
                _keywordDescLabel.AddToClassList("card-detail-keyword-desc--hidden");
                return;
            }

            if (_activeKeywordBlock != null)
            {
                _activeKeywordBlock.RemoveFromClassList("card-detail-stat-block--active");
            }

            _activeKeywordBlock = block;
            block.AddToClassList("card-detail-stat-block--active");
            _keywordDescLabel.text = description;
            _keywordDescLabel.RemoveFromClassList("card-detail-keyword-desc--hidden");
        }

        private static VisualElement CreateIconStatBlock(string iconClass, string valueText, string labelText)
        {
            VisualElement block = new VisualElement();
            block.AddToClassList("card-detail-stat-block");

            Label label = new Label(labelText);
            label.AddToClassList("card-detail-row-label");
            block.Add(label);

            VisualElement row = new VisualElement();
            row.AddToClassList("card-detail-row");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("card-detail-stat-icon");
            icon.AddToClassList(iconClass);

            Label value = new Label(valueText);
            value.AddToClassList("card-detail-row-value");

            row.Add(icon);
            row.Add(value);
            block.Add(row);
            return block;
        }

    }
}
