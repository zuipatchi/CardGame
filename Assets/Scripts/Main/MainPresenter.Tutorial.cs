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
        //   BasicLoop      きほん: キャラを出す→ターン終了でクリア（カードの使い方とターン終了だけを学ぶ）
        //   AttackBasics   攻撃の仕方: キャラを出す→召喚酔いで攻撃できないと知る→ターン終了→相手の番（速攻でデッキ攻撃＝タップ）
        //                  →次の自分の番でタップした相手を攻撃（アンタップには攻撃できない／反撃ダメージなし）→クリア
        //   DeckOutWin     勝ち方(デッキ切れ): 攻撃役を1体セット済み→相手デッキ(残1)へデッキ攻撃＝デッキ切れ勝利
        //   VictoryPointsWin 勝ち方(勝利点): 勝利点18＋味方1体セット済み→E3002 を使って20点＝勝利点勝利
        //   ＜キーワード能力＞ 攻撃役と的を盤面にプリセットし、そのキーワードを実際に持つ既存カードで
        //     1アクション（攻撃。速攻のみ「出す→攻撃」）を体験させてクリア（「チュートリアル完了」表示）。
        //     守護=C1005 / 速攻=C1009 / 飛行=C5003 / 防人=C3007(守護兼) / 強襲・デッキ攻撃×=C3006(速攻兼) / 必殺=C7006

        // 台本の固定デッキ・セットアップ用カードID（実在カードID。並びはそのまま使う＝シャッフルしない）。
        private static class TutorialScript
        {
            // ── BasicLoop / AttackBasics（共通） ──
            // 手札3枚（先頭3枚が初期手札）：ぱっち少年(コスト2/攻2/HP4)＋魔法使いぱっちトークン×2、残りも魔法使いぱっちトークン。
            // AttackBasics は攻撃で完了するため、デッキ切れで勝負がつかないよう枚数を多めに確保する
            // （BasicLoop はターン終了で即クリアするのでデッキ枚数は問わない）。
            public static readonly string[] BasicPlayerDeckIds =
            {
                "C1001", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // CPU は手札の先頭が速攻持ち（C1009 剛腕のぱっち・白/コスト2/速攻）。これを出して即デッキ攻撃すると
            // 攻撃したキャラがタップ（横向き）するため、次のプレイヤーの番に「攻撃できる対象」ができる。
            // （守護には触れず、タップの仕組みだけで攻撃を教える）
            // 後攻＝初期手札5枚＋ターンドローでデッキが尽きると、プレイヤーが相手デッキを攻撃して
            // デッキ切れ勝利できてしまう（＝攻撃の手順を学ぶ前に終わる）。それを防ぐため枚数を多めに確保する。
            public static readonly string[] BasicCpuDeckIds =
            {
                "C1009", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
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

            // 速攻だけは「出したターンに攻撃」を見せるため、手札の先頭に速攻キャラ（C1009・白・コスト2）を入れる。
            public static readonly string[] HastePlayerDeckIds =
            {
                "C1009", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };

            // ── CardReading（カードの見方） ──
            // 戦闘はしない。手札・デッキはすべてキーワード能力を持つ実在キャラにする（トークンは入れない）。
            // アイコン（コスト/属性/攻撃力/体力/キーワード）と詳細モーダルの読み方を学ぶため、
            // 守護=C1005 / 飛行=C5003 / 速攻=C1009 / 防人=C3007 / 強襲=C3006 / 必殺=C7006 を巡回させる。
            // 先頭は解説でハイライトする守護の C1005。END を何度か押してもデッキ切れで負けないよう多めに確保する。
            public static readonly string[] CardReadingPlayerDeckIds =
            {
                "C1005", "C5003", "C1009",
                "C3007", "C3006", "C7006", "C1005", "C5003", "C1009",
                "C3007", "C3006", "C7006", "C1005", "C5003", "C1009",
                "C3007", "C3006", "C7006", "C1005", "C5003", "C1009",
                "C3007", "C3006", "C7006", "C1005", "C5003", "C1009",
            };

            public static readonly string[] CardReadingCpuDeckIds =
            {
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
                "C1011", "C1011", "C1011", "C1011", "C1011", "C1011",
            };
        }

        // E3002 が付与する勝利点。プリセット値（あと何点で勝てるか）の計算に使う。
        private const int VictoryPointsTutorialGain = 2;

        private string[] TutorialPlayerDeckIds()
        {
            switch (_tutorialId)
            {
                case TutorialId.CardReading:
                    return TutorialScript.CardReadingPlayerDeckIds;
                case TutorialId.DeckOutWin:
                    return TutorialScript.DeckOutPlayerDeckIds;
                case TutorialId.VictoryPointsWin:
                    return TutorialScript.VictoryPointsPlayerDeckIds;
                case TutorialId.HasteKw:
                    return TutorialScript.HastePlayerDeckIds;
                case TutorialId.GuardianKw:
                case TutorialId.FlyingKw:
                case TutorialId.SakimoriKw:
                case TutorialId.AssaultKw:
                case TutorialId.NoDeckAttackKw:
                case TutorialId.DeadlyKw:
                    return TutorialScript.KeywordFillerDeckIds;
                case TutorialId.AttackBasics:
                case TutorialId.BasicLoop:
                default:
                    return TutorialScript.BasicPlayerDeckIds;
            }
        }

        private string[] TutorialCpuDeckIds()
        {
            switch (_tutorialId)
            {
                case TutorialId.CardReading:
                    return TutorialScript.CardReadingCpuDeckIds;
                case TutorialId.DeckOutWin:
                    return TutorialScript.DeckOutCpuDeckIds;
                case TutorialId.VictoryPointsWin:
                    return TutorialScript.VictoryPointsCpuDeckIds;
                case TutorialId.GuardianKw:
                case TutorialId.HasteKw:
                case TutorialId.FlyingKw:
                case TutorialId.SakimoriKw:
                case TutorialId.AssaultKw:
                case TutorialId.NoDeckAttackKw:
                case TutorialId.DeadlyKw:
                    return TutorialScript.KeywordFillerDeckIds;
                case TutorialId.AttackBasics:
                case TutorialId.BasicLoop:
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
                || _tutorialId == TutorialId.NoDeckAttackKw
                || _tutorialId == TutorialId.DeadlyKw;
        }

        // 進行ステップ：0=キャラを出す / 1=ターン終了 / 2=相手の番（タップ） / 3=攻撃 / 4=完了
        // BasicLoop は 1（ターン終了）でクリア。AttackBasics は 2→3 と進めて 3（攻撃）でクリア。
        private int _tutorialStep;
        private bool _tutorialCpuActed;
        private CardView _tutorialHeroCard;
        // CompleteTutorial で立てる。勝敗オーバーレイを「YOU WIN」ではなく「チュートリアル完了」表示にするためのフラグ。
        // 勝ち方（デッキ切れ/勝利点）は winReason 付きで終わるためこのフラグは使わない。
        private bool _tutorialCompleted;
        // FailTutorial で立てる。誤った操作（例: 飛行チュートリアルで守護持ちを攻撃）をしたとき、
        // 勝敗オーバーレイを「チュートリアル失敗」表示にするためのフラグ。
        private bool _tutorialFailed;

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
            else if (_tutorialId == TutorialId.VictoryPointsWin)
            {
                PresetVictoryPointsTutorial();
            }
            else if (_tutorialId == TutorialId.AttackBasics)
            {
                PresetAttackBasicsTutorial();
            }
            else if (IsKeywordTutorial())
            {
                PresetKeywordTutorial();
            }
        }

        // 攻撃の仕方チュートリアル：相手の場に「アンタップ（縦向き）の的」を1体置いておく。
        // 相手の番に速攻キャラがデッキ攻撃して横向き（タップ）の的ができるため、
        // 「アンタップには攻撃できない／タップには攻撃できる」を同じ盤面で見比べられる。
        private void PresetAttackBasicsTutorial()
        {
            PresetCharacter("C1011", isOpponent: true, tapped: false);
        }

        // キーワード能力チュートリアルの盤面をセットする。攻撃役と「的」を置き、必要なら相手キャラをタップ状態にする。
        // 使うカードはそのキーワードを実際に持つ既存カード（守護=C1005 / 飛行=C5003 / 防人=C3007 / 強襲・デッキ攻撃×=C3006 / 必殺=C7006）。
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
                    // 速攻キャラは手札から出す（C1009）。相手にタップ済みの的を置く。
                    PresetCharacter("C1011", isOpponent: true, tapped: true);
                    break;
                case TutorialId.FlyingKw:
                    _tutorialHeroCard = PresetCharacter("C5003", isOpponent: false, tapped: false);    // 飛行
                    PresetCharacter("C1005", isOpponent: true, tapped: true);                          // 守護（無視できる）
                    PresetCharacter("C1011", isOpponent: true, tapped: true);   // 守護の奥（飛行で狙える）
                    break;
                case TutorialId.SakimoriKw:
                    _tutorialHeroCard = PresetCharacter("C5003", isOpponent: false, tapped: false);    // 飛行
                    PresetCharacter("C3007", isOpponent: true, tapped: true);   // 防人（飛行は優先攻撃）
                    PresetCharacter("C1011", isOpponent: true, tapped: true);                          // 防人がいると狙えない
                    break;
                case TutorialId.AssaultKw:
                    _tutorialHeroCard = PresetCharacter("C3006", isOpponent: false, tapped: false);    // 強襲
                    PresetCharacter("C1011", isOpponent: true, tapped: false);  // 未タップ（強襲で狙える）
                    break;
                case TutorialId.NoDeckAttackKw:
                    _tutorialHeroCard = PresetCharacter("C3006", isOpponent: false, tapped: false);    // デッキ攻撃×
                    PresetCharacter("C1011", isOpponent: true, tapped: true);   // 代わりに攻撃する的
                    break;
                case TutorialId.DeadlyKw:
                    _tutorialHeroCard = PresetCharacter("C7006", isOpponent: false, tapped: false);    // 必殺（攻1）
                    PresetCharacter("C2008", isOpponent: true, tapped: true);   // HP8 の壁（通常攻撃では1回で倒せない）
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
            if (_tutorialId == TutorialId.CardReading)
            {
                // step 0：手札カードのアイコンの読み方を解説し、先頭カードを光らせてクリックを促す。
                // （step 0 のまま END を押しても完了せず、次の自分の番でここへ戻って再表示される）
                if (_tutorialStep == 0)
                {
                    ShowCoach("カードの見方を覚えよう。\n手札のカードの左側にはアイコンが並んでいるよ。\n一番上のアイコンが【コスト】（出すのに必要な手札の枚数）、その下が攻撃力、さらにその下がHP\nそれ以降はそのカードが持つ【キーワード能力】だよ。\n光っているカードをクリックして、くわしい情報を見てみよう！");
                    HighlightHandCard("C1005");
                }
                return;
            }

            if (_tutorialId == TutorialId.DeckOutWin)
            {
                ShowCoach("勝ち方のひとつ「デッキ切れ」を体験しよう。\n相手のデッキをマイナスにすると勝ち（0枚では勝ちにならない）！相手の【デッキ】（左上のカードの山）へ自分のキャラから矢印をドラッグして、デッキに攻撃してとどめを刺そう！\nデッキに攻撃したキャラの攻撃力の値だけ相手のデッキを上から破棄できるよ");
                ClearHighlights();
                return;
            }

            if (_tutorialId == TutorialId.VictoryPointsWin)
            {
                ShowCoach("勝ち方のひとつ「勝利点」を体験しよう。\n勝利点が20点になったら勝ち！あなたは今18点（左下に現在の勝利点が表示される）。手札の光っているカードを使って勝利点を得て、20点にしよう");
                HighlightHandCard("E3002");
                return;
            }

            if (IsKeywordTutorial())
            {
                TutorialBeginKeywordPhase();
                return;
            }

            if (_tutorialId == TutorialId.AttackBasics)
            {
                switch (_tutorialStep)
                {
                    case 0:
                        ShowCoach("攻撃の仕方を体験しよう。\nまずは攻撃役にする手札の緑に光っているキャラを、自分の場（画面中央の下半分）へドラッグして出してみよう");
                        HighlightHandCard("C1001");
                        break;
                    case 3:
                        // 攻撃対象は標準のオレンジハイライト（attack-target-char）に任せ、緑枠は出さない。
                        ShowCoach("さっき出したキャラは召喚酔いが解けて攻撃できるようになった！\nただしアンタップ（縦向き）の相手キャラには攻撃できない。タップ（横向き）になっている相手キャラへ自分のキャラから矢印をドラッグして攻撃しよう。\n攻撃力の値だけ体力を減らせるよ。攻撃しても反撃ダメージは受けないよ");
                        ClearHighlights();
                        break;
                }
                return;
            }

            // BasicLoop（カードの使い方とターン終了だけ）
            if (_tutorialStep == 0)
            {
                ShowCoach("ようこそ！Patchiカードゲームの基本を体験しよう。\nまずは手札の緑に光っているキャラカードを、自分の場（画面中央の下半分）へドラッグして出してみよう");
                HighlightHandCard("C1001");
            }
        }

        // キーワード能力チュートリアルのメインフェーズ開始時の案内。
        private void TutorialBeginKeywordPhase()
        {
            switch (_tutorialId)
            {
                case TutorialId.GuardianKw:
                    ShowCoach("「守護（しゅご）」を体験しよう。\n相手に守護を持つキャラがいる間は、守護を持つキャラにしか攻撃できない。\n守護を持たないキャラへ攻撃できないことを確認してから守護を持つキャラを攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.HasteKw:
                    if (_tutorialHeroCard != null)
                    {
                        ShowCoach("速攻はキャラを出したターンにそのまま攻撃できる。\n相手キャラへ自分のキャラから矢印をドラッグして攻撃しよう！");
                        ClearHighlights();
                    }
                    else
                    {
                        ShowCoach("「速攻（そっこう）」を体験しよう。\n速攻持ちは出したターンからすぐ攻撃できる。\n手札の枠が緑に光るキャラを場へ出そう（コストに手札を2枚使う）");
                        HighlightHandCard("C1009");
                    }
                    break;
                case TutorialId.FlyingKw:
                    ShowCoach("「飛行（ひこう）」を体験しよう。\n飛行は相手の守護を持つキャラを無視して攻撃できる。\n守護を持っていない相手キャラへ攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.SakimoriKw:
                    ShowCoach("「防人（さきもり）」を体験しよう。\n相手に防人がいると、飛行を持つキャラは防人を優先して攻撃しないといけない。\n防人でないキャラへ攻撃できないことを確認してから、防人のキャラへ攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.AssaultKw:
                    ShowCoach("「強襲（きょうしゅう）」を体験しよう。\n強襲は、まだタップしていない（縦向きの）相手キャラにも攻撃できる。\nタップしていない相手キャラへ攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.NoDeckAttackKw:
                    ShowCoach("「デッキ攻撃×」を体験しよう。\nこの能力を持つキャラは相手デッキを直接攻撃できない。\nデッキへ攻撃できないことを確認してから代わりに相手キャラへ攻撃しよう！");
                    ClearHighlights();
                    break;
                case TutorialId.DeadlyKw:
                    ShowCoach("「必殺（ひっさつ）」を体験しよう。\n必殺を持つキャラが相手キャラを攻撃すると、ダメージ計算をせず相手を破壊する。\n相手はHP8の大型キャラだけど、こちらの攻撃力はたったの1。それでも必殺なら一撃で倒せる！\n相手キャラを攻撃してみよう！");
                    ClearHighlights();
                    break;
            }
        }

        // 基本チュートリアル：キャラを場へドロップしてコスト選択が始まったときに呼ぶ。
        // 「出す」吹き出しから「コストを払う」吹き出しへ切り替える（2段階に分割）。
        private void TutorialOnLocalStagedCost()
        {
            if ((_tutorialId == TutorialId.BasicLoop || _tutorialId == TutorialId.AttackBasics) && _tutorialStep == 0)
            {
                // AutoOk（自動OK）が ON なら、必要枚数を選んだ時点で自動確定するため「OKを押す」案内は出さない。
                bool autoOk = _optionModel.AutoOk.CurrentValue;
                ShowCoach(autoOk
                    ? "いいね！キャラを出すにはコストを払うよ。\nこのカードのコストは2だから手札のカードを2枚クリックして選ぼう"
                    : "いいね！キャラを出すにはコストを払うよ。\nこのカードのコストは2だから手札のカードを2枚クリックして選んでから【OK】ボタンを押そう");
                ClearHighlights();
            }
        }

        // カードの見方チュートリアル：手札カードをクリックしてカード詳細モーダルを開いたときに呼ぶ。
        // step 0（解説中）→ step 1（詳細を開いた＝閉じ待ち）へ進める。詳細の解説はモーダルと重ならないよう、
        // 閉じた後（TutorialOnLocalCardDetailClosed）に出す。
        private void TutorialOnLocalCardDetailOpened()
        {
            if (_tutorialId != TutorialId.CardReading || _tutorialStep != 0)
            {
                return;
            }
            _tutorialStep = 1;
        }

        // カードの見方チュートリアル：一度開いた詳細を閉じたときに呼ぶ（CardDetailModal.OnHidden 経由）。
        // step 1（閉じ待ち）→ step 2（END 待ち）へ進め、詳細画面の読み方を解説して END をハイライトする。
        private void TutorialOnLocalCardDetailClosed()
        {
            if (_tutorialId != TutorialId.CardReading || _tutorialStep != 1)
            {
                return;
            }
            _tutorialStep = 2;
            ShowCoach("今見たのが【カード詳細】だよ。\nコスト・攻撃力・体力に加えて、キーワード能力や効果、フレーバーテキストまで見れる。\nキーワード能力のアイコンをクリックすると、その能力の説明が見れるよ。\n確認が終わったら右側の【END】ボタンを押してチュートリアル完了！");
            Highlight(_endButton);
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
                    ShowCoach("ナイス！キャラを場に出せたね。\nカードの詳細はカードをクリックすると確認できるよ\nこれであなたの最初のターンの目的は完了。右側の【END】ボタンを押してターンを終了しよう");
                    Highlight(_endButton);
                }
                return;
            }

            if (_tutorialId == TutorialId.AttackBasics)
            {
                if (_tutorialStep == 0 && action._actionType == MainPhaseActionType.PlaceChar)
                {
                    _tutorialStep = 1;
                    _tutorialHeroCard = action._card;
                    ShowCoach("出せたね！でも出したばかりのキャラは「召喚酔い」で、このターンはまだ攻撃できないんだ。\n攻撃できるのは次の自分のターンから。右側の【END】ボタンを押してターンを終了しよう");
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
                    // 飛行チュートリアルで守護持ちを攻撃したら失敗（飛行は守護を無視して奥を狙うのが正解）。
                    if (IsTutorialForbiddenAttackTarget(action._target))
                    {
                        FailTutorial();
                    }
                    else
                    {
                        CompleteTutorial();
                    }
                }
                return;
            }

            // 勝ち方（デッキ切れ・勝利点）はエンジンが勝利を出すため何もしない。
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

        // チュートリアルで攻撃してはいけない相手キャラかどうか。
        // 飛行チュートリアルは「守護を無視して奥のキャラを攻撃する」ことを学ばせるため、守護持ちキャラは
        // 攻撃対象として強調せず（HighlightAttackTargets でスキップ）、攻撃したら失敗にする。
        private bool IsTutorialForbiddenAttackTarget(CardView enemyChar)
        {
            return _isTutorial && _tutorialId == TutorialId.FlyingKw && enemyChar != null && enemyChar.HasGuardian;
        }

        // チュートリアルの失敗処理：勝敗オーバーレイ（チュートリアル失敗）を出して終了する。
        private void FailTutorial()
        {
            if (_isGameOver)
            {
                return;
            }
            _tutorialStep = 4;
            _tutorialFailed = true;
            _isGameOver = true;
            OnGameEnd(playerWins: false);
        }

        // 自分がパス（ターン終了）したときに呼ぶ。
        private void TutorialOnLocalPass()
        {
            // CardReading は詳細を開いて閉じ、解説を見た後（step 2）に END でクリア。
            // それ以前（step 0/1）は何もせず、次の自分の番で再び解説に戻る。
            if (_tutorialId == TutorialId.CardReading && _tutorialStep == 2)
            {
                CompleteTutorial();
                return;
            }

            // BasicLoop は「カードを出す→ターン終了」までが目的。ターン終了でそのままクリアする。
            if (_tutorialId == TutorialId.BasicLoop && _tutorialStep == 1)
            {
                CompleteTutorial();
                return;
            }

            // AttackBasics はターン終了後、相手の番（速攻でデッキ攻撃＝タップ）へ進める。
            if (_tutorialId == TutorialId.AttackBasics && _tutorialStep == 1)
            {
                _tutorialStep = 2;
                ClearHighlights();
            }
        }

        // ─── 台本どおりに動く相手（チュートリアル専用 CPU ループ） ──────────
        private async UniTask RunTutorialOpponentMainLoopAsync(CancellationToken ct)
        {
            // AttackBasics のみ、相手が速攻キャラ（C1009）を出してあなたのデッキを攻撃する。
            // 攻撃したキャラはタップ（横向き）するため、次のプレイヤーの番に「攻撃できる対象」ができる。
            // （守護には触れず、タップの仕組みだけで攻撃を教える）
            if (_tutorialId == TutorialId.AttackBasics && _tutorialStep == 2 && !_tutorialCpuActed)
            {
                _tutorialCpuActed = true;
                ShowCoach("相手の番だよ。相手はキャラを出して、あなたのデッキを攻撃してきた。\n攻撃したキャラは「タップ」（横向き）になるんだ");
                ClearHighlights();

                IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                if (cpuHand.Count > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                    await ExecuteCpuMainActionAsync(
                        new MainPhaseAction { _actionType = MainPhaseActionType.PlaceChar, _card = cpuHand[0] },
                        ct);

                    // 出したキャラ（速攻）であなたのデッキを攻撃させ、相手キャラをタップ状態にする。
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
