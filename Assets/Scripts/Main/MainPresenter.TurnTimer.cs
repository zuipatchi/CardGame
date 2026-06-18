using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Game;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── 手番の制限時間 ──────────────────────────────────────────────
        //
        // 自分のメインフェーズに制限時間を設ける。0 になったら自動でターン終了（パス）。
        // タイマーはトークンをキャンセルせず、入力待ち中の _mainActionTcs を Pass で完了させる方式。
        // 途中操作（ステージ中のカード・コスト選択）は既存の OnBackClicked で巻き戻す。
        // カード効果の解決アニメーション中（_mainActionTcs == null）は中断せず、
        // 次の入力待ち（WaitForPlayerMainActionAsync 冒頭）で即パスする（オンラインの同期ずれを避けるため）。

        // 1ターンの制限時間（秒）。
        private const int TurnTimeLimitSeconds = 60;
        // 残りこの秒数以下で警告表示（赤点滅＋警告SE）にする。
        private const int TurnTimeWarningSeconds = 10;

        // 自分のメインフェーズ開始時に呼ぶ。残り時間のカウントダウンを開始する。
        private void StartTurnTimer(CancellationToken ct)
        {
            _turnTimedOut = false;
            RunTurnTimerAsync(ct).Forget();
        }

        private async UniTaskVoid RunTurnTimerAsync(CancellationToken ct)
        {
            int remaining = TurnTimeLimitSeconds;
            _turnTimerView.SetWarning(false);
            _turnTimerView.SetRemaining(remaining);
            _turnTimerView.Show();

            try
            {
                while (remaining > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
                    remaining--;
                    _turnTimerView.SetRemaining(remaining);

                    if (remaining <= TurnTimeWarningSeconds)
                    {
                        _turnTimerView.SetWarning(true);
                        // 残り少の毎秒チクタク（既存の短い SE を流用）
                        if (remaining > 0 && _soundStore.Cancel1SE != null)
                        {
                            _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ターンが時間内に終了した（パス・ゲーム終了・シーン破棄）
                return;
            }

            TriggerTurnTimeout();
        }

        // 時間切れ。入力待ち中なら今すぐ強制パス。解決中なら次の入力待ちで即パスさせる。
        private void TriggerTurnTimeout()
        {
            if (_turnTimedOut || _isGameOver)
            {
                return;
            }
            _turnTimedOut = true;
            ForcePassIfWaiting();
        }

        // 入力待ち中（_mainActionTcs が有効）のときだけ、その場で強制パスする。
        private void ForcePassIfWaiting()
        {
            if (_mainActionTcs == null)
            {
                return;
            }
            // ステージ中のカード・コスト選択を手札へ巻き戻してからパスする
            OnBackClicked();
            _mainActionTcs.TrySetResult(new MainPhaseAction { _actionType = MainPhaseActionType.Pass });
        }
    }
}
