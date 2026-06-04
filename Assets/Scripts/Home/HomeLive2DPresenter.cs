using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Home
{
    [DefaultExecutionOrder(-10000)]
    public sealed class HomeLive2DPresenter : MonoBehaviour
    {
        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private AnimationClip[] _motions;

        [SerializeField]
        private AnimationClip _walkClip;

        [SerializeField]
        private AnimationClip _eatClip;

        [SerializeField]
        private AnimationClip _sadClip;

        public bool IsRainy { get; set; }

        [SerializeField]
        private float _walkSpeed = 2f;

        private DogSpeechPresenter _speechPresenter;

        [Inject]
        public void Construct(DogSpeechPresenter speechPresenter)
        {
            _speechPresenter = speechPresenter;
        }

        private readonly Queue<(Vector3 pos, Animator food)> _foodQueue = new Queue<(Vector3, Animator)>();

        // アイドルアニメーションの待機を中断するための CTS
        private CancellationTokenSource _idleInterruptCts = new CancellationTokenSource();

        private void Awake()
        {
            if (_animator == null)
            {
                return;
            }

            // CubismFadeController は FadeMotionList が未設定だと毎フレーム NullRef になるため無効化する
            // フェード時間はすべて 0 に設定済みなので無効化しても映像に影響しない
            Component[] components = _animator.GetComponents<Component>();
            foreach (Component c in components)
            {
                if (c.GetType().Name == "CubismFadeController" && c is Behaviour b)
                {
                    b.enabled = false;
                    break;
                }
            }
        }

        private void Start()
        {
            if (_animator == null)
            {
                return;
            }
            bool hasMotions = _motions != null && _motions.Length > 0;
            bool hasSad = IsRainy && _sadClip != null;
            if (!hasMotions && !hasSad)
            {
                return;
            }
            PlaySequenceAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            _idleInterruptCts.Cancel();
            _idleInterruptCts.Dispose();
        }

        public void NotifyFoodSpawned(Vector3 worldPos, Animator foodAnimator)
        {
            _foodQueue.Enqueue((worldPos, foodAnimator));

            // アイドル待機中なら即座にキャンセルしてループに戻す
            CancellationTokenSource old = _idleInterruptCts;
            _idleInterruptCts = new CancellationTokenSource();
            old.Cancel();
            old.Dispose();
        }

        private async UniTaskVoid PlaySequenceAsync(CancellationToken token)
        {
            while (true)
            {
                if (_foodQueue.Count > 0 && _walkClip != null && _eatClip != null)
                {
                    (Vector3 target, Animator food) = _foodQueue.Dequeue();
                    try
                    {
                        await WalkAndEatAsync(target, food, token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                else
                {
                    AnimationClip clip;
                    if (IsRainy && _sadClip != null)
                    {
                        clip = _sadClip;
                    }
                    else
                    {
                        clip = _motions[UnityEngine.Random.Range(0, _motions.Length)];
                    }
                    _animator.Play(clip.name);

                    // destroyCancellationToken と _idleInterruptCts の両方で中断可能にする
                    using CancellationTokenSource linked =
                        CancellationTokenSource.CreateLinkedTokenSource(token, _idleInterruptCts.Token);
                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(clip.length), cancellationToken: linked.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                        // 食べ物スポーンによる中断 → ループ先頭に戻ってキューを処理
                    }
                }
            }
        }

        private async UniTask WalkAndEatAsync(Vector3 target, Animator foodAnimator, CancellationToken token)
        {
            Transform dogTransform = _animator.transform;
            Vector3 startPos = dogTransform.position;
            float dx = target.x - startPos.x;
            float dy = target.y - startPos.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);

            if (distance > 0.001f)
            {
                // 移動方向に向きを合わせる（スケールX の符号で左右反転）
                if (Mathf.Abs(dx) > 0.001f)
                {
                    Vector3 scale = dogTransform.localScale;
                    scale.x = dx > 0f ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                    dogTransform.localScale = scale;
                }

                float duration = distance / _walkSpeed;
                float elapsed = 0f;
                float walkTimer = 0f;
                _animator.Play(_walkClip.name);

                while (elapsed < duration)
                {
                    await UniTask.NextFrame(cancellationToken: token);

                    float dt = Time.deltaTime;
                    elapsed += dt;
                    walkTimer += dt;

                    // Walk クリップが終端に達したら先頭から再生してループさせる
                    if (walkTimer >= _walkClip.length)
                    {
                        walkTimer -= _walkClip.length;
                        _animator.Play(_walkClip.name);
                    }

                    float t = Mathf.Clamp01(elapsed / duration);
                    dogTransform.position = new Vector3(
                        Mathf.Lerp(startPos.x, target.x, t),
                        Mathf.Lerp(startPos.y, target.y, t),
                        startPos.z
                    );
                }
            }

            // 犬と食べ物の Eat アニメーションを同時再生
            _animator.Play(_eatClip.name);
            if (foodAnimator != null)
            {
                foodAnimator.Play("Eat");
            }

            async UniTask WaitAndDestroyFoodAsync()
            {
                try
                {
                    await UniTask.NextFrame(cancellationToken: token);
                    while (foodAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                    {
                        await UniTask.NextFrame(cancellationToken: token);
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(foodAnimator.gameObject);
                }
            }

            UniTask dogEatTask = UniTask.Delay(TimeSpan.FromSeconds(_eatClip.length), cancellationToken: token);
            UniTask foodEatTask = foodAnimator != null ? WaitAndDestroyFoodAsync() : UniTask.CompletedTask;
            await UniTask.WhenAll(dogEatTask, foodEatTask);

            _speechPresenter?.ShowEatMessage();
        }
    }
}
