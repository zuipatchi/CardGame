using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace Common.View
{
    // 一定時間だけ Label を表示して自動で隠すトースト表示の共有ヘルパー。
    // 連続表示時は前回のタイマーをキャンセルして上書きする。
    public sealed class ToastController : IDisposable
    {
        private readonly Label _label;
        private CancellationTokenSource _cts;

        public ToastController(Label label)
        {
            _label = label;
        }

        // message を durationMs ミリ秒だけ表示する。linkedToken でシーン破棄に追従する。
        public void Show(string message, int durationMs, CancellationToken linkedToken)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
            ShowAsync(message, durationMs, _cts.Token).Forget();
        }

        private async UniTaskVoid ShowAsync(string message, int durationMs, CancellationToken token)
        {
            _label.text = message;
            _label.style.display = DisplayStyle.Flex;
            try
            {
                await UniTask.Delay(durationMs, cancellationToken: token);
                _label.style.display = DisplayStyle.None;
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
