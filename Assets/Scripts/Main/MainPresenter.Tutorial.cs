using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Tutorial;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── チュートリアル（誘導つきスクリプト対戦） ────────────────────────
        //
        // 通常のオフライン CPU 戦の仕組みをそのまま使い、決定的セットアップ（先攻固定・固定デッキ・
        // シャッフルなし。BuildAsync 参照）と、この「コーチ層」を上から被せて実現する。
        // ガイド中心：吹き出し＋ハイライトで誘導し、本番の入力系はほぼ触らない（操作の強制ゲートはしない）。
        //
        // 台本は TutorialId ごとに分岐する:
        //   BasicLoop      きほん: キャラを出す→ターン終了→相手の番（速攻でデッキ攻撃＝タップ）→タップした相手を攻撃→クリア
        //   DeckOutWin     勝ち方(デッキ切れ): 攻撃役を1体セット済み→相手デッキ(残1)へデッキ攻撃＝デッキ切れ勝利
        //   FieldCharsWin  勝ち方(制圧): 場に7体セット済み→手札の0コストキャラを1体出して8体＝制圧勝利
        //   VictoryPointsWin 勝ち方(勝利点): 勝利点18＋味方1体セット済み→E3002 を使って20点＝勝利点勝利
        //   ＜キーワード能力＞ 攻撃役と的を盤面にプリセットし、そのキーワードを実際に持つ既存カードで
        //     1アクション（攻撃。速攻のみ「出す→攻撃」）を体験させてクリア（「チュートリアル完了」表示）。
        //     守護=C1005 / 速攻=C1010 / 飛行=C5003 / 防人=C3009(守護兼) / 強襲・デッキ攻撃×=C3008(速攻兼)

        // 台本の固定デッキ・セットアップ用カードID（実在カードID。並びはそのまま使う＝シャッフルしない）。
        private static class TutorialScript
        {
            // ── BasicLoop ──
            // 手札3枚（先頭3枚が初期手札）：ぱっち少年(コスト2/攻2/HP4)＋モブぱっち×2、残りはモブぱっち。
            public static readonly string[] BasicPlayerDeckIds =
            {
                "C1001", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // CPU は手札の先頭が速攻持ち（C1010 剛腕のぱっち・白/コスト2/速攻）。これを出して即デッキ攻撃すると
            // 攻撃したキャラがタップ（横向き）するため、次のプレイヤーの番に「攻撃できる対象」ができる。
            // （守護には触れず、タップの仕組みだけで攻撃を教える）
            public static readonly string[] BasicCpuDeckIds =
            {
                "C1010", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // ── DeckOutWin（デッキ切れ） ──
            // 攻撃役（ぱっち少年・攻2）を場にプリセットし、相手デッキを残り1枚にして1回のデッキ攻撃で勝つ。
            public static readonly string[] DeckOutPlayerDeckIds =
            {
                "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // CPU は6枚（手札5＋デッキ1）。守護なし＝デッキ攻撃が通る。残り1枚を攻2で削り切って勝利。
            public static readonly string[] DeckOutCpuDeckIds =
            {
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // ── FieldCharsWin（制圧） ──
            // 手札の先頭は0コストの魔法使いぱっちトークン（C1009）。これを出して8体目にする。
            public static readonly string[] FieldCharsPlayerDeckIds =
            {
                "C1009", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // CPU はプレイヤーが1ターン目に勝つため事実上出番なし。最低限の枚数を持たせる。
            public static readonly string[] FieldCharsCpuDeckIds =
            {
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // 開始時に自分の場へ並べておくキャラ（7体）。残り1体を出すと制圧勝利。
            public static readonly string[] FieldCharsPresetIds =
            {
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // ── VictoryPointsWin（勝利点） ──
            // 手札の先頭は0コストの「不死鳥の恵み」(E3002・勝利点2)。これを使って20点にする。
            public static readonly string[] VictoryPointsPlayerDeckIds =
            {
                "E3002", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            public static readonly string[] VictoryPointsCpuDeckIds =
            {
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // ── キーワード能力（共通） ──
            // 攻撃役・的は盤面にプリセットするため、手札・デッキはモブぱっちのフィラーでよい。
            public static readonly string[] KeywordFillerDeckIds =
            {
                "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // 速攻だけは「出したターンに攻撃」を見せるため、手札の先頭に速攻キャラ（C1010・白・コスト2）を入れる。
            public static readonly string[] HastePlayerDeckIds =
            {
                "C1010", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };
        }

        // E3002 が付与する勝利点。プリセット値（あと何点で勝てるか）の計算に使う。
        private const int VictoryPointsTutorialGain = 2;

        private string[] TutorialPlayerDeckIds()
        {
            switch (_tutorialId)
            {
                case TutorialId.DeckOutWin:
                    return TutorialScript.DeckOutPlayerDeckIds;
                case TutorialId.FieldCharsWin:
                    return TutorialScript.FieldCharsPlayerDeckIds;
                case TutorialId.VictoryPointsWin:
                    return TutorialScript.VictoryPointsPlayerDeckIds;
                case TutorialId.HasteKw:
                    return TutorialScript.HastePlayerDeckIds;
                case TutorialId.GuardianKw:
                case TutorialId.FlyingKw:
                case TutorialId.SakimoriKw:
                case TutorialId.AssaultKw:
                case TutorialId.NoDeckAttackKw:
                    return TutorialScript.KeywordFillerDeckIds;
                default:
                    return TutorialScript.BasicPlayerDeckIds;
            }
        }

        private string[] TutorialCpuDeckIds()
        {
            switch (_tutorialId)
            {
                case TutorialId.DeckOutWin:
                    return TutorialScript.DeckOutCpuDeckIds;
                case TutorialId.FieldCharsWin:
                    return TutorialScript.FieldCharsCpuDeckIds;
                case TutorialId.VictoryPointsWin:
                    return TutorialScript.VictoryPointsCpuDeckIds;
                case TutorialId.GuardianKw:
                case TutorialId.HasteKw:
                case TutorialId.FlyingKw:
                case TutorialId.SakimoriKw:
                case TutorialId.AssaultKw:
                case TutorialId.NoDeckAttackKw:
                    return TutorialScript.KeywordFillerDeckIds;
                default:
                    return TutorialScript.BasicCpuDeckIds;
            }
        }

        // キーワード能力チュートリアルかどうか。
        private bool IsKeywordTutorial()
        {
            return _tutorialId == TutorialId.GuardianKw
                || _tutorialId == TutorialId.HasteKw
                || _tutorialId == TutorialId.FlyingKw
                || _tutorialId == TutorialId.SakimoriKw
                || _tutorialId == TutorialId.AssaultKw
                || _tutorialId == TutorialId.NoDeckAttackKw;
        }

        // 進行ステップ（BasicLoop）：0=キャラを出す / 1=ターン終了 / 2=相手の番（守護） / 3=攻撃 / 4=完了
        private int _tutorialStep;
        private bool _tutorialCpuActed;
        private CardView _tutorialHeroCard;
        // CompleteTutorial で立てる。勝敗オーバーレイを「YOU WIN」ではなく「チュートリアル完了」表示にするためのフラグ。
        // 勝ち方（デッキ切れ/制圧/勝利点）は winReason 付きで終わるためこのフラグは使わない。
        private bool _tutorialCompleted;

        private VisualElement _tutorialCoach;
        private VisualElement _tutorialCoachPanel;
        private Label _tutorialCoachLabel;
        private Label _tutorialCoachChip;
        private readonly List<VisualElement> _tutorialHighlighted = new List<VisualElement>();

        // BuildAsync の末尾（ゲームループ開始前）で呼ぶ。コーチ吹き出しを生成し、必要なら盤面をセットする。
        private void SetupTutorial(VisualElement mainRoot)
        {
            _tutorialStep = 0;
            _tutorialCpuActed = false;

            _tutorialCoach = new VisualElement();
            _tutorialCoach.AddToClassList("tutorial-coach");
            _tutorialCoach.pickingMode = PickingMode.Ignore;
            _tutorialCoach.style.display = DisplayStyle.None;

            _tutorialCoachPanel = new VisualElement();
            _tutorialCoachPanel.AddToClassList("tutorial-coach-panel");
            // パネル本体はクリックを拾う（タップで畳む＝下のカードを確認できる）。
            _tutorialCoachPanel.pickingMode = PickingMode.Position;
            _tutorialCoachPanel.RegisterCallback<ClickEvent>(_ => SetCoachCollapsed(true));

            _tutorialCoachLabel = new Label();
            _tutorialCoachLabel.AddToClassList("tutorial-coach-label");
            _tutorialCoachLabel.pickingMode = PickingMode.Ignore;
            _tutorialCoachPanel.Add(_tutorialCoachLabel);

            Label hint = new Label("（タップで隠す）");
            hint.AddToClassList("tutorial-coach-hint");
            hint.pickingMode = PickingMode.Ignore;
            _tutorialCoachPanel.Add(hint);

            _tutorialCoach.Add(_tutorialCoachPanel);

            // 畳んだときに出る再表示ボタン（タップで吹き出しを戻す）。
            _tutorialCoachChip = new Label("タップで再表示");
            _tutorialCoachChip.AddToClassList("tutorial-coach-chip");
            _tutorialCoachChip.pickingMode = PickingMode.Position;
            _tutorialCoachChip.RegisterCallback<ClickEvent>(_ => SetCoachCollapsed(false));
            _tutorialCoachChip.style.display = DisplayStyle.None;
            _tutorialCoach.Add(_tutorialCoachChip);

            mainRoot.Add(_tutorialCoach);

            if (_tutorialId == TutorialId.DeckOutWin)
            {
                PresetDeckOutTutorial();
            }
            else if (_tutorialId == TutorialId.FieldCharsWin)
            {
                PresetFieldCharsTutorial();
            }
            else if (_tutorialId == TutorialId.VictoryPointsWin)
            {
                PresetVictoryPointsTutorial();
            }
            else if (IsKeywordTutorial())
            {
                PresetKeywordTutorial();
            }
        }

        // キーワード能力チュートリアルの盤面をセットする。攻撃役と「的」を置き、必要なら相手キャラをタップ状態にする。
        // 使うカードはそのキーワードを実際に持つ既存カード（守護=C1005 / 飛行=C5003 / 防人=C3009 / 強襲・デッキ攻撃×=C3008）。
        private void PresetKeywordTutorial()
        {
            switch (_tutorialId)
            {
                case TutorialId.GuardianKw:
                    _tutorialHeroCard = PresetCharacter("C1001", isOpponent: false, tapped: false);   // 攻撃役
                    PresetCharacter("C1005", isOpponent: true, tapped: true);   // 守護
                    PresetCharacter("C1011", isOpponent: true, tapped: true);                          // 守護以外（攻撃できない）
                    break;
                case TutorialId.HasteKw:
                    // 速攻キャラは手札から出す（C1010）。相手にタップ済みの的を置く。
                    PresetCharacter("C1011", isOpponent: true, tapped: true);
                    break;
                case TutorialId.FlyingKw:
                    _tutorialHeroCard = PresetCharacter("C5003", isOpponent: false, tapped: false);    // 飛行
                    PresetCharacter("C1005", isOpponent: true, tapped: true);                          // 守護（無視できる）
                    PresetCharacter("C1011", isOpponent: true, tapped: true);   // 守護の奥（飛行で狙える）
                    break;
                case TutorialId.SakimoriKw:
                    _tutorialHeroCard = PresetCharacter("C5003", isOpponent: false, tapped: false);    // 飛行
                    PresetCharacter("C3009", isOpponent: true, tapped: true);   // 防人（飛行は優先攻撃）
                    PresetCharacter("C1011", isOpponent: true, tapped: true);                          // 防人がいると狙えない
                    break;
                case TutorialId.AssaultKw:
                    _tutorialHeroCard = PresetCharacter("C3008", isOpponent: false, tapped: false);    // 強襲
                    PresetCharacter("C1011", isOpponent: true, tapped: false);  // 未タップ（強襲で狙える）
                    break;
                case TutorialId.NoDeckAttackKw:
                    _tutorialHeroCard = PresetCharacter("C3008", isOpponent: false, tapped: false);    // デッキ攻撃×
                    PresetCharacter("C1011", isOpponent: true, tapped: true);   // 代わりに攻撃する的
                    break;
            }
        }

        // 指定IDのキャラを場に出す共通ヘルパー。tapped=true なら横向き（攻撃対象になる）にする。
        private CardView PresetCharacter(string id, bool isOpponent, bool tapped)
        {
            if (!_cardDatabase.TryGet(id, out CardData data))
            {
                return null;
            }
            FieldView field = isOpponent ? _opponentFieldView : _playerFieldView;
            if (field.IsCharactersFull)
            {
                return null;
            }
            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: isOpponent);
            field.PlaceCard(card);
            if (tapped)
            {
                card.SetTapped(true);
            }
            return card;
        }

        // デッキ切れチュートリアル：攻撃役（ぱっち少年・攻2）を1体だけ場に出しておく。
        // この1体で相手デッキ（残り1枚）へデッキ攻撃するとデッキ切れ勝利になる。
        private void PresetDeckOutTutorial()
        {
            if (_cardDatabase.TryGet("C1001", out CardData attacker))
            {
                CardView card = new CardView(_cardStore.CardTemplate, attacker, _cardStore.CardBack, faceDown: false, isOpponent: false);
                _playerFieldView.PlaceCard(card);
                _tutorialHeroCard = card;
            }
        }

        // 制圧チュートリアル：自分の場にあらかじめキャラを並べておく（あと1体で8体＝勝利）。
        private void PresetFieldCharsTutorial()
        {
            foreach (string id in TutorialScript.FieldCharsPresetIds)
            {
                if (_playerFieldView.IsCharactersFull)
                {
                    break;
                }
                if (_cardDatabase.TryGet(id, out CardData data))
                {
                    CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: false);
                    _playerFieldView.PlaceCard(card);
                }
            }
        }

        // 勝利点チュートリアル：味方を1体だけ場に出し（E3002 のバフ対象。1体なので対象選択は自動）、
        // 自分の勝利点をあと VictoryPointsTutorialGain 点で勝てる値にプリセットする。
        private void PresetVictoryPointsTutorial()
        {
            if (_cardDatabase.TryGet("C1011", out CardData ally))
            {
                CardView card = new CardView(_cardStore.CardTemplate, ally, _cardStore.CardBack, faceDown: false, isOpponent: false);
                _playerFieldView.PlaceCard(card);
            }

            int preset = WinRule.VictoryPointsToWin - VictoryPointsTutorialGain;
            if (preset < 0)
            {
                preset = 0;
            }
            _playerVictoryPoints.AddPoints(preset);
            _playerVictoryPoints.SetDisplayedPoints(preset);
        }

        // 自分のメインフェーズ開始時に、現在ステップの案内を表示する。
        private void TutorialBeginPlayerMainPhase()
        {
            if (_tutorialId == TutorialId.DeckOutWin)
            {
                ShowCoach("勝ち方のひとつ「デッキ切れ」を体験しよう。\n相手のデッキをマイナスにすると勝ち（0枚では勝ちにならない）！自分のキャラから、相手の【デッキ】（左上のカードの山）へ矢印をドラッグして、デッキに攻撃してとどめを刺そう！\n攻撃したキャラの攻撃力分の数だけ相手のデッキを上から破棄できるよ");
                ClearHighlights();
                return;
            }

            if (_tutorialId == TutorialId.FieldCharsWin)
            {
                ShowCoach("勝ち方のひとつ「制圧」を体験しよう。\n自分の場にキャラを8体ならべると勝ち！今7体いるから、手札の枠線が緑に光っているキャラを場へ出して8体にしよう");
                HighlightHandCard("C1009");
                return;
            }

            if (_tutorialId == TutorialId.VictoryPointsWin)
            {
                ShowCoach("勝ち方のひとつ「勝利点」を体験しよう。\n勝利点が20点になったら勝ち！キミは今あと2点（左下に現在の勝利点が表示される）。手札の光っているカードを使って勝利点を得て、20点にしよう");
                HighlightHandCard("E3002");
                return;
            }

            if (IsKeywordTutorial())
            {
                TutorialBeginKeywordPhase();
                return;
            }

            // BasicLoop
            switch (_tutorialStep)
            {
                case 0:
                    ShowCoach("ようこそ！Patchiカードゲームの基本を体験しよう。\nまずは手札の緑に枠が光っているキャラカードを、自分の場（画面中央の下半分）へドラッグして出してみよう");
                    HighlightHandCard("C1001");
                    break;
                case 3:
                    // 攻撃対象は標準のオレンジハイライト（attack-target-char）に任せ、緑枠は出さない。
                    ShowCoach("タップ中の相手キャラには攻撃できる。自分のキャラから、相手キャラへ矢印をドラッグして攻撃してみよう！");
                    ClearHighlights();
                    break;
            }
        }

        // キーワード能力チュートリアルのメインフェーズ開始時の案内。
        private void TutorialBeginKeywordPhase()
        {
            switch (_tutorialId)
            {
                case TutorialId.GuardianKw:
                    ShowCoach("「守護（しゅご）」を体験しよう。\n相手に守護を持つキャラがいる間は、守護を持つキャラしか攻撃できない。\n守護を持たないキャラへ攻撃できないことを確認してから守護を持つキャラを攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.HasteKw:
                    if (_tutorialHeroCard != null)
                    {
                        ShowCoach("速攻はキャラを出したターンにそのまま攻撃できる。\n相手キャラへ矢印をドラッグして攻撃しよう！");
                        ClearHighlights();
                    }
                    else
                    {
                        ShowCoach("「速攻（そっこう）」を体験しよう。\n速攻持ちは出したターンからすぐ攻撃できる。\n手札の枠が緑に光るキャラを場へ出そう（コストに手札を2枚使う）");
                        HighlightHandCard("C1010");
                    }
                    break;
                case TutorialId.FlyingKw:
                    ShowCoach("「飛行（ひこう）」を体験しよう。\n飛行は相手の守護を持つキャラを無視して攻撃できる。\n守護を持っていない相手キャラへドラッグして攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.SakimoriKw:
                    ShowCoach("「防人（さきもり）」を体験しよう。\n相手に防人がいると、飛行を持つキャラは防人を優先して攻撃しないといけない。\n防人でないキャラへ攻撃できないことを確認してから、防人のキャラへ矢印をドラッグして攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.AssaultKw:
                    ShowCoach("「強襲（きょうしゅう）」を体験しよう。\n強襲は、まだタップしていない（縦向きの）相手キャラにも攻撃できる。\nタップしていない相手キャラへ矢印をドラッグして攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.NoDeckAttackKw:
                    ShowCoach("「デッキ攻撃×」を体験しよう。\nこの能力を持つキャラは相手デッキを直接攻撃できない。\nデッキへ矢印をドラッグして攻撃できないことを確認してから代わりに相手キャラへ矢印をドラッグして攻撃しよう！");
                    ClearHighlights();
                    break;
            }
        }

        // 基本チュートリアル：キャラを場へドロップしてコスト選択が始まったときに呼ぶ。
        // 「出す」吹き出しから「コストを払う」吹き出しへ切り替える（2段階に分割）。
        private void TutorialOnLocalStagedCost()
        {
            if (_tutorialId == TutorialId.BasicLoop && _tutorialStep == 0)
            {
                // AutoOk（自動OK）が ON なら、必要枚数を選んだ時点で自動確定するため「OKを押す」案内は出さない。
                bool autoOk = _optionModel.AutoOk.CurrentValue;
                ShowCoach(autoOk
                    ? "いいね！キャラを出すにはコストを払うよ。\n手札のカードを2枚クリックして選ぼう"
                    : "いいね！キャラを出すにはコストを払うよ。\n手札のカードを2枚クリックして選んでから【OK】ボタンを押そう");
                ClearHighlights();
            }
        }

        // 自分のアクションが解決した直後に呼ぶ。期待アクションならステップを進める / クリアする。
        private void TutorialOnLocalActionResolved(MainPhaseAction action)
        {
            if (_tutorialId == TutorialId.BasicLoop)
            {
                if (_tutorialStep == 0 && action._actionType == MainPhaseActionType.PlaceChar)
                {
                    _tutorialStep = 1;
                    _tutorialHeroCard = action._card;
                    ShowCoach("ナイス！キャラを場に出せたね。\nこれであなたの最初のターンの目的は完了。右側の【END】ボタンを押してターンを終了しよう");
                    Highlight(_endButton);
                }
                else if (_tutorialStep == 3 && action._actionType == MainPhaseActionType.Attack)
                {
                    CompleteTutorial();
                }
                return;
            }

            if (IsKeywordTutorial())
            {
                // 速攻：キャラを出した直後は「そのまま攻撃」へ誘導する（速攻＝召喚酔いなし）。
                if (_tutorialId == TutorialId.HasteKw && action._actionType == MainPhaseActionType.PlaceChar)
                {
                    _tutorialHeroCard = action._card;
                    ShowCoach("出せたね！速攻だから出したこのターンのうちにそのまま攻撃できる。\n自分のキャラから相手キャラへ矢印をドラッグして攻撃しよう！");
                    ClearHighlights();
                    return;
                }
                if (action._actionType == MainPhaseActionType.Attack)
                {
                    CompleteTutorial();
                }
                return;
            }

            // 勝ち方（デッキ切れ・制圧・勝利点）はエンジンが勝利を出すため何もしない。
        }

        // チュートリアルのクリア処理：勝敗オーバーレイ（YOU WIN）を出して終了する。
        private void CompleteTutorial()
        {
            if (_isGameOver)
            {
                return;
            }
            _tutorialStep = 4;
            _tutorialCompleted = true;
            _isGameOver = true;
            OnGameEnd(playerWins: true);
        }

        // 自分がパス（ターン終了）したときに呼ぶ。
        private void TutorialOnLocalPass()
        {
            if (_tutorialId != TutorialId.BasicLoop)
            {
                return;
            }

            if (_tutorialStep == 1)
            {
                _tutorialStep = 2;
                ClearHighlights();
            }
        }

        // ─── 台本どおりに動く相手（チュートリアル専用 CPU ループ） ──────────
        private async UniTask RunTutorialOpponentMainLoopAsync(CancellationToken ct)
        {
            // BasicLoop のみ、相手が速攻キャラ（C1010）を出してキミのデッキを攻撃する。
            // 攻撃したキャラはタップ（横向き）するため、次のプレイヤーの番に「攻撃できる対象」ができる。
            // （守護には触れず、タップの仕組みだけで攻撃を教える）
            if (_tutorialId == TutorialId.BasicLoop && _tutorialStep == 2 && !_tutorialCpuActed)
            {
                _tutorialCpuActed = true;
                ShowCoach("相手の番だよ。相手はキャラを出して、キミのデッキを攻撃してきた。\n攻撃したキャラは「タップ」（横向き）になるんだ");
                ClearHighlights();

                IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                if (cpuHand.Count > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                    await ExecuteCpuMainActionAsync(
                        new MainPhaseAction { _actionType = MainPhaseActionType.PlaceChar, _card = cpuHand[0] },
                        ct);

                    // 出したキャラ（速攻）でキミのデッキを攻撃させ、相手キャラをタップ状態にする。
                    CardView cpuAttacker = _opponentFieldView.Characters.FirstOrDefault();
                    if (cpuAttacker != null && !_isGameOver)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                        await ExecuteCpuMainActionAsync(
                            new MainPhaseAction { _actionType = MainPhaseActionType.Attack, _attacker = cpuAttacker, _targetsDeck = true },
                            ct);
                    }
                }

                _tutorialStep = 3;
            }

            // それ以外（勝ち方チュートリアルや以降の相手ターン）は何もせずパスする。
        }

        // ─── コーチ吹き出し・ハイライト ──────────────────────────────────
        private void ShowCoach(string text)
        {
            if (_tutorialCoach == null)
            {
                return;
            }
            _tutorialCoachLabel.text = text;
            _tutorialCoach.style.display = DisplayStyle.Flex;
            // 新しい案内は展開状態で表示する。
            SetCoachCollapsed(false);
        }

        // 吹き出しの展開／畳みを切り替える（畳んでも「ヒントを見る」ボタンで再表示できる）。
        // ハイライト枠は常に残して誘導は維持する。
        private void SetCoachCollapsed(bool collapsed)
        {
            if (_tutorialCoachPanel != null)
            {
                _tutorialCoachPanel.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_tutorialCoachChip != null)
            {
                _tutorialCoachChip.style.display = collapsed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void TutorialHideCoach()
        {
            ClearHighlights();
            if (_tutorialCoach != null)
            {
                _tutorialCoach.style.display = DisplayStyle.None;
            }
        }

        // 手札内の指定IDのカードを光らせる。
        private void HighlightHandCard(string id)
        {
            CardView card = _handView.Cards.FirstOrDefault(c => c.Data != null && c.Data.Id == id);
            if (card != null)
            {
                Highlight(card);
            }
            else
            {
                ClearHighlights();
            }
        }

        private void Highlight(params VisualElement[] targets)
        {
            ClearHighlights();
            if (targets == null)
            {
                return;
            }
            foreach (VisualElement target in targets)
            {
                if (target == null)
                {
                    continue;
                }
                target.AddToClassList("tutorial-highlight");
                _tutorialHighlighted.Add(target);
            }
        }

        private void ClearHighlights()
        {
            foreach (VisualElement target in _tutorialHighlighted)
            {
                target?.RemoveFromClassList("tutorial-highlight");
            }
            _tutorialHighlighted.Clear();
        }
    }
}
