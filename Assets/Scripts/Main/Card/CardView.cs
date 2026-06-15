using System;
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
        private readonly VisualElement _hpArea;
        private readonly Label _atkLabel;
        private readonly Label _hpLabel;
        private readonly VisualElement _guardianIcon;
        private readonly VisualElement _hasteIcon;
        private readonly VisualElement _flyingIcon;
        private int _currentHp;
        // 実行時バフ（BuffAttackByKeyword / BuffHpByKeyword）。攻撃力・最大HPへの加算量。
        private int _attackBuff;
        private int _hpBuff;
        private CardDragManipulator _dragManipulator;
        public bool IsFaceDown { get; private set; }
        public bool IsOpponent { get; private set; }
        public CardData Data { get; }
        public CardState State { get; private set; }
        public int CurrentHp => _currentHp;
        // バフを含む現在の攻撃力（戦闘ダメージ・CPU判断・対象選択はこの値を使う）。
        public int CurrentAttack => Data.Attack + _attackBuff;
        // バフを含む最大HP（回復のクランプ上限）。
        public int MaxHp => Data.Hp + _hpBuff;

        public CardView(VisualTreeAsset template, CardData data, Texture2D backImage = null, bool faceDown = false, bool isOpponent = false)
        {
            Data = data;
            IsOpponent = isOpponent;
            template.CloneTree(this);
            _cardRoot = this.Q<VisualElement>("CardRoot");
            _frontFace = this.Q<VisualElement>("FrontFace");
            _backFace = this.Q<VisualElement>("BackFace");
            _imageArea = this.Q<VisualElement>("ImageArea");
            _costLabel = this.Q<Label>("CostLabel");
            _nameLabel = this.Q<Label>("NameLabel");
            _atkArea = this.Q<VisualElement>(className: "game-card__atk-area");
            _hpArea = this.Q<VisualElement>(className: "game-card__hp-area");
            _atkLabel = this.Q<Label>("AtkLabel");
            _hpLabel = this.Q<Label>("HpLabel");
            _guardianIcon = this.Q<VisualElement>("GuardianIcon");
            _hasteIcon = this.Q<VisualElement>("HasteIcon");
            _flyingIcon = this.Q<VisualElement>("FlyingIcon");

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
            _cardRoot.EnableInClassList("game-card--attr-red", visible && Data.Attribute == CardAttribute.Red);
            _cardRoot.EnableInClassList("game-card--attr-blue", visible && Data.Attribute == CardAttribute.Blue);
            _cardRoot.EnableInClassList("game-card--attr-green", visible && Data.Attribute == CardAttribute.Green);
            _cardRoot.EnableInClassList("game-card--attr-yellow", visible && Data.Attribute == CardAttribute.Yellow);
            _cardRoot.EnableInClassList("game-card--attr-black", visible && Data.Attribute == CardAttribute.Black);
            _cardRoot.EnableInClassList("game-card--attr-purple", visible && Data.Attribute == CardAttribute.Purple);
            _cardRoot.EnableInClassList("game-card--attr-white", visible && Data.Attribute == CardAttribute.White);
        }

        private static Color GetAttributeColor(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => new Color(0.86f, 0.24f, 0.24f, 1f),
                CardAttribute.Blue => new Color(0.27f, 0.51f, 0.90f, 1f),
                CardAttribute.Green => new Color(0.24f, 0.78f, 0.31f, 1f),
                CardAttribute.Yellow => new Color(0.86f, 0.75f, 0.20f, 1f),
                CardAttribute.Black => new Color(0.39f, 0.39f, 0.47f, 1f),
                CardAttribute.Purple => new Color(0.67f, 0.31f, 0.86f, 1f),
                CardAttribute.White => new Color(0.82f, 0.82f, 0.88f, 1f),
                _ => new Color(0.82f, 0.82f, 0.88f, 1f)
            };
        }

        public void TakeDamage(int damage)
        {
            _currentHp -= damage;
            _hpLabel.text = Mathf.Max(0, _currentHp).ToString();
        }

        public async UniTask TakeDamageAsync(int damage, CancellationToken ct)
        {
            float scaleVal = 1f;
            float colorT = 1f;
            float shakeX = 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                // HP アイコン拡大 + 赤く + カード右へ
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; _hpArea.style.scale = new Scale(new Vector3(v, v, 1f)); }, 2.2f, 0.07f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => colorT, v => { colorT = v; _hpLabel.style.color = new StyleColor(Color.Lerp(Color.red, Color.white, v)); }, 0f, 0.07f))
                .Join(DOTween.To(() => shakeX, v => { shakeX = v; _cardRoot.style.translate = new StyleTranslate(new Translate(v, 0f)); }, 14f, 0.07f).SetEase(Ease.OutQuad))
                // シェイク（左→右→中央）
                .Append(DOTween.To(() => shakeX, v => { shakeX = v; _cardRoot.style.translate = new StyleTranslate(new Translate(v, 0f)); }, -12f, 0.07f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(() => shakeX, v => { shakeX = v; _cardRoot.style.translate = new StyleTranslate(new Translate(v, 0f)); }, 7f, 0.06f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(() => shakeX, v => { shakeX = v; _cardRoot.style.translate = new StyleTranslate(new Translate(v, 0f)); }, 0f, 0.06f).SetEase(Ease.InOutSine))
                // HP アイコン戻る + 色戻る（シェイク最後のステップに相乗り）
                .Join(DOTween.To(() => scaleVal, v => { scaleVal = v; _hpArea.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.28f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => colorT, v => { colorT = v; _hpLabel.style.color = new StyleColor(Color.Lerp(Color.red, Color.white, v)); }, 1f, 0.42f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            _hpArea.style.scale = new Scale(Vector3.one);
            _hpLabel.style.color = StyleKeyword.Null;
            _cardRoot.style.translate = new StyleTranslate(new Translate(0f, 0f));

            _currentHp -= damage;
            _hpLabel.text = Mathf.Max(0, _currentHp).ToString();
        }

        // HP を amount 回復する（最大HP = Data.Hp でクランプ）。amount <= 0 は全回復扱い。
        // 回復量が 0（既に満タン等）なら演出せず即終了する。緑のパルス演出付き。
        public async UniTask HealAsync(int amount, CancellationToken ct)
        {
            int maxHp = MaxHp;
            int healed = amount <= 0 ? maxHp : Mathf.Min(maxHp, _currentHp + amount);
            if (healed <= _currentHp)
            {
                return;
            }

            _currentHp = healed;
            _hpLabel.text = _currentHp.ToString();

            float scaleVal = 1f;
            float colorT = 0f;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Color healColor = new Color(0.24f, 0.78f, 0.31f, 1f);
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; _hpArea.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1.8f, 0.12f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => colorT, v => { colorT = v; _hpLabel.style.color = new StyleColor(Color.Lerp(Color.white, healColor, v)); }, 1f, 0.12f))
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; _hpArea.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.30f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => colorT, v => { colorT = v; _hpLabel.style.color = new StyleColor(Color.Lerp(Color.white, healColor, v)); }, 0f, 0.40f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            _hpArea.style.scale = new Scale(Vector3.one);
            _hpLabel.style.color = StyleKeyword.Null;
        }

        public void ResetCurrentHp()
        {
            _currentHp = Data.Hp;
            _hpLabel.text = _currentHp.ToString();
        }

        // 攻撃力を amount 永続的に上げる（BuffAttackByKeyword）。ATK ラベルを更新しオレンジのパルス演出を出す。
        public async UniTask BuffAttackAsync(int amount, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }
            _attackBuff += amount;
            _atkLabel.text = CurrentAttack.ToString();
            await PulseStatAsync(_atkArea, _atkLabel, new Color(1f, 0.65f, 0.1f), ct);
        }

        // HP（現在HP・最大HP）を amount 永続的に上げる（BuffHpByKeyword）。HP ラベルを更新し緑のパルス演出を出す。
        public async UniTask BuffHpAsync(int amount, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }
            _hpBuff += amount;
            _currentHp += amount;
            _hpLabel.text = _currentHp.ToString();
            await PulseStatAsync(_hpArea, _hpLabel, new Color(0.24f, 0.78f, 0.31f), ct);
        }

        // ステータス（ATK/HP）の値が上がったときの共通パルス演出：area を拡大し、label の色を color に寄せて戻す。
        private async UniTask PulseStatAsync(VisualElement area, Label label, Color color, CancellationToken ct)
        {
            float scaleVal = 1f;
            float colorT = 0f;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; area.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1.8f, 0.12f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => colorT, v => { colorT = v; label.style.color = new StyleColor(Color.Lerp(Color.white, color, v)); }, 1f, 0.12f))
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; area.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.30f).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => colorT, v => { colorT = v; label.style.color = new StyleColor(Color.Lerp(Color.white, color, v)); }, 0f, 0.40f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            area.style.scale = new Scale(Vector3.one);
            label.style.color = StyleKeyword.Null;
        }

        private void Bind(CardData data)
        {
            _currentHp = data.Hp;
            _costLabel.text = data.Cost.ToString();
            _nameLabel.text = data.CardName;
            _atkLabel.text = CurrentAttack.ToString();
            _hpLabel.text = _currentHp.ToString();

            _atkArea.style.display = data is EventCardData
                ? DisplayStyle.None : DisplayStyle.Flex;
            _hpArea.style.display = data is CharacterCardData
                ? DisplayStyle.Flex : DisplayStyle.None;
            _guardianIcon.style.display = data is CharacterCardData characterData && characterData.Guardian
                ? DisplayStyle.Flex : DisplayStyle.None;
            _hasteIcon.style.display = data is CharacterCardData hasteData && hasteData.Haste
                ? DisplayStyle.Flex : DisplayStyle.None;
            _flyingIcon.style.display = data is CharacterCardData flyingData && flyingData.Flying
                ? DisplayStyle.Flex : DisplayStyle.None;

            if (data.Image != null)
            {
                _imageArea.style.backgroundImage = new StyleBackground(data.Image);
            }
        }
    }
}
