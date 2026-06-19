using DG.Tweening;
using UnityEngine.UIElements;

namespace Main.Card
{
    // 自分の手番の残り時間表示（「TIME」キャプション + 残り秒数）。自分のメインフェーズ中のみ表示し、
    // 毎秒 SetRemaining で更新する。残り時間が少なくなったら SetWarning(true) で赤く警告表示にする。
    public sealed class TurnTimerView : VisualElement
    {
        private readonly Label _label;
        private Tween _shakeTween;

        public TurnTimerView()
        {
            AddToClassList("turn-timer");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            Label caption = new Label("TIME");
            caption.AddToClassList("turn-timer-caption");
            caption.pickingMode = PickingMode.Ignore;
            Add(caption);

            _label = new Label("0");
            _label.AddToClassList("turn-timer-label");
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
        }

        public void SetRemaining(int seconds)
        {
            _label.text = seconds.ToString();
        }

        public void SetWarning(bool isWarning)
        {
            if (isWarning)
            {
                _label.AddToClassList("turn-timer-label--warning");
            }
            else
            {
                _label.RemoveFromClassList("turn-timer-label--warning");
            }
        }

        // 残り時間が減ったときに数字を左右に揺らす（残り少の警告中、毎秒呼ぶ）。
        // 拡大（warning の scale）とは独立した translate で表現する。
        public void Shake()
        {
            _shakeTween?.Kill();
            float offset = 0f;
            _shakeTween = DOTween.Sequence()
                .Append(ShakeStep(7f))
                .Append(ShakeStep(-6f))
                .Append(ShakeStep(3f))
                .Append(ShakeStep(0f))
                .OnComplete(() => _label.style.translate = new StyleTranslate(new Translate(0f, 0f)));

            Tween ShakeStep(float target)
            {
                return DOTween.To(() => offset, v =>
                {
                    offset = v;
                    _label.style.translate = new StyleTranslate(new Translate(v, 0f));
                }, target, 0.05f).SetEase(Ease.InOutSine);
            }
        }

        public void Show()
        {
            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _shakeTween?.Kill();
            _label.style.translate = new StyleTranslate(new Translate(0f, 0f));
            style.display = DisplayStyle.None;
        }
    }
}
