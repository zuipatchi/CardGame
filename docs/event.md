# カード効果（EventType）と設定方法

カードの効果は `EventType`（[EffectType.cs](../Assets/Scripts/Main/Card/EffectType.cs)。ファイル名は `EffectType.cs` だが enum 名は `EventType`）で表す。

- **イベントカード**: `EventType` + `EventValue`（必要なら `EventValue2`）で設定する（プレイ時に即時解決）
- **キャラカード**: `EffectTrigger`（発動タイミング）+ `EffectType`（イベントと共通の `EventType`）+ `EffectValue`（必要なら `EffectValue2`）で設定する

> 値が2つ必要な効果（例: SummonChar の「ID」と「体数」）のために、汎用の2つ目の数値 `EventValue2` / `EffectValue2` がある。使わない効果では 0。

効果の説明文は `Description` に手書きし、カード詳細モーダルに表示される（自動生成はしない）。

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
| CostBoost | 通常プレイ時は**無効果**。手札からコストとして支払うときに、このカードを EventValue 分のコストとして数える（コスト倍化）。**属性連動**：このカードの属性がプレイするカードの属性と一致する（またはこのカードが白属性）ときのみ EventValue 分、それ以外の属性のコストには通常どおり1。例: 赤の CostBoost(2) は赤カードのコストに2・青/白などには1、白の CostBoost は何色のコストにも2。キャラに付ける場合は `CharacterEffectTrigger.OnUsedAsCost` と併用、イベントは `EventType=CostBoost` 単体で判定 | コスト換算値（例: 2） |
| DamageAllEnemies | 発動側から見た敵フィールドのキャラ全員に EventValue 分のダメージを同時に与え、HP が 0 以下になったキャラを破壊する。敵フィールド中央に AoE パーティクル演出を再生（敵キャラが0体でも演出は再生） | ダメージ量 |
| DamageEnemy | 発動側から見た敵キャラを**値1体**選び、それぞれに**値2分**のダメージを同時に与え、HP が 0 以下なら破壊する。対象はプレイヤーがクリックで選択（選択中は金枠ハイライト `selectable-char`、選択済みは赤枠 `selected-char`、残り体数をトースト表示）。対象数が敵の数以上なら全員が対象（選択不要）・0体なら空振り。CPU は攻撃力上位を狙う。オンラインは対象をフィールドのインデックス配列で同期（`NGS_DamageTarget`） | 値1=対象数 / 値2=ダメージ量 |
| SummonChar | 発動側の自フィールドに、指定キャラ（**値1**=キャラIDの数字部分→"C{番号}"。例 1001→C1001）を**値2**体（未設定=0 は1体）新規生成して配置する（手札・デッキは消費しない）。召喚キャラの OnEnter も発動する。フィールドが9体（`FieldView.MaxCharacters`）で打ち切り（OnEnter 連鎖もここで自然停止）。存在しない／キャラ以外のIDは空振り | 値1=召喚キャラID数字 / 値2=体数 |
| GainVictoryPoints | 発動した側の勝利点（緑属性の勝利条件）に EventValue 分を加算する。20到達でそのプレイヤーが勝利。発動カードの上に MedalIcon フロート → 勝利点カウンターで加点演出 | 加算する勝利点 |
| NextCardCostFree | 発動した側が**次にプレイするカード1枚のコストを0にする**（使うまで持続。ターンをまたいでも次の1枚に適用）。発動カード上に「コスト0」フロート表示。コスト0のカードに使うと無駄に消費される点に注意 | 使用しない（0 固定） |
| Bounce | 発動側から見た敵キャラを**値（値1）体**選び、**所有者（相手）の手札へ戻す**。対象はプレイヤーがクリックで選択（DamageEnemy と同じ `selectable-char` / `selected-char` ハイライト・残り体数トースト）。対象数が敵の数以上なら全員が対象（選択不要）・0体なら空振り。CPU は攻撃力上位を狙う。オンラインは対象をフィールドのインデックス配列で同期（`NGS_DamageTarget`）。戻ったキャラは相手の手札から再びプレイできる | 値1=戻す体数 |

> `AtkBoost` / `DefBoost` / `Negate` は enum に定義のみで未実装。

---

## 設定方法

### イベントカード

属性別の `EventCardSO`（`Assets/Data/{属性}/EventCards_{属性}.asset`）のリストにカードを追加し、インスペクターで設定する。

| フィールド | 説明 |
|---|---|
| Card Name / Cost / Image | 名前・コスト・画像 |
| Attribute | 属性。**SO が一括設定するためカード個別では読み取り専用（グレー表示）**。SO の `Attribute` に追従する |
| Event Type | 効果種別（上表の `EventType`） |
| Event Value | 効果の数値（上表「値の意味」の値1） |
| Event Value 2 | 2つ目の数値（SummonChar の体数など。使わない効果は 0） |
| Description | 効果説明（詳細モーダル表示用に手書き） |
| Trigger On Grave | ON にすると、このカードが墓地に送られたときにコストを支払わずに効果が発動する |
| Event Trigger | 発動タイミング（下表）。既定は `OnPlay` |

- ID は属性ごとに自動採番される：`E{(属性番号)×1000 + 連番}`（赤=`E1001`/青=`E2001`/…。`CardIdAutoAssigner`）

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
| Guardian | **守護**。ON にすると、このキャラが場にいる間は相手はこのキャラ（守護持ち）にしか攻撃できない（守護以外のキャラ・ハートへの攻撃は不可）。`EffectType` とは独立したフラグで、攻撃のみを制限する。カードと詳細モーダルに ShieldIcon を表示（詳細は [rules.md](rules.md)「攻撃の詳細」） |
| Haste | **速攻**。ON にすると、このキャラは召喚酔いせず、場に出したターンから攻撃できる（通常配置・召喚・Switch / Evolve のいずれの配置でも即攻撃可。1ターン1回の攻撃制限は維持）。`EffectType` とは独立したフラグ。カードと詳細モーダルに SpeedIcon を表示（詳細は [rules.md](rules.md)「攻撃回数と召喚酔い」） |
| Flying | **飛行**。ON にすると、このキャラは守護を無視して攻撃対象（キャラ・ハート）を選べ、かつ飛行を持つキャラからしか攻撃されない（飛行なしキャラは飛行キャラを攻撃不可）。`EffectType` とは独立したフラグで、攻撃のみに作用する。カードと詳細モーダルに FlyIcon を表示（詳細は [rules.md](rules.md)「攻撃の詳細」） |
| Description | 効果説明（詳細モーダル表示用に手書き） |

#### EffectTrigger（[CharacterEffectTrigger.cs](../Assets/Scripts/Main/Card/CharacterEffectTrigger.cs)）

| Trigger | 発動タイミング |
|---|---|
| None | 効果なし |
| OnEnter | 通常配置でフィールドに出した瞬間（Switch / Evolve 配置は対象外。CPU・オンライン相手の配置でも発動） |
| OnAttack | 攻撃宣言時（キャラ攻撃・ハート攻撃の両方に対応） |
| OnUsedAsCost | 手札からコストとして支払うとき（`EffectType=CostBoost` と併用してコスト倍化に使う） |
| OnDestroy | 破壊時。戦闘での撃破・`DamageEnemy` / `DamageAllEnemies` での撃破・`BanishChar` での除去で発動する（HP が 0 になって、または除去されて場から墓地へ送られた瞬間）。Evolve の生贄・Switch で手札に戻すのは対象外。破壊されたキャラを source として効果を解決する |
| OnTurnStart | 自分のターン開始時（ターン開始演出の直後・ドローフェーズの前）。このキャラが場にいる限り毎ターン1回発動する。場を離れると発動しない（出したターンは既に開始時を過ぎているため次の自分ターンから発動） |

- ID は属性ごとに自動採番される：`C{(属性番号)×1000 + 連番}`（赤=`C1001`/青=`C2001`/…。`CardIdAutoAssigner`）

---

## 効果ごとの注意点

- **CostBoost**: キャラは `EffectTrigger=OnUsedAsCost` + `EffectType=CostBoost`、イベントは `EventType=CostBoost` 単体で判定。通常プレイ時は無効果で、コスト支払い時のみ `EventValue` 分のコストとして数える。**属性連動**：CostBoost カードの属性がプレイするカードの属性と一致する（または CostBoost カードが白属性）ときだけ EventValue 分になり、それ以外の属性のコストには1として数える（コスト判定の詳細は [rules.md](rules.md)「コストシステム」）。
- **DamageAllEnemies / DamageEnemy / SummonChar / GainVictoryPoints / NextCardCostFree / Bounce**: イベント・キャラ（OnEnter / OnAttack / OnDestroy / OnTurnStart）両方で使用可能。
- **OnTurnStart（キャラ・イベント共通）**: 自分のターン開始時（ドロー前）に毎ターン発動。キャラは場にいる間、イベントはプレイして登録された後ずっと発動し続ける（コストとして捨てたイベントは登録されず発動しない）。発動順は「場のキャラ → 登録済みイベント」。オンラインでは盤面・登録簿が同期済みのため決定的に対称解決される（対象選択は既存の同期を流用・追加同期なし）。
- **Bounce**: 対象選択は DamageEnemy と同じ仕組み（`ResolveEnemyCharTargetsAsync` を共用。プレイヤー選択／CPU 自動／オンラインはインデックス同期）。対象キャラは所有者の手札へ戻す（相手の手札に戻す場合は裏向きで、自分の手札に戻る場合は表向き）。`EventValue` = 戻す体数（値2は不使用）。デッキは消費しないため手札が増える。
- **OnDestroy**: 破壊されたキャラの効果は、破壊が完了して墓地へ送られた後に発動する。複数体が同時に破壊された場合は破壊演出を同時再生したうえで OnDestroy を1体ずつ順番に解決する（対象選択 UI の競合を防ぐ）。効果はカードデータと同期済み盤面から決定的に解決されるため、オンラインでも両クライアントで対称に発動する（追加同期不要）。OnDestroy 効果がさらに別キャラを破壊した場合は連鎖して発動する（盤面が有限のため停止する）。
- **DamageEnemy**: **値1=対象数、値2=ダメージ**（値2が0だとダメージ0で無効果になる点に注意）。プレイヤーが敵キャラを値1体クリックで選ぶ（`selectable-char` でハイライト、選択済みは `selected-char` で赤枠）。対象数が敵の数以上なら全員が対象・0体なら空振り。選んだ全対象に同時ダメージ。オンラインでは対象をフィールドのインデックス配列で相手へ送るため、同名カードが複数いても曖昧にならない。
- **SummonChar**: 値1=召喚キャラIDの数字部分（例 1001→"C1001"。ID採番は属性別、下記「設定方法」参照）、値2=体数（0は1体）。手札・デッキを消費せず自フィールドに新規生成し、召喚キャラの OnEnter も発動する。フィールドは9体上限（`FieldView.MaxCharacters`）で、満杯になると召喚は打ち切られ OnEnter 連鎖も自然停止する（自己召喚カードでも無限ループにならない）。オンラインは召喚IDがカードデータで確定するため追加同期不要（決定的）。
- **GainVictoryPoints**: 加点カードは**緑属性**で作る（緑カードをプレイした側に勝利点表示が出現するため）。
- **NextCardCostFree**: 発動側の「次の1枚無料」フラグ（`_playerNextCardFree` / `_opponentNextCardFree`）を立て、次の `PayHandCostAsync` でコスト0扱いにしてフラグ消費する（使うまで持続）。フラグは次の支払いで消費されるため Switch/Evolve の内部配置には波及しない（イベント本体プレイ時に消費済み）。オンラインは無料カードを「空の `costCardIds`」として送り相手が無料再生するため追加同期不要。EventValue は不使用（0）。
- 勝敗に関わる属性（赤=ハート / 青=デッキ0 / 緑=勝利点20）の挙動は [rules.md](rules.md)「勝敗条件」を参照。
- オンライン対戦では効果はカードデータと盤面から決定的に解決されるため、プレイ同期（`NGS_MainAction`）以外の追加同期は不要。

---

## 新しい効果を追加する（コード）

新しい `EventType` を実装する手順（enum 追加・`ApplyEventEffectAsync` / `ResolveCharacterTriggeredEffectAsync` への case 追加・演出）は [patterns.md](patterns.md)「2. 新しいイベント効果（EventType）を追加する」「2-B. キャラカードに登場時効果を追加する」「2-C. コスト支払い時に作用する受動効果」を参照。
