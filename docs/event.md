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
| CostBoost | 通常プレイ時は**無効果**。手札からコストとして支払うときに、このカードを EventValue 分のコストとして数える（コスト倍化）。キャラに付ける場合は `CharacterEffectTrigger.OnUsedAsCost` と併用、イベントは `EventType=CostBoost` 単体で判定 | コスト換算値（例: 2） |
| DamageAllEnemies | 発動側から見た敵フィールドのキャラ全員に EventValue 分のダメージを同時に与え、HP が 0 以下になったキャラを破壊する。敵フィールド中央に AoE パーティクル演出を再生（敵キャラが0体でも演出は再生） | ダメージ量 |
| DamageEnemy | 発動側から見た敵キャラを**値1体**選び、それぞれに**値2分**のダメージを同時に与え、HP が 0 以下なら破壊する。対象はプレイヤーがクリックで選択（選択中は金枠ハイライト `selectable-char`、選択済みは赤枠 `selected-char`、残り体数をトースト表示）。対象数が敵の数以上なら全員が対象（選択不要）・0体なら空振り。CPU は攻撃力上位を狙う。オンラインは対象をフィールドのインデックス配列で同期（`NGS_DamageTarget`） | 値1=対象数 / 値2=ダメージ量 |
| SummonChar | 発動側の自フィールドに、指定キャラ（**値1**=キャラIDの数字部分→"C###"）を**値2**体（未設定=0 は1体）新規生成して配置する（手札・デッキは消費しない）。召喚キャラの OnEnter も発動する。フィールドが9体（`FieldView.MaxCharacters`）で打ち切り（OnEnter 連鎖もここで自然停止）。存在しない／キャラ以外のIDは空振り | 値1=召喚キャラID数字 / 値2=体数 |
| GainVictoryPoints | 発動した側の勝利点（緑属性の勝利条件）に EventValue 分を加算する。20到達でそのプレイヤーが勝利。発動カードの上に MedalIcon フロート → 勝利点カウンターで加点演出 | 加算する勝利点 |

> `AtkBoost` / `DefBoost` / `Negate` は enum に定義のみで未実装。

---

## 設定方法

### イベントカード

[EventCards.asset](../Assets/Data/EventCards.asset)（`EventCardSO`）のリストにカードを追加し、インスペクターで設定する。

| フィールド | 説明 |
|---|---|
| Card Name / Cost / Image / Attribute | 名前・コスト・画像・属性（`CardAttribute`） |
| Event Type | 効果種別（上表の `EventType`） |
| Event Value | 効果の数値（上表「値の意味」の値1） |
| Event Value 2 | 2つ目の数値（SummonChar の体数など。使わない効果は 0） |
| Description | 効果説明（詳細モーダル表示用に手書き） |
| Trigger On Grave | ON にすると、このカードが墓地に送られたときにコストを支払わずに効果が発動する |

- ID（`E###`）はリスト順に自動採番される（`CardIdAutoAssigner`）

### キャラカード

[CharacterCard.asset](../Assets/Data/CharacterCard.asset)（`CharacterCardSO`）のリストで設定する。

| フィールド | 説明 |
|---|---|
| Attack / Hp / Cost / Image / Attribute | ステータス |
| Effect Trigger | 発動タイミング（下表） |
| Effect Type | 効果種別（イベントと共通の `EventType`） |
| Effect Value | 効果の数値（値1） |
| Effect Value 2 | 2つ目の数値（SummonChar の体数など。使わない効果は 0） |
| Description | 効果説明（詳細モーダル表示用に手書き） |

#### EffectTrigger（[CharacterEffectTrigger.cs](../Assets/Scripts/Main/Card/CharacterEffectTrigger.cs)）

| Trigger | 発動タイミング |
|---|---|
| None | 効果なし |
| OnEnter | 通常配置でフィールドに出した瞬間（Switch / Evolve 配置は対象外。CPU・オンライン相手の配置でも発動） |
| OnAttack | 攻撃宣言時（キャラ攻撃・ハート攻撃の両方に対応） |
| OnUsedAsCost | 手札からコストとして支払うとき（`EffectType=CostBoost` と併用してコスト倍化に使う） |
| OnDestroy | 破壊時（enum 定義のみ・未実装） |

- ID（`C###`）はリスト順に自動採番される（`CardIdAutoAssigner`）

---

## 効果ごとの注意点

- **CostBoost**: キャラは `EffectTrigger=OnUsedAsCost` + `EffectType=CostBoost`、イベントは `EventType=CostBoost` 単体で判定。通常プレイ時は無効果で、コスト支払い時のみ `EventValue` 分のコストとして数える（コスト判定の詳細は [rules.md](rules.md)「コストシステム」）。
- **DamageAllEnemies / DamageEnemy / SummonChar / GainVictoryPoints**: イベント・キャラ（OnEnter / OnAttack）両方で使用可能。
- **DamageEnemy**: **値1=対象数、値2=ダメージ**（値2が0だとダメージ0で無効果になる点に注意）。プレイヤーが敵キャラを値1体クリックで選ぶ（`selectable-char` でハイライト、選択済みは `selected-char` で赤枠）。対象数が敵の数以上なら全員が対象・0体なら空振り。選んだ全対象に同時ダメージ。オンラインでは対象をフィールドのインデックス配列で相手へ送るため、同名カードが複数いても曖昧にならない。
- **SummonChar**: 値1=召喚キャラIDの数字部分（7→"C007"）、値2=体数（0は1体）。手札・デッキを消費せず自フィールドに新規生成し、召喚キャラの OnEnter も発動する。フィールドは9体上限（`FieldView.MaxCharacters`）で、満杯になると召喚は打ち切られ OnEnter 連鎖も自然停止する（自己召喚カードでも無限ループにならない）。オンラインは召喚IDがカードデータで確定するため追加同期不要（決定的）。
- **GainVictoryPoints**: 加点カードは**緑属性**で作る（緑カードをプレイした側に勝利点表示が出現するため）。
- 勝敗に関わる属性（赤=ハート / 青=デッキ0 / 緑=勝利点20）の挙動は [rules.md](rules.md)「勝敗条件」を参照。
- オンライン対戦では効果はカードデータと盤面から決定的に解決されるため、プレイ同期（`NGS_MainAction`）以外の追加同期は不要。

---

## 新しい効果を追加する（コード）

新しい `EventType` を実装する手順（enum 追加・`ApplyEventEffectAsync` / `ResolveCharacterTriggeredEffectAsync` への case 追加・演出）は [patterns.md](patterns.md)「2. 新しいイベント効果（EventType）を追加する」「2-B. キャラカードに登場時効果を追加する」「2-C. コスト支払い時に作用する受動効果」を参照。
