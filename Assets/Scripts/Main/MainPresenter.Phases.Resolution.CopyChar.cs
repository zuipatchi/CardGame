using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // CopyFieldChar 効果：発動側が自フィールドのキャラを1体選び、そのコピーを copies 体（未設定=0 は1体）
        // 自フィールドに出す。コピーはバフ・現在HP込みの状態を複製する（SummonSingleCharAsync に stateSource を渡す）。
        // 配置時に OnEnter も発動する。自キャラが0体なら空振り、フィールド満杯で打ち切り。
        // 選択はプレイヤー（自フィールドのクリック選択）／CPU（攻撃力上位）／オンライン相手（フィールド内インデックスを受信）で分岐。
        // インデックスは両クライアントで同期済みの盤面に対するもので、DamageEnemy と同じ NGS_DamageTarget チャネルを流用する。
        internal async UniTask ApplyCopyFieldCharAsync(int copies, bool isLocal, CancellationToken ct)
        {
            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
            List<CardView> chars = new List<CardView>(field.Characters);
            if (chars.Count == 0)
            {
                return;
            }

            CardView source = await ResolveCopySourceCharAsync(chars, isLocal, ct);
            if (source == null)
            {
                return;
            }

            int copyCount = copies <= 0 ? 1 : copies;
            Rect fromRect = source.worldBound;
            for (int i = 0; i < copyCount; i++)
            {
                // フィールドが満杯（キャラ8体勝利成立含む）になったら打ち切る
                if (field.IsCharactersFull || _isGameOver)
                {
                    break;
                }
                await SummonSingleCharAsync(source.Data, field, isLocal, fromRect, ct, source);
            }
        }

        // コピー元の自キャラを決定する。
        // ローカル：1体なら自動・複数なら自フィールドのクリック選択（選んだらオンラインへインデックス送信）。
        // オンライン相手：インデックスを受信。CPU：攻撃力上位を選ぶ。
        private async UniTask<CardView> ResolveCopySourceCharAsync(List<CardView> chars, bool isLocal, CancellationToken ct)
        {
            if (isLocal)
            {
                CardView selected = chars.Count == 1
                    ? chars[0]
                    : await WaitForPlayerFieldCharSelectionAsync(ct);
                if (selected == null)
                {
                    return null;
                }
                if (_isOnline)
                {
                    _networkGameService.SendDamageTargets(new[] { chars.IndexOf(selected) });
                }
                return selected;
            }

            if (_isOnline)
            {
                int[] indices = await _networkGameService.WaitForOpponentDamageTargetsAsync(ct);
                int index = (indices != null && indices.Length > 0) ? indices[0] : 0;
                return (index >= 0 && index < chars.Count) ? chars[index] : chars[0];
            }

            // CPU：攻撃力が最も高いキャラをコピーする
            return chars.OrderByDescending(c => c.CurrentAttack).First();
        }
    }
}
