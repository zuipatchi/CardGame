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
        private readonly Label _atkLabel;
        private readonly Label _defLabel;
        private CardDragManipulator _dragManipulator;
        public bool IsFaceDown { get; private set; }
        public CardData Data { get; }
        public CardState State { get; private set; }

        public CardView(VisualTreeAsset template, CardData data, Texture2D backImage = null, bool faceDown = false)
        {
            Data = data;
            template.CloneTree(this);
            _cardRoot = this.Q<VisualElement>("CardRoot");
            _frontFace = this.Q<VisualElement>("FrontFace");
            _backFace = this.Q<VisualElement>("BackFace");
            _imageArea = this.Q<VisualElement>("ImageArea");
            _costLabel = this.Q<Label>("CostLabel");
            _nameLabel = this.Q<Label>("NameLabel");
            _atkArea = this.Q<VisualElement>(className: "game-card__atk-area");
            _defArea = this.Q<VisualElement>(className: "game-card__def-area");
            _atkLabel = this.Q<Label>("AtkLabel");
            _defLabel = this.Q<Label>("DefLabel");

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
            }
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
            _cardRoot.EnableInClassList("game-card--ready", state == CardState.Ready);
            _cardRoot.EnableInClassList("game-card--resolve", state == CardState.Resolve);
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

        private void Bind(CardData data)
        {
            _costLabel.text = data.Cost.ToString();
            _nameLabel.text = data.CardName;
            _atkLabel.text = data.Attack.ToString();
            _defLabel.text = data.Defense.ToString();

            _atkArea.style.display = data is CharacterCardData or EventCardData
                ? DisplayStyle.None : DisplayStyle.Flex;
            _defArea.style.display = data is SkillCardData or EventCardData
                ? DisplayStyle.None : DisplayStyle.Flex;

            if (data.Image != null)
            {
                _imageArea.style.backgroundImage = new StyleBackground(data.Image);
            }
        }
    }
}
