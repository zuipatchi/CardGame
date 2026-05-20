using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Home
{
    [DefaultExecutionOrder(-10000)]
    public sealed class HomeLive2DPresenter : MonoBehaviour
    {
        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private AnimationClip[] _motions;

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
            if (_animator == null || _motions == null || _motions.Length == 0)
            {
                return;
            }
            PlaySequenceAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid PlaySequenceAsync(CancellationToken token)
        {
            while (true)
            {
                AnimationClip clip = _motions[UnityEngine.Random.Range(0, _motions.Length)];
                _animator.Play(clip.name);
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(clip.length), cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
