# カード効果（EventType）と設定方法

カードの効果は `EventType`（[EffectType.cs](../Assets/Scripts/Main/Card/EffectType.cs)。ファイル名は `EffectType.cs` だが enum 名は `EventType`）で表す。

- **イベントカード**: `EventType` + `EventValue`（必要なら `EventValue2`）で設定する（プレイ時に即時解決）
- **キャラカード**: `EffectTrigger`（発動タイミング）+ `EffectType`（イベントと共通の `EventType`）+ `EffectValue`（必要なら `EffectValue2`）で設定する

> 値が2つ必要な効果（例: SummonChar の「ID」と「体数」）のために、汎用の2つ目の数値 `EventValue2` / `EffectValue2` がある。使わない効果では 0。

> **勝利点付帯値（`VictoryPointBonus`）**: `EventType` の効果とは独立して、全カード共通で「勝利点を N 得る」付帯値を持てる（`CardData.VictoryPointBonus`）。効果解決のタイミング（キャラはトリガー時、イベントはプレイ／OnTurnStart 時）に、効果（EventType）とあわせて発動側へ加算される。0 のときは加算なし（演出も出ない）。「効果＋勝利点」（例: 全回復しつつ勝利点2）も「勝利点を得るだけ」（`EventType=None` + 付帯値）もこれ1つで表現する。加点は **緑属性カード**で設定すること（勝利点は共通の勝利条件だが、加点する手段は緑カードに限る設計）。**緑属性以外のカードでは付帯値は常に 0 に固定される**（getter が 0 を返し、エディタの SO 検証でも 0 に書き戻される）。

効果の説明文は `Description` に設定し、カード詳細モーダルに表示される。手書きするほか、カードエディタの **「自動生成」** ボタンで現在の発動タイミング・効果種別・値・属性・勝利点付帯値から生成できる（対応表は下記「効果テキストの自動生成」）。`Description` とは別に世界観テキスト用の `Flavor Text`（`FlavorText`）も全カード共通で設定でき、詳細モーダルの最下部に斜体で表示される（効果には影響しない）。

---

## 効果一覧

| EventType | 効果 | EventValue の意味 |
|---|---|---|
| None | 効果なし | － |
| Draw | 発動プレイヤーが指定枚数デッキ上から手札に加える。デッキが 0 枚になった時点で残りのドローを中断（ゲームオーバーにはならない） | ドロー枚数 |
| BanishChar | 相手フィールドの先頭キャラ（`Characters[0]`）を墓地へ送る。相手フィールドにキャラがいない場合は空振り | 使用しない（0 固定） |
| Recover | 発動プレイヤー自身の墓地の**上から**指定枚数を取り出し、自デッキに加えてシャッフルする。墓地枚数が指定枚数未満の場合は存在する全枚数を回収する | 回収枚数 |
| Switch | 発動プレイヤー自身のフィールドのキャラを1体選んで（複数いる場合はクリックで選択）手札に戻し、手札からキャラカードを1枚コストを払って配置する。フィールドにキャラがいない場合は効果なし | 使用しない（0 固定） |
| Evolve | 自分のフィールドのキャラを1体選んで（複数いる場合はクリックで選択）墓地に送り、手札から犠牲キャラより高コストのキャラカードを1枚コストなしで配置する。フィールドにキャラがいないまたは手札に適格カードがない場合は何もしない。配置時にエフェクト再生 | 使用しない（0 固定） |
| CostBoost | 通常プレイ時は**無効果**。手札からコストとして支払うときに、このカードを EventValue 分のコストとして数える（コスト倍化）。**属性連動**：このカードの属性がプレイするカードの属性と一致するときのみ EventValue 分、それ以外の属性のコストには通常どおり1（白も一般属性扱い）。例: 赤の CostBoost(2) は赤カードのコストに2・青/白などには1、白の CostBoost は白カードのコストにのみ2。キャラに付ける場合は `CharacterEffectTrigger.OnUsedAsCost` と併用、イベントは `EventType=CostBoost` 単体で判定 | コスト換算値（例: 2） |
| DamageAllEnemies | 発動側から見た敵フィールドのキャラ全員に EventValue 分のダメージを同時に与え、HP が 0 以下になったキャラを破壊する。敵フィールド中央に AoE パーティクル演出を再生（敵キャラが0体でも演出は再生） | ダメージ量 |
| DamageEnemy | 発動側から見た敵キャラを**値1体**選び、それぞれに**値2分**のダメージを同時に与え、HP が 0 以下なら破壊する。対象はプレイヤーがクリックで選択（選択中は金枠ハイライト `selectable-char`、選択済みは赤枠 `selected-char`、残り体数をトースト表示）。対象数が敵の数以上なら全員が対象（選択不要）・0体なら空振り。CPU は攻撃力上位を狙う。オンラインは対象をフィールドのインデックス配列で同期（`NGS_DamageTarget`） | 値1=対象数 / 値2=ダメージ量 |
| SummonChar | 発動側の自フィールドに、指定キャラ（**値1**=キャラIDの数字部分→"C{番号}"。例 1001→C1001）を**値2**体（未設定=0 は1体）新規生成して配置する（手札・デッキは消費しない）。召喚キャラの OnEnter も発動する。フィールドが9体（`FieldView.MaxCharacters`）で打ち切り（OnEnter 連鎖もここで自然停止）。存在しない／キャラ以外のIDは空振り | 値1=召喚キャラID数字 / 値2=体数 |
| GainVPPerGreenGrave | 発動した側の墓地にある**緑属性カードの枚数**だけ、自分の勝利点に加算する（演出は勝利点付帯値と同じ MedalIcon フロート＋加点演出）。墓地に緑カードがなければ加点 0。イベントカードは効果解決後に墓地へ送られるため、プレイしたそのカード自身は枚数に含まれない | 使用しない（0 固定） |
| NextCardCostFree | 発動した側が**次にプレイするカード1枚のコストを0にする**（使うまで持続。ターンをまたいでも次の1枚に適用）。発動カード上に「コスト0」フロート表示。コスト0のカードに使うと無駄に消費される点に注意 | 使用しない（0 固定） |
| Bounce | 発動側から見た敵キャラを**値（値1）体**選び、**所有者（相手）の手札へ戻す**。対象はプレイヤーがクリックで選択（DamageEnemy と同じ `selectable-char` / `selected-char` ハイライト・残り体数トースト）。対象数が敵の数以上なら全員が対象（選択不要）・0体なら空振り。CPU は攻撃力上位を狙う。オンラインは対象をフィールドのインデックス配列で同期（`NGS_DamageTarget`）。戻ったキャラは相手の手札から再びプレイできる | 値1=戻す体数 |
| BounceAll | 発動側から見た敵フィールドのキャラ**全員**を**所有者（相手）の手札へ戻す**（Bounce の全員対象版）。対象選択は不要（全員確定なのでオンラインも追加同期なし）。敵キャラ0体なら空振り。Bounce と同じく相手の手札へ戻すキャラは裏向き・自分の手札へ戻るキャラは表向き | 使用しない（0 固定） |
| ExtraTurn | 発動した（アクティブな）プレイヤーが、相手にターンを渡さず**もう一度自分のターン**（ターン開始時効果→ドロー→メインフェーズ）を行う。発動時にカード上へ「もう一度！」フロート表示。**アクティブプレイヤーが発動したときのみ有効**（相手ターン中の OnDestroy 等では発動しない）。1ターン中に複数回発動しても追加ターンは1回（フラグ管理）。各ターンで発動すれば連続して継続する | 使用しない（0 固定） |
| HealAllAllies | 発動側の**自フィールドのキャラ全員**の HP を EventValue 分回復する（最大HP=元のHPでクランプ）。**EventValue=0 のときは最大HPまで全回復**。自キャラが0体なら空振り。各キャラの HP ラベルが緑にパルスする演出 | 回復量（0=全回復） |
| DrawSkipNext | 発動プレイヤーが指定枚数ドローし（Draw と同じ。デッキ0枚で中断）、その代わり**そのプレイヤーの次のドローフェーズを1回スキップ**する（ドロー0枚にして消費。スキップ時は「ドロースキップ」トースト表示）。スキップ予約はターンをまたいで次に来る自分のドローフェーズで消費される（ExtraTurn 中でも「次に来るドロー」を飛ばす） | ドロー枚数 |
| DrawNextTurnStart | 発動時には引かず（「次ターン DRAW X」フロート表示のみ）、**そのプレイヤーの次のターン開始時のドローフェーズで通常ドローに上乗せして指定枚数を追加ドロー**する。複数回発動すると枚数は累積する。デッキが尽きた時点で中断 | ドロー枚数 |

> 「勝利点を固定値で得るだけ」のカードは `EventType=None` ＋ 勝利点付帯値（`VictoryPointBonus`）で作る（旧 `GainVictoryPoints` は撤去・付帯値へ統合。enum の整数 11 は欠番）。
> `AtkBoost` / `DefBoost` / `Negate` は enum に定義のみで未実装。

---

## 設定方法

> SO のインスペクターで直接編集するほか、メニュー **`Card → カードエディタ`**（[CardEditorWindow.cs](../Assets/Scripts/Editor/CardEditorWindow.cs)）で全属性のカードを横断して検索・編集・追加・削除できる。EventType に応じて値1/値2 の意味がラベル・ヒント表示され、画像は ObjectField で割り当てられる。採番ルールを変えた後は同ウィンドウの「ID再採番」で全カードを一括で振り直せる（旧 ID を参照する SummonChar 値・保存デッキは別途修正が必要）。効果テキスト（`Description`）は「自動生成」ボタンで下記ルールから生成できる。

### 効果テキストの自動生成

カードエディタの「自動生成」ボタンは、`発動タイミング + 効果本体 +（勝利点）` を「、」で連結して `Description` を生成する（守護/速攻/飛行フラグはアイコン表示があるためテキストに含めない）。値プレースホルダは **n=値1 / m=値2**、`{属性}` はカードの属性名。生成ロジックは [CardEditorWindow.cs](../Assets/Scripts/Editor/CardEditorWindow.cs) の `BuildDescription` / `EffectBody`。

発動タイミング（接頭辞）

| トリガー | テキスト |
|---|---|
| キャラ OnEnter / OnAttack / OnDestroy / OnUsedAsCost / OnTurnStart / OnAttacked / OnKill | 場に出た時 / 攻撃した時 / 破壊された時 / コストとして使用した時 / 自分のターン開始時 / 攻撃された時 / 相手キャラを撃破した時 |
| キャラ None・イベント OnPlay | （接頭辞なし） |
| イベント OnTurnStart | 自分のターン開始時に毎ターン |

効果本体（EventType）

| EventType | テキスト |
|---|---|
| None / AtkBoost / DefBoost / Negate | （空文字） |
| Draw | カードをn枚引く |
| BanishChar | 相手の先頭キャラを破壊する |
| Recover | 墓地から上のn枚をデッキに戻す |
| Switch | 自分のキャラ1体を手札に戻し、手札からキャラを1体配置する |
| Evolve | 自分のキャラ1体を生贄にして、より高コストのキャラをコストなしで配置する |
| CostBoost | {属性}コストn個分として扱う |
| DamageAllEnemies | 相手キャラ全体にnダメージを与える |
| GainVPPerGreenGrave | 墓地にある緑カードの数だけ勝利点を得る |
| DamageEnemy | 相手キャラn体にmダメージを与える |
| SummonChar | 「{召喚カード名}」をm体召喚する（ID `C{n}` から名前解決。m=0は1体） |
| NextCardCostFree | 次に使うカード1枚のコストを0にする |
| Bounce | 相手キャラn体を持ち主の手札に戻す |
| BounceAll | 相手キャラ全体を持ち主の手札に戻す |
| ExtraTurn | 追加でもう1度自分のターンを行う |
| HealAllAllies | 自分のキャラ全体のHPをn回復する（n=0は「全回復する」） |
| DrawSkipNext | カードをn枚引く。次のドローを1回スキップする |
| DrawNextTurnStart | 次の自分のターン開始時にn枚多く引く |

勝利点付帯値が n>0 のとき末尾に「勝利点をn得る」を付与。効果も勝利点も無い（EventType=None かつ付帯値0）ときは空文字（接頭辞も付けない）。

> 例: OnEnter + DamageEnemy(値1=1,値2=2) →「場に出た時、相手キャラ1体に2ダメージを与える」。OnUsedAsCost + CostBoost(値1=2)・赤属性 →「コストとして使用した時、赤コスト2個分として扱う」。

### イベントカード

属性別の `EventCardSO`（`Assets/Data/{属性}/EventCards_{属性}.asset`）のリストにカードを追加し、インスペクターで設定する。

| フィールド | 説明 |
|---|---|
| Card Name / Cost / Image | 名前・コスト・画像 |
| Attribute | 属性。**SO が一括設定するためカード個別では読み取り専用（グレー表示）**。SO の `Attribute` に追従する |
| Event Type | 効果種別（上表の `EventType`） |
| Event Value | 効果の数値（上表「値の意味」の値1） |
| Event Value 2 | 2つ目の数値（SummonChar の体数など。使わない効果は 0） |
| Victory Point Bonus | 勝利点付帯値。効果（Event Type）とは独立して、プレイ／OnTurnStart 時に発動側へこの値を加算する。0 で加算なし。緑カードで設定する（緑属性以外では 0 に固定される） |
| Description | 効果説明（詳細モーダル表示用に手書き） |
| Flavor Text | フレーバーテキスト（世界観・雰囲気用。効果には影響せず、詳細モーダル最下部に斜体で表示。空欄なら非表示） |
| Trigger On Grave | ON にすると、このカードが墓地に送られたときにコストを支払わずに効果が発動する |
| Event Trigger | 発動タイミング（下表）。既定は `OnPlay` |

- ID は属性ごとに自動採番される：`E{(属性番号)×1000 + 連番}`（属性番号=白1/青2/緑3/黄4/赤5/黒6/紫7。白=`E1001`/青=`E2001`/…。`CardIdAutoAssigner`）

#### EventTrigger（[EventCardTrigger.cs](../Assets/Scripts/Main/Card/EventCardTrigger.cs)）

| Trigger | 発動タイミング |
|---|---|
| OnPlay | カードを使ったとき（プレイ時に即時解決して墓地へ送る。従来の挙動・既定） |
| OnTurnStart | 自分のターン開始時。**プレイ時は即時解決せずコストだけ払って墓地へ送り、永続イベントとして登録する**。以降、自分のターン開始時（ドロー前）に毎ターン発動し続ける（墓地から一時カードがフィールドへせり出し、効果を解決して墓地へ戻る）。除去手段はない。**登録されるのはプレイしたカードのみで、コストとして捨てた同名カードは発動しない**（登録簿で管理し墓地は走査しないため）。発動順はキャラの `OnTurnStart` の後 |

### キャラカード

属性別の `CharacterCardSO`（`Assets/Data/{属性}/CharacterCards_{属性}.asset`）のリストで設定する。

| フィールド | 説明 |
|---|---|
| Attack / Hp / Cost / Image | ステータス |
| Attribute | 属性。**SO が一括設定するためカード個別では読み取り専用（グレー表示）** |
| Effect Trigger | 発動タイミング（下表） |
| Effect Type | 効果種別（イベントと共通の `EventType`） |
| Effect Value | 効果の数値（値1） |
| Effect Value 2 | 2つ目の数値（SummonChar の体数など。使わない効果は 0） |
| Victory Point Bonus | 勝利点付帯値。効果（Effect Type）とは独立して、効果トリガー（OnEnter 等）の発動時にこの値を発動側へ加算する。0 で加算なし。緑カードで設定する（緑属性以外では 0 に固定される）。`Effect Type=None` でも値があればトリガー時に勝利点だけ得る |
| Guardian | **守護**。ON にすると、このキャラが場にいる間は相手はこのキャラ（守護持ち）にしか攻撃できない（守護以外のキャラへの攻撃は不可）。`EffectType` とは独立したフラグで、攻撃のみを制限する。カードと詳細モーダルに ShieldIcon を表示（詳細は [rules.md](rules.md)「攻撃の詳細」） |
| Haste | **速攻**。ON にすると、このキャラは召喚酔いせず、場に出したターンから攻撃できる（通常配置・召喚・Switch / Evolve のいずれの配置でも即攻撃可。1ターン1回の攻撃制限は維持）。`EffectType` とは独立したフラグ。カードと詳細モーダルに SpeedIcon を表示（詳細は [rules.md](rules.md)「攻撃回数と召喚酔い」） |
| Flying | **飛行**。ON にすると、このキャラは守護を無視して攻撃対象（相手キャラ）を選べ、かつ飛行を持つキャラからしか攻撃されない（飛行なしキャラは飛行キャラを攻撃不可）。`EffectType` とは独立したフラグで、攻撃のみに作用する。カードと詳細モーダルに FlyIcon を表示（詳細は [rules.md](rules.md)「攻撃の詳細」） |
| Description | 効果説明（詳細モーダル表示用に手書き） |
| Flavor Text | フレーバーテキスト（世界観・雰囲気用。効果には影響せず、詳細モーダル最下部に斜体で表示。空欄なら非表示） |

#### EffectTrigger（[CharacterEffectTrigger.cs](../Assets/Scripts/Main/Card/CharacterEffectTrigger.cs)）

| Trigger | 発動タイミング |
|---|---|
| None | 効果なし |
| OnEnter | 通常配置でフィールドに出した瞬間（Switch / Evolve 配置は対象外。CPU・オンライン相手の配置でも発動） |
| OnAttack | 攻撃宣言時（相手キャラ・相手デッキへの攻撃） |
| OnUsedAsCost | 手札からコストとして支払うとき（`EffectType=CostBoost` と併用してコスト倍化に使う） |
| OnDestroy | 破壊時。戦闘での撃破・`DamageEnemy` / `DamageAllEnemies` での撃破・`BanishChar` での除去で発動する（HP が 0 になって、または除去されて場から墓地へ送られた瞬間）。Evolve の生贄・Switch で手札に戻すのは対象外。破壊されたキャラを source として効果を解決する |
| OnTurnStart | 自分のターン開始時（ターン開始演出の直後・ドローフェーズの前）。このキャラが場にいる限り毎ターン1回発動する。場を離れると発動しない（出したターンは既に開始時を過ぎているため次の自分ターンから発動） |
| OnAttacked | 被攻撃時。**相手キャラの攻撃の対象になった**瞬間に発動する（攻撃された本人を source として解決）。**攻撃側 ATK 0 の盾ブロック（ダメージ0）でも発動する**（ダメージの有無は問わない）。`DamageEnemy` 等の効果ダメージでは発動しない（キャラの攻撃＝戦闘のみ）。ダメージ適用後・破壊判定の前に発動するため、回復効果で生き残ったり反撃したりできる（破壊判定は OnAttacked 後に再計算される） |
| OnKill | 撃破時。**このキャラの攻撃で相手キャラを破壊した**ときに発動する（攻撃した本人を source として解決）。**戦闘での撃破のみ**で、`DamageEnemy` / `BanishChar` 等の効果破壊では発動しない。対象の OnDestroy 解決後に発動し、反撃等で攻撃側が場を離れている場合は発動しない |

- ID は属性ごとに自動採番される：`C{(属性番号)×1000 + 連番}`（属性番号=白1/青2/緑3/黄4/赤5/黒6/紫7。白=`C1001`/青=`C2001`/…。`CardIdAutoAssigner`）

---

## 効果ごとの注意点

- **CostBoost**: キャラは `EffectTrigger=OnUsedAsCost` + `EffectType=CostBoost`、イベントは `EventType=CostBoost` 単体で判定。通常プレイ時は無効果で、コスト支払い時のみ `EventValue` 分のコストとして数える。**属性連動**：CostBoost カードの属性がプレイするカードの属性と一致するときだけ EventValue 分になり、それ以外の属性のコストには1として数える（白も一般属性扱いで、白 CostBoost は白のコストのみ倍化。コスト判定の詳細は [rules.md](rules.md)「コストシステム」）。
- **DamageAllEnemies / DamageEnemy / SummonChar / GainVPPerGreenGrave / HealAllAllies / NextCardCostFree / Bounce / BounceAll / ExtraTurn / DrawSkipNext / DrawNextTurnStart**: イベント・キャラ（OnEnter / OnAttack / OnDestroy / OnTurnStart / OnAttacked / OnKill）両方で使用可能。勝利点付帯値（`VictoryPointBonus`）も同じく両方で使用可能で、効果と同時に発動する。
- **OnAttacked / OnKill（キャラ専用の戦闘トリガー）**: いずれも `ExecuteAttackAsync`（キャラ vs キャラ戦闘）からのみ発動する。`OnAttacked` は被攻撃側が攻撃の対象になった直後（破壊判定の前）に解決され、source は攻撃された本人・所有者は攻撃側の相手。**攻撃側 ATK 0 の盾ブロック（ダメージ0）でも発動する**。`OnKill` は攻撃側が対象を撃破し、対象の OnDestroy 解決後に攻撃側が場に残っていれば解決され、source は攻撃した本人・所有者は攻撃側（ダメージ0では撃破が起きないため発動しない）。どちらも戦闘から決定的に解決されるためオンライン・CPU でも追加同期なしで対称発動する（`DamageEnemy` 等の効果ダメージでは発動しない）。
- **OnTurnStart（キャラ・イベント共通）**: 自分のターン開始時（ドロー前）に毎ターン発動。キャラは場にいる間、イベントはプレイして登録された後ずっと発動し続ける（コストとして捨てたイベントは登録されず発動しない）。発動順は「場のキャラ → 登録済みイベント」。オンラインでは盤面・登録簿が同期済みのため決定的に対称解決される（対象選択は既存の同期を流用・追加同期なし）。
- **Bounce**: 対象選択は DamageEnemy と同じ仕組み（`ResolveEnemyCharTargetsAsync` を共用。プレイヤー選択／CPU 自動／オンラインはインデックス同期）。対象キャラは所有者の手札へ戻す（相手の手札に戻す場合は裏向きで、自分の手札に戻る場合は表向き）。`EventValue` = 戻す体数（値2は不使用）。デッキは消費しないため手札が増える。
- **BounceAll**: Bounce の全員対象版。`ApplyBounceAllAsync` が敵フィールドの全数を `ApplyBounceAsync` に渡し、「対象数が敵の数以上なら全員（選択不要）」分岐を流用して全体バウンスを実現する。対象選択 UI は出ず、全員確定のためオンラインの追加同期も不要。演出は個別バウンス（対象ごとにパーティクル＋1枚ずつ戻す）と異なり、**フィールド中央で全体用エフェクト（`_bounceAllEffectPrefab`）を1度だけ再生し、全カードをまとめて同時に手札へ戻す**（`ApplyBounceAsync` の一括モード `simultaneous` を使用）。手札へ戻す挙動（裏向き／表向き）は Bounce と同じ。`EventValue` / `EventValue2` は不使用（0）。

> バウンス演出のパーティクルは `MainPresenter` の `_bounceEffectPrefab`（個別 Bounce・対象ごと）/ `_bounceAllEffectPrefab`（BounceAll・フィールド中央に1度）。各カード効果の演出は終了後に共通の余韻ディレイ（`EffectTrailingDelaySeconds` = 0.25秒）を挟んでから次の処理へ進む（[docs/effects.md](effects.md) セクション7）。
- **OnDestroy**: 破壊されたキャラの効果は、破壊が完了して墓地へ送られた後に発動する。複数体が同時に破壊された場合は破壊演出を同時再生したうえで OnDestroy を1体ずつ順番に解決する（対象選択 UI の競合を防ぐ）。効果はカードデータと同期済み盤面から決定的に解決されるため、オンラインでも両クライアントで対称に発動する（追加同期不要）。OnDestroy 効果がさらに別キャラを破壊した場合は連鎖して発動する（盤面が有限のため停止する）。
- **DamageEnemy**: **値1=対象数、値2=ダメージ**（値2が0だとダメージ0で無効果になる点に注意）。プレイヤーが敵キャラを値1体クリックで選ぶ（`selectable-char` でハイライト、選択済みは `selected-char` で赤枠）。対象数が敵の数以上なら全員が対象・0体なら空振り。選んだ全対象に同時ダメージ。オンラインでは対象をフィールドのインデックス配列で相手へ送るため、同名カードが複数いても曖昧にならない。
- **SummonChar**: 値1=召喚キャラIDの数字部分（例 1001→"C1001"。ID採番は属性別、下記「設定方法」参照）、値2=体数（0は1体）。手札・デッキを消費せず自フィールドに新規生成し、召喚キャラの OnEnter も発動する。フィールドは9体上限（`FieldView.MaxCharacters`）で、満杯になると召喚は打ち切られ OnEnter 連鎖も自然停止する（自己召喚カードでも無限ループにならない）。オンラインは召喚IDがカードデータで確定するため追加同期不要（決定的）。
- **勝利点付帯値（VictoryPointBonus）**: 加点カードは**緑属性**で作る（勝利点は共通の勝利条件だが、加点する手段は緑カードに限る設計）。効果（EventType）解決後に `ApplyVictoryPointBonusAsync` が MedalIcon 演出 ＋ `AddVictoryPoints` を共通実行する。固定値なのでオンラインでも決定的（追加同期不要）。「勝利点を得るだけ」は `EventType=None` ＋ 付帯値で表現する。**緑属性以外のカードでは付帯値は常に 0 に固定される**：`CardData.VictoryPointBonus` の getter が緑以外で 0 を返し、さらに各 SO の `OnValidate`（`EditorClampVictoryPointBonusToAttribute`）が緑以外のカードの serialized 値を 0 に書き戻す。
- **GainVPPerGreenGrave**: 加点カードは**緑属性**で作る（加点する手段が緑カードに限る点は付帯値と同じ）。加点値は固定ではなく、解決時に発動側の墓地の緑属性カード枚数（`GraveyardView.CountByAttribute(CardAttribute.Green)`）を数えて `AddVictoryPoints` に渡す。墓地は両クライアントで同期済みのため決定的に解決される（追加同期不要）。`EventValue` / `EffectValue` は不使用（0）。
- **HealAllAllies**: 発動側の自フィールド全キャラの HP を `EventValue` / `EffectValue` 分回復する（最大HP=元のHPでクランプ。`CardView.HealAsync`）。**値0は全回復**。自キャラが0体なら空振り。盤面は同期済みで決定的に解決される（追加同期不要）。勝利点付帯値と併用すれば「全回復しつつ勝利点を得る」カードになる（例: E3004 不死鳥の恵み）。
- **NextCardCostFree**: 発動側の「次の1枚無料」フラグ（`_playerNextCardFree` / `_opponentNextCardFree`）を立て、次の `PayHandCostAsync` でコスト0扱いにしてフラグ消費する（使うまで持続）。フラグは次の支払いで消費されるため Switch/Evolve の内部配置には波及しない（イベント本体プレイ時に消費済み）。オンラインは無料カードを「空の `costCardIds`」として送り相手が無料再生するため追加同期不要。EventValue は不使用（0）。
- **ExtraTurn**: 発動時に `_extraTurnPending` フラグを立て（`ApplyExtraTurnAsync`）、ターン終了時（`RunTurnAsync` 末尾）に `GameModel.RepeatTurn()` で `IsLocalTurn` を反転せず `TurnNumber` だけ加算して同じプレイヤーがもう一度ターンを行う。発動側がアクティブプレイヤー（`_gameModel.IsLocalTurn` と一致）のときのみ有効化する。オンラインは効果がカードデータ＋同期済み盤面から両クライアントで決定的に解決されるため追加のネットワークメッセージ不要だが、Pass 時の相手ドロー待ち（`_preDrawReceiveTask`）登録は ExtraTurn 保留中はスキップする（次も自分のターンが続くため）。EventValue は不使用（0）。
- **DrawSkipNext**: Draw と同じく `ApplyDrawEffectAsync` で即時ドローしたうえで、発動側のスキップフラグ（`_playerSkipNextDraw` / `_opponentSkipNextDraw`）を立てる（`SetSkipNextDraw`）。フラグは次に来るそのプレイヤーの `RunDrawPhaseAsync` で `drawCount=0` にして消費する。各クライアントは自分側（アクティブ側）のフラグを見るため drawCount は対称に 0 になり、ドロー0枚でも `SendDrawNotification` を送る既存仕様で lockstep が保たれる（追加同期不要）。EventValue / EffectValue = ドロー枚数（値2は不使用）。
- **DrawNextTurnStart**: 発動時はドローせず、発動側の予約カウント（`_playerPendingNextDraw` / `_opponentPendingNextDraw`）に EventValue を加算する（`AddPendingNextDraw`・累積）。予約は次に来るそのプレイヤーの `RunDrawPhaseAsync` で `drawCount` に上乗せして消費する。各クライアントは自分側（アクティブ側）の予約を見るため drawCount は対称に決まり、追加ドローも既存の `SendDrawNotification` lockstep でそのまま同期される（追加同期不要）。EventValue / EffectValue = ドロー枚数（値2は不使用）。`DrawSkipNext` と同居した場合はスキップで base を 0 にした後に予約分を上乗せして引く。
- 勝敗条件（共通の3条件＝デッキ切れ / 勝利点20 / キャラ8体）の挙動は [rules.md](rules.md)「勝敗条件」を参照。属性に依らず全プレイヤーに適用される。
- オンライン対戦では効果はカードデータと盤面から決定的に解決されるため、プレイ同期（`NGS_MainAction`）以外の追加同期は不要。

---

## 新しい効果を追加する（コード）

新しい `EventType` を実装する手順（enum 追加・`ApplyEventEffectAsync` / `ResolveCharacterTriggeredEffectAsync` への case 追加・演出）は [patterns.md](patterns.md)「2. 新しいイベント効果（EventType）を追加する」「2-B. キャラカードに登場時効果を追加する」「2-C. コスト支払い時に作用する受動効果」を参照。
