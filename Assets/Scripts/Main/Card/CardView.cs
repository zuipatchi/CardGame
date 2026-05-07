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
        private readonly Label _costLabel;
        private readonly Label _nameLabel;
        private readonly Label _effectLabel;
        private readonly Label _atkLabel;
        private readonly Label _defLabel;
        public bool IsFaceDown { get; private set; }

        public CardView(VisualTreeAsset template, CardData data, Texture2D backImage = null, bool faceDown = false)
        {
            template.CloneTree(this);
            _cardRoot = this.Q<VisualElement>("CardRoot");
            _frontFace = this.Q<VisualElement>("FrontFace");
            _backFace = this.Q<VisualElement>("BackFace");
            _costLabel = this.Q<Label>("CostLabel");
            _nameLabel = this.Q<Label>("NameLabel");
            _effectLabel = this.Q<Label>("EffectLabel");
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
            _effectLabel.text = data.EffectText;
            _atkLabel.text = $"ATK {data.Attack}";
            _defLabel.text = $"DEF {data.Defense}";
        }
    }
}
