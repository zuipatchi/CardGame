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
        private readonly VisualElement _sakimoriIcon;
        private readonly VisualElement _assaultIcon;
        private readonly VisualElement _noDeckAttackIcon;
        private readonly VisualElement _archerIcon;
        private readonly VisualElement _deadlyIcon;
        private readonly VisualElement _triggerOnGraveIcon;
        private int _currentHp;
        // 実行時バフ（BuffAttackByKeyword / BuffHpByKeyword）。攻撃力・最大HPへの加算量。
        private int _attackBuff;
        private int _hpBuff;
        // 実行時に付与されたキーワード能力（GrantKeyword）。SO 固有のフラグとは別に保持する。
        private bool _grantedGuardian;
        private bool _grantedHaste;
        private bool _grantedFlying;
        private bool _grantedSakimori;
        private bool _grantedAssault;
        private bool _grantedNoDeckAttack;
        private bool _grantedArcher;
        private bool _grantedDeadly;
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

        // キーワード能力（SO 固有 OR 実行時付与）。攻撃ルール判定・アイコン表示はこれを参照する。
        public bool HasGuardian => (Data is CharacterCardData guardianData && guardianData.Guardian) || _grantedGuardian;
        public bool HasHaste => (Data is CharacterCardData hasteData && hasteData.Haste) || _grantedHaste;
        public bool HasFlying => (Data is CharacterCardData flyingData && flyingData.Flying) || _grantedFlying;
        public bool HasSakimori => (Data is CharacterCardData sakimoriData && sakimoriData.Sakimori) || _grantedSakimori;
        public bool HasAssault => (Data is CharacterCardData assaultData && assaultData.Assault) || _grantedAssault;
        public bool HasNoDeckAttack => (Data is CharacterCardData noDeckAttackData && noDeckAttackData.NoDeckAttack) || _grantedNoDeckAttack;
        public bool HasArcher => (Data is CharacterCardData archerData && archerData.Archer) || _grantedArcher;
        public bool HasDeadly => (Data is CharacterCardData deadlyData && deadlyData.Deadly) || _grantedDeadly;

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
            _sakimoriIcon = this.Q<VisualElement>("SakimoriIcon");
            _assaultIcon = this.Q<VisualElement>("AssaultIcon");
            _noDeckAttackIcon = this.Q<VisualElement>("NoDeckAttackIcon");
            _archerIcon = this.Q<VisualElement>("ArcherIcon");
            _deadlyIcon = this.Q<VisualElement>("DeadlyIcon");
            _triggerOnGraveIcon = this.Q<VisualElement>("TriggerOnGraveIcon");

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

        // ─── フィールド配置時のスケール ────────────────────────────────────────
        // フィールド上のカードのスケールは「フィールド枚数による基準スケール」と
        // 「攻撃ハイライト時の拡大倍率」の合成で決まる。両方をインライン style.scale で
        // 個別に指定すると後勝ちで打ち消し合う（USS の scale はインラインに負ける）ため、
        // CardView が両者を保持して合成した値を一度に適用する。
        private const float AttackHighlightScale = 1.04f;
        private float _fieldScale = 1f;
        private bool _attackHighlighted;

        // フィールドの枚数に応じた基準スケールを設定する（FieldView から呼ぶ）。
        public void SetFieldScale(float scale)
        {
            _fieldScale = scale;
            ApplyFieldScale();
        }

        // 攻撃可能／攻撃対象ハイライトの拡大を切り替える（基準スケールに倍率を掛ける）。
        public void SetAttackHighlighted(bool highlighted)
        {
            _attackHighlighted = highlighted;
            ApplyFieldScale();
        }

        // ─── タップ（攻撃・ターン終了時に横向き） ───────────────────────────
        // タップ＝カードを 90° 横に倒した状態。攻撃を宣言したキャラと、ターン終了時の
        // 守護/防人が横向きになる。「タップ状態のキャラにしか攻撃できない」攻撃ルールの
        // 対象判定はこの IsTapped を参照する。回転は内側の _cardRoot にかける
        // （外側の style.rotate は手札の扇・フィールド配置で使われるため不干渉）。
        private const float TappedAngle = 90f;
        public bool IsTapped { get; private set; }

        // 即時にタップ/アンタップへ切り替える（演出なし）。ターン開始時の一括アンタップや
        // バウンスでの状態リセットに使う。
        public void SetTapped(bool tapped)
        {
            IsTapped = tapped;
            _cardRoot.style.rotate = new Rotate(tapped ? TappedAngle : 0f);
            // タップ時はネームラベル・アイコンを逆回転して読める向きに再配置する（USS 側で対応）
            _cardRoot.EnableInClassList("game-card--tapped", tapped);
        }

        // 回転アニメーション付きでタップ/アンタップする（攻撃時・ターン終了時の自動タップ）。
        // 既に同じ状態なら何もしない。
        public async UniTask SetTappedAsync(bool tapped, CancellationToken ct)
        {
            if (IsTapped == tapped)
            {
                return;
            }
            IsTapped = tapped;
            // タップ時はネームラベル・アイコンを逆回転して読める向きに再配置する（USS 側で対応）
            _cardRoot.EnableInClassList("game-card--tapped", tapped);
            float target = tapped ? TappedAngle : 0f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Tween tween = DOTween.To(
                () => _cardRoot.style.rotate.value.angle.value,
                a => _cardRoot.style.rotate = new Rotate(a),
                target,
                0.18f
            ).SetEase(Ease.OutBack).OnComplete(() => tcs.TrySetResult());

            ct.Register(() =>
            {
                tween.Kill();
                tcs.TrySetCanceled();
            });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            _cardRoot.style.rotate = new Rotate(target);
        }

        private void ApplyFieldScale()
        {
            float scale = _fieldScale * (_attackHighlighted ? AttackHighlightScale : 1f);
            style.scale = new Scale(new Vector3(scale, scale, 1f));
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
            // 論理的な表裏状態はアニメーション開始時に即座に確定させる。
            // 見た目のめくり（表示切り替え）は中間点に残すが、IsFaceDown の更新を
            // 中間点まで遅らせると、fire-and-forget でめくっている最中のカード
            // （引いた直後の手札など）が一時的に裏向き扱いとなり、速攻判定などが
            // 誤って弾かれる。そのため状態は先に更新する。
            bool faceDown = !IsFaceDown;
            IsFaceDown = faceDown;

            await AnimateScaleXAsync(0f, 0.15f, Ease.InQuad, cancellation);

            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            _frontFace.style.display = faceDown ? DisplayStyle.None : DisplayStyle.Flex;
            _backFace.style.display = faceDown ? DisplayStyle.Flex : DisplayStyle.None;
            _imageArea.style.display = faceDown ? DisplayStyle.None : DisplayStyle.Flex;
            ApplyTypeFrame(!faceDown);

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

        // 場で受けた実行時の変化（攻撃力・HPバフ、被ダメージ、付与キーワード）をすべて
        // カードデータ固有の初期状態へ戻す。バウンスで手札に戻る際に呼び、場の状態を引き継がせない。
        public void ResetRuntimeState()
        {
            _attackBuff = 0;
            _hpBuff = 0;
            _currentHp = Data.Hp;
            _grantedGuardian = false;
            _grantedHaste = false;
            _grantedFlying = false;
            _grantedSakimori = false;
            _grantedAssault = false;
            _grantedNoDeckAttack = false;
            _grantedArcher = false;
            _grantedDeadly = false;
            _atkLabel.text = CurrentAttack.ToString();
            _hpLabel.text = _currentHp.ToString();
            RefreshKeywordIcons();
            // タップ状態（横向き）も解除して縦に戻す
            SetTapped(false);
        }

        // 別キャラのランタイム状態（攻撃力バフ・HPバフ・現在HP）をこのカードへ複製する（演出なしで即時反映）。
        // CopyFieldChar 効果で「バフ・ダメージ込みの現在値」をコピーするのに使う。
        public void CopyRuntimeStateFrom(CardView source)
        {
            _attackBuff = source._attackBuff;
            _hpBuff = source._hpBuff;
            _currentHp = source._currentHp;
            _grantedGuardian = source._grantedGuardian;
            _grantedHaste = source._grantedHaste;
            _grantedFlying = source._grantedFlying;
            _grantedSakimori = source._grantedSakimori;
            _grantedAssault = source._grantedAssault;
            _grantedNoDeckAttack = source._grantedNoDeckAttack;
            _grantedArcher = source._grantedArcher;
            _grantedDeadly = source._grantedDeadly;
            _atkLabel.text = CurrentAttack.ToString();
            _hpLabel.text = Mathf.Max(0, _currentHp).ToString();
            RefreshKeywordIcons();
        }

        // キーワード能力アイコンの表示を現在の状態（SO 固有 OR 付与）に合わせて更新する。
        // ダメージトリガー（Data.TriggerOnGrave）はカードデータ固定のフラグだが、守護などと同じアイコン枠として一括更新する。
        private void RefreshKeywordIcons()
        {
            _guardianIcon.style.display = HasGuardian ? DisplayStyle.Flex : DisplayStyle.None;
            _hasteIcon.style.display = HasHaste ? DisplayStyle.Flex : DisplayStyle.None;
            _flyingIcon.style.display = HasFlying ? DisplayStyle.Flex : DisplayStyle.None;
            _sakimoriIcon.style.display = HasSakimori ? DisplayStyle.Flex : DisplayStyle.None;
            _assaultIcon.style.display = HasAssault ? DisplayStyle.Flex : DisplayStyle.None;
            _noDeckAttackIcon.style.display = HasNoDeckAttack ? DisplayStyle.Flex : DisplayStyle.None;
            _archerIcon.style.display = HasArcher ? DisplayStyle.Flex : DisplayStyle.None;
            _deadlyIcon.style.display = HasDeadly ? DisplayStyle.Flex : DisplayStyle.None;
            _triggerOnGraveIcon.style.display = Data.TriggerOnGrave ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // キーワード能力を実行時に永続付与する（GrantKeyword）。既に持っていれば演出のみ。
        // 付与したアイコンをポップ表示する。
        public async UniTask GrantKeywordAsync(GrantableKeyword keyword, CancellationToken ct)
        {
            VisualElement icon;
            switch (keyword)
            {
                case GrantableKeyword.Guardian:
                    _grantedGuardian = true;
                    icon = _guardianIcon;
                    break;
                case GrantableKeyword.Haste:
                    _grantedHaste = true;
                    icon = _hasteIcon;
                    break;
                case GrantableKeyword.Flying:
                    _grantedFlying = true;
                    icon = _flyingIcon;
                    break;
                case GrantableKeyword.Sakimori:
                    _grantedSakimori = true;
                    icon = _sakimoriIcon;
                    break;
                case GrantableKeyword.Assault:
                    _grantedAssault = true;
                    icon = _assaultIcon;
                    break;
                case GrantableKeyword.NoDeckAttack:
                    _grantedNoDeckAttack = true;
                    icon = _noDeckAttackIcon;
                    break;
                case GrantableKeyword.Archer:
                    _grantedArcher = true;
                    icon = _archerIcon;
                    break;
                case GrantableKeyword.Deadly:
                    _grantedDeadly = true;
                    icon = _deadlyIcon;
                    break;
                default:
                    return;
            }

            RefreshKeywordIcons();
            await PopIconAsync(icon, ct);
        }

        // 付与アイコンの出現演出：少し大きく弾ませてから元のサイズへ戻す。
        private async UniTask PopIconAsync(VisualElement icon, CancellationToken ct)
        {
            float scaleVal = 0.2f;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; icon.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1.6f, 0.18f).SetEase(Ease.OutBack))
                .Append(DOTween.To(() => scaleVal, v => { scaleVal = v; icon.style.scale = new Scale(new Vector3(v, v, 1f)); }, 1f, 0.22f).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (System.OperationCanceledException) { }

            icon.style.scale = new Scale(Vector3.one);
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

        // 攻撃力を amount 永続的に下げる（DebuffAttack）。CurrentAttack は 0 未満にならないようクランプする。
        // ATK ラベルを更新し青のパルス演出を出す（上昇のオレンジと区別）。
        public async UniTask DebuffAttackAsync(int amount, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }
            // CurrentAttack（= Data.Attack + _attackBuff）が 0 を下回らないよう、減算量を実際に下げられる分にクランプする
            int reduce = Mathf.Min(amount, CurrentAttack);
            _attackBuff -= reduce;
            _atkLabel.text = CurrentAttack.ToString();
            await PulseStatAsync(_atkArea, _atkLabel, new Color(0.25f, 0.55f, 1f), ct);
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
            RefreshKeywordIcons();

            if (data.Image != null)
            {
                _imageArea.style.backgroundImage = new StyleBackground(data.Image);
            }
        }
    }
}
