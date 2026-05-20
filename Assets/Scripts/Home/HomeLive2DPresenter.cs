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
        private AnimationClip[] _motions;

        private void Start()
        {
            if (_motionController == null || _motions == null || _motions.Length == 0)
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
