# デザインシステム

UI Toolkit（UXML + USS）を使用したUIの設計ルールをまとめる。

## USS ファイルの使い方

スタイルはインライン記述せず、シーンごとの USS ファイルに定義してクラスで適用する。

```xml
<!-- UXML の先頭で USS を読み込む -->
<ui:UXML ...>
    <Style src="MyScene.uss" />
    ...
    <ui:Button class="btn-accent" style="width: 160px;" />
</ui:UXML>
```

USS ファイルは対応する UXML と同じディレクトリに配置する（例: `View/Matching.uss`）。

---

---

## カラーパレット

| 用途 | 値 |
|---|---|
| カード背景 | `rgb(22, 22, 35)` |
| オーバーレイ暗幕 | `rgba(0, 0, 0, 0.55)` |
| ボーダー | `rgba(255, 255, 255, 0.15)` |
| 区切り線 | `rgba(255, 255, 255, 0.1)` |
| テキスト（見出し） | `rgb(240, 240, 255)` |
| テキスト（本文・ラベル） | `rgb(180, 180, 210)` |
| テキスト（ボタン） | `rgb(255, 255, 255)` |
| アクセント（ボタン背景） | `rgb(70, 90, 180)` |

---

## タイポグラフィ

| 用途 | font-size | font-style |
|---|---|---|
| モーダルタイトル | `20px` | `bold` |
| ラベル（項目名） | `13px` | normal |
| ボタンテキスト | `14px` | normal |

### 日本語フォント

UI Toolkit のデフォルトフォントを **Noto Sans JP Bold** に設定済みのため、UXML/USS で個別にフォントを指定する必要はない。すべてのテキストが自動でこのフォントで描画される。

- 実体は可変フォントから `wght=700` を切り出した静的 Bold（`Assets/Font/NotoSansJP-Bold.ttf`）。これを TextCore ダイナミック FontAsset 化し、`Assets/Font/UI Panel Text Settings.asset`（PanelTextSettings）のデフォルトフォントに設定 → `Assets/Scripts/Panel Settings.asset` に割り当てている。
- 配線を作り直したいときはエディタメニュー **Tools > UI > Setup Bold Japanese Font** を実行（[UiFontSetup.cs](../Assets/Scripts/Editor/UiFontSetup.cs)）。
- ベースが既に太字（700）のため、`-unity-font-style: bold` を併用した箇所はさらに太く（合成ボールド）描画される。
- **NotoSansJP に無い記号は使わない**。絵文字・ダインバット系（鉛筆 `✎`、`✕`、小三角 `▾` など）はこのフォントに未収録で、エディタでは OS フォントへフォールバックして見えても **WebGL ビルドでは豆腐（□）になる**。閉じる＝`×`(U+00D7)、ドロップダウン矢印＝`▼`(U+25BC) のように収録済みグリフを使うか、画像アイコン（USS `background-image`）／テキストに置き換える。矢印（→←↑↓）・三点リーダ（…）・星（★☆）・●■・引用符は収録済み。

## スペーシング

| 用途 | 値 |
|---|---|
| カード内パディング | `28px 32px` |
| セクション間マージン | `18px` |
| 最終セクション下マージン | `24px` |
| タイトル下マージン | `16px` |
| 区切り線下マージン | `20px` |
| ラベル〜スライダー間 | `4px` |

---

## コンポーネント

USS クラス定義の実装例は [Assets/Scripts/Matching/View/Matching.uss](../Assets/Scripts/Matching/View/Matching.uss) を参照。

### カード（`.card`）

```css
.card {
    background-color: rgb(22, 22, 35);
    border-top-left-radius: 16px; border-top-right-radius: 16px;
    border-bottom-left-radius: 16px; border-bottom-right-radius: 16px;
    border-left-width: 1px; border-right-width: 1px;
    border-top-width: 1px; border-bottom-width: 1px;
    border-left-color: rgba(255, 255, 255, 0.15); /* 他3辺も同じ */
    padding-top: 32px; padding-right: 32px; padding-bottom: 32px; padding-left: 32px;
}
```

### 区切り線（`.divider`）

```css
.divider {
    height: 1px;
    background-color: rgba(255, 255, 255, 0.1);
    margin-bottom: 16px;
}
```

### ボタン

実装例: [Assets/AddressableAssets/Modal/Modal.uss](../Assets/AddressableAssets/Modal/Modal.uss)

```css
/* プライマリボタン（主要アクション） */
.modal-btn-primary {
    background-color: rgb(70, 90, 180);
    color: rgb(255, 255, 255);
    border-top-left-radius: 8px; /* 他3角も同じ */
    border-left-width: 0; /* 他3辺も同じ */
    padding-top: 10px; padding-right: 10px; padding-bottom: 10px; padding-left: 10px;
    font-size: 14px;
    -unity-text-align: middle-center;
    transition-property: background-color;
    transition-duration: 0.12s;
}
.modal-btn-primary:hover { background-color: rgb(90, 115, 210); }
.modal-btn-primary:active { background-color: rgb(55, 70, 155); }

/* ゴーストボタン（補助アクション） */
.modal-btn-ghost {
    background-color: rgba(0, 0, 0, 0);
    color: rgb(180, 180, 210);
    border-top-left-radius: 8px; /* 他3角も同じ */
    border-left-width: 1px; /* 他3辺も同じ */
    border-left-color: rgba(255, 255, 255, 0.3); /* 他3辺も同じ */
    padding-top: 10px; padding-right: 10px; padding-bottom: 10px; padding-left: 10px;
    font-size: 14px;
    -unity-text-align: middle-center;
    transition-property: background-color;
    transition-duration: 0.12s;
}
.modal-btn-ghost:hover {
    background-color: rgba(255, 255, 255, 0.1);
    border-left-color: rgba(255, 255, 255, 0.55); /* 他3辺も同じ */
}
.modal-btn-ghost:active { background-color: rgba(255, 255, 255, 0.2); }
```

**C# コードで生成したボタンのホバー・押下効果**

`Button` をコードで生成してインラインスタイルを設定している場合、USS 擬似クラスは
インラインスタイルに上書きされないため、PointerEvent コールバックで対応する:

```csharp
private static void AddButtonHoverEffect(Button button, Color baseColor)
{
    Color hoverColor = new Color(
        Mathf.Clamp01(baseColor.r + 0.12f),
        Mathf.Clamp01(baseColor.g + 0.12f),
        Mathf.Clamp01(baseColor.b + 0.12f), baseColor.a);
    Color activeColor = new Color(
        Mathf.Clamp01(baseColor.r - 0.1f),
        Mathf.Clamp01(baseColor.g - 0.1f),
        Mathf.Clamp01(baseColor.b - 0.1f), baseColor.a);
    button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = new StyleColor(hoverColor));
    button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = new StyleColor(baseColor));
    button.RegisterCallback<PointerDownEvent>(_ => button.style.backgroundColor = new StyleColor(activeColor));
    button.RegisterCallback<PointerUpEvent>(_ => button.style.backgroundColor = new StyleColor(hoverColor));
}
```

**USS での背景色変化が効かない場合の原因**

ボタンに `button.style.backgroundColor = ...` (インラインスタイル) が設定されていると、
USS クラスの `:hover` / `:active` ルールは上書きできない（インラインスタイルが優先される）。
解決策は上記 PointerEvent か、インラインスタイルを除去して USS クラスのみで管理する。

**背景画像ボタンの押下フィードバック（scale 変化）**

背景が PNG 画像のボタン（背景色変化が見えないボタン）には、`scale` の transition で
ホバー拡大・押下縮小のフィードバックを付ける。Main フィールドの Action ボタン
（Pass/End/Back/OK）・Mulligan ボタン・Game End ボタンで採用。

```css
.action-button {
    transition-property: scale;
    transition-duration: 0.1s;
}
.action-button:hover  { scale: 1.06 1.06; }
.action-button:active { scale: 0.94 0.94; }
```

### リスト項目（`.room-item`）

ルーム名（左・太字）と人数バッジ（右・ピル表示）を横並びにした行。子要素は `.room-item__name` / `.room-item__count`（`MatchingPresenter.RebuildRoomList` が Label を2つ生成して付与）。マッチングモーダル（カード幅 720px）を横に広げてもルーム行は固定幅 456px のまま、`align-self: center` でリスト内に中央寄せされる。

```css
.room-item {
    width: 456px; align-self: center; flex-shrink: 0; /* カードを広げても従来サイズで中央寄せ */
    flex-direction: row; align-items: center; justify-content: space-between;
    background-color: rgba(255, 255, 255, 0.05);
    border-top-left-radius: 8px; /* 他3角も同じ */
    border-left-width: 1px; /* 他3辺も同じ */
    border-left-color: rgba(255, 255, 255, 0.1); /* 他3辺も同じ */
    padding-top: 10px; padding-right: 14px; padding-bottom: 10px; padding-left: 16px;
    margin-bottom: 8px;
}
.room-item:hover {
    background-color: rgba(70, 90, 180, 0.2);
    border-left-color: rgba(70, 90, 180, 0.5); /* 他3辺も同じ */
}
.room-item__name { /* ルーム名：左寄せ・太字、余白を占有 */
    flex-grow: 1; flex-shrink: 1;
    color: rgb(225, 225, 245); font-size: 16px; -unity-font-style: bold; -unity-text-align: middle-left;
}
.room-item__count { /* 人数バッジ：青系ピル・右端 */
    flex-shrink: 0; margin-left: 12px;
    color: rgb(200, 210, 240); font-size: 13px;
    background-color: rgba(70, 90, 180, 0.28);
    border-top-left-radius: 6px; /* 他3角も同じ */
    padding-top: 3px; padding-right: 10px; padding-bottom: 3px; padding-left: 10px;
    -unity-text-align: middle-center;
}
```

---

## オーバーレイ

モーダル表示時はゲーム画面を暗幕で覆う。

```xml
<!-- position: absolute で全画面を覆う -->
<ui:VisualElement style="
    position: absolute; width: 100%; height: 100%;
    background-color: rgba(0, 0, 0, 0.55);">
    <!-- align-items: center; justify-content: center でカードを中央配置 -->
    <ui:VisualElement style="flex-grow: 1; align-items: center; justify-content: center;"/>
</ui:VisualElement>
```

---

## アイコン

アイコンは SVG を Addressables に配置し、UXML の `background-image` で参照する。

```
Assets/AddressableAssets/Icon/
  sliders-solid-full.svg   オプション設定アイコン
```

常に表示するアイコン（オプションボタンなど）は `position: absolute` で配置する。

```xml
<!-- 右上に固定表示する例 -->
<ui:Image style="position: absolute; right: 2%; top: 2%; width: 5%; height: 5%;"/>
```

---

## UIDocument の設定

| 設定 | 値 | 理由 |
|---|---|---|
| SortingOrder | `1000` | 他のUIより手前に描画するため |
