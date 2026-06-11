# カード効果（EventType）と設定方法

カードの効果は `EventType`（[EffectType.cs](../Assets/Scripts/Main/Card/EffectType.cs)。ファイル名は `EffectType.cs` だが enum 名は `EventType`）で表す。

- **イベントカード**: `EventType` + `EventValue` で設定する（プレイ時に即時解決）
- **キャラカード**: `EffectTrigger`（発動タイミング）+ `EffectType`（イベントと共通の `EventType`）+ `EffectValue` で設定する

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
| Event Value | 効果の数値（上表「EventValue の意味」） |
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
| Effect Value | 効果の数値 |
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
- **DamageAllEnemies / GainVictoryPoints**: イベント・キャラ（OnEnter / OnAttack）両方で使用可能。
- **GainVictoryPoints**: 加点カードは**緑属性**で作る（緑カードをプレイした側に勝利点表示が出現するため）。
- 勝敗に関わる属性（赤=ハート / 青=デッキ0 / 緑=勝利点20）の挙動は [rules.md](rules.md)「勝敗条件」を参照。
- オンライン対戦では効果はカードデータと盤面から決定的に解決されるため、プレイ同期（`NGS_MainAction`）以外の追加同期は不要。

---

## 新しい効果を追加する（コード）

新しい `EventType` を実装する手順（enum 追加・`ApplyEventEffectAsync` / `ResolveCharacterTriggeredEffectAsync` への case 追加・演出）は [patterns.md](patterns.md)「2. 新しいイベント効果（EventType）を追加する」「2-B. キャラカードに登場時効果を追加する」「2-C. コスト支払い時に作用する受動効果」を参照。
