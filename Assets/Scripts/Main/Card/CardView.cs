using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CardView : VisualElement
    {
        private readonly VisualElement _cardRoot;
        private readonly VisualElement _frontFace;
        private readonly VisualElement _backFace;
        private readonly VisualElement _imageArea;
        private readonly Label _costLabel;
        private readonly Label _nameLabel;
        private readonly VisualElement _atkArea;
        private readonly VisualElement _defArea;
        private readonly VisualElement _spdArea;
        private readonly VisualElement _hpArea;
        private readonly Label _atkLabel;
        private readonly Label _defLabel;
        private readonly Label _spdLabel;
        private readonly Label _hpLabel;
        private readonly Label _chainLabel;
        private readonly VisualElement _attributeIcon;
        private readonly AttributeDatabaseSO _attrIconDb;
        private CardDragManipulator _dragManipulator;
        private Tween _playableHighlightTween;
        public bool IsFaceDown { get; private set; }
        public bool IsOpponent { get; private set; }
        public CardData Data { get; }
        public CardState State { get; private set; }

        public CardView(VisualTreeAsset template, CardData data, Texture2D backImage = null, bool faceDown = false, AttributeDatabaseSO attrIconDb = null, bool isOpponent = false)
        {
            Data = data;
            IsOpponent = isOpponent;
            _attrIconDb = attrIconDb;
            template.CloneTree(this);
            _cardRoot = this.Q<VisualElement>("CardRoot");
            _frontFace = this.Q<VisualElement>("FrontFace");
            _backFace = this.Q<VisualElement>("BackFace");
            _imageArea = this.Q<VisualElement>("ImageArea");
            _costLabel = this.Q<Label>("CostLabel");
            _nameLabel = this.Q<Label>("NameLabel");
            _atkArea = this.Q<VisualElement>(className: "game-card__atk-area");
            _defArea = this.Q<VisualElement>(className: "game-card__def-area");
            _spdArea = this.Q<VisualElement>(className: "game-card__spd-area");
            _hpArea = this.Q<VisualElement>(className: "game-card__hp-area");
            _atkLabel = this.Q<Label>("AtkLabel");
            _defLabel = this.Q<Label>("DefLabel");
            _spdLabel = this.Q<Label>("SpdLabel");
            _hpLabel = this.Q<Label>("HpLabel");
            _chainLabel = this.Q<Label>("ChainLabel");
            _attributeIcon = this.Q<VisualElement>("AttributeIcon");

            _cardRoot.style.scale = new Scale(Vector3.one);

            Bind(data);

            if (backImage != null)
            {
                SetBackImage(backImage);
            }

            if (faceDown)
            {
                IsFaceDown = true;
                _frontFace.style.display = DisplayStyle.None;
                _backFace.style.display = DisplayStyle.Flex;
                _imageArea.style.display = DisplayStyle.None;
                ApplyTypeFrame(false);
            }
            else
            {
                ApplyTypeFrame(true);
            }
        }

        public void SetPlayableHighlight(bool playable)
        {
            _playableHighlightTween?.Kill();
            _playableHighlightTween = null;
            _cardRoot.style.borderTopColor = StyleKeyword.Null;
            _cardRoot.style.borderRightColor = StyleKeyword.Null;
            _cardRoot.style.borderBottomColor = StyleKeyword.Null;
            _cardRoot.style.borderLeftColor = StyleKeyword.Null;
            if (!playable)
            {
                return;
            }

            Color typeColor = Data is CharacterCardData
                ? new Color(0.274f, 0.510f, 0.902f, 1f)
                : Data is SkillCardData
                    ? new Color(0.863f, 0.235f, 0.235f, 1f)
                    : new Color(0.824f, 0.725f, 0.196f, 1f);

            float t = 0f;
            _playableHighlightTween = DOTween.To(
                () => t,
                v =>
                {
                    t = v;
                    Color c = Color.Lerp(typeColor, Color.white, v);
                    _cardRoot.style.borderTopColor = new StyleColor(c);
                    _cardRoot.style.borderRightColor = new StyleColor(c);
                    _cardRoot.style.borderBottomColor = new StyleColor(c);
                    _cardRoot.style.borderLeftColor = new StyleColor(c);
                },
                1f,
                0.5f
            ).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        public void AttachDragManipulator(CardDragManipulator manipulator)
        {
            _dragManipulator = manipulator;
            this.AddManipulator(manipulator);
        }

        public void RemoveDragManipulator()
        {
            if (_dragManipulator == null)
            {
                return;
            }

            this.RemoveManipulator(_dragManipulator);
            _dragManipulator = null;
        }

        public void SetState(CardState state)
        {
            State = state;
            _cardRoot.EnableInClassList("game-card--resolve", state == CardState.Resolve);
        }

        public void FaceUp()
        {
            if (!IsFaceDown)
            {
                return;
            }

            IsFaceDown = false;
            _frontFace.style.display = DisplayStyle.Flex;
            _backFace.style.display = DisplayStyle.None;
            _imageArea.style.display = DisplayStyle.Flex;
            ApplyTypeFrame(true);
        }

        public void SetChainNumber(int number)
        {
            if (number <= 0)
            {
                _chainLabel.style.display = DisplayStyle.None;
                return;
            }

            _chainLabel.text = number is >= 1 and <= 9
                ? ((char)(0x2460 + number - 1)).ToString()
                : number.ToString();
            _chainLabel.style.display = DisplayStyle.Flex;
        }

        public void SetBackImage(Texture2D texture)
        {
            _backFace.style.backgroundImage = new StyleBackground(texture);
        }

        public async UniTask FlipAsync(CancellationToken cancellation = default)
        {
            await AnimateScaleXAsync(0f, 0.15f, Ease.InQuad, cancellation);

            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            IsFaceDown = !IsFaceDown;
            _frontFace.style.display = IsFaceDown ? DisplayStyle.None : DisplayStyle.Flex;
            _backFace.style.display = IsFaceDown ? DisplayStyle.Flex : DisplayStyle.None;
            _imageArea.style.display = IsFaceDown ? DisplayStyle.None : DisplayStyle.Flex;
            ApplyTypeFrame(!IsFaceDown);

            await AnimateScaleXAsync(1f, 0.15f, Ease.OutQuad, cancellation);
        }

        private async UniTask AnimateScaleXAsync(float targetX, float duration, Ease ease, CancellationToken cancellation)
        {
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Tween tween = DOTween.To(
                () => _cardRoot.style.scale.value.value.x,
                x => _cardRoot.style.scale = new Scale(new Vector3(x, 1f, 1f)),
                targetX,
                duration
            ).SetEase(ease).OnComplete(() => tcs.TrySetResult());

            cancellation.Register(() =>
            {
                tween.Kill();
                tcs.TrySetCanceled();
            });

            try
            {
                await tcs.Task;
            }
            catch (System.OperationCanceledException) { }
        }

        private void ApplyTypeFrame(bool visible)
        {
            _cardRoot.EnableInClassList("game-card--character", visible && Data is CharacterCardData);
            _cardRoot.EnableInClassList("game-card--skill", visible && Data is SkillCardData);
            _cardRoot.EnableInClassList("game-card--event", visible && Data is EventCardData);
        }

        private void Bind(CardData data)
        {
            _costLabel.text = data.Cost.ToString();
            _nameLabel.text = data.CardName;
            _atkLabel.text = data.Attack.ToString();
            _defLabel.text = data.Defense.ToString();
            _spdLabel.text = data.Speed.ToString();
            _hpLabel.text = data.Hp.ToString();

            _atkArea.style.display = data is CharacterCardData or EventCardData
                ? DisplayStyle.None : DisplayStyle.Flex;
            _defArea.style.display = DisplayStyle.None;
            _spdArea.style.display = data is CharacterCardData
                ? DisplayStyle.Flex : DisplayStyle.None;
            _hpArea.style.display = data is CharacterCardData
                ? DisplayStyle.Flex : DisplayStyle.None;

            if (data.Image != null)
            {
                _imageArea.style.backgroundImage = new StyleBackground(data.Image);
            }

            _attributeIcon.style.display = DisplayStyle.None;
        }
    }
}
