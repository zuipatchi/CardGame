using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Live2D.Cubism.Framework.Motion;
using UnityEngine;

namespace Home
{
    public sealed class HomeLive2DPresenter : MonoBehaviour
    {
        [SerializeField]
        private CubismMotionController _motionController;

        [SerializeField]
        private AnimationClip _idleMotion;

        [SerializeField]
        private AnimationClip[] _randomMotions;

        private void Start()
        {
            if (_motionController == null || _idleMotion == null)
            {
                return;
            }
            PlaySequenceAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid PlaySequenceAsync(CancellationToken token)
        {
            while (true)
            {
                _motionController.PlayAnimation(_idleMotion, layerIndex: 0, priority: CubismMotionPriority.PriorityForce, isLoop: false);
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_idleMotion.length), cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_randomMotions == null || _randomMotions.Length == 0)
                {
                    continue;
                }

                AnimationClip clip = _randomMotions[UnityEngine.Random.Range(0, _randomMotions.Length)];
                _motionController.PlayAnimation(clip, layerIndex: 0, priority: CubismMotionPriority.PriorityForce, isLoop: false);
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
