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

UXML で日本語テキストを使用する場合は `Assets/Font/NotoSansJP-VariableFont_wght.ttf` を指定する。

```xml
<ui:Label text="日本語テキスト" style="-unity-font: url('project://database/Assets/Font/NotoSansJP-VariableFont_wght.ttf');"/>
```

---

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

### リスト項目（`.room-item`）

```css
.room-item {
    background-color: rgba(255, 255, 255, 0.05);
    border-top-left-radius: 8px; /* 他3角も同じ */
    border-left-width: 1px; /* 他3辺も同じ */
    border-left-color: rgba(255, 255, 255, 0.1); /* 他3辺も同じ */
    padding-top: 14px; padding-right: 16px; padding-bottom: 14px; padding-left: 16px;
    margin-bottom: 8px;
    color: rgb(180, 180, 210);
    font-size: 14px;
    -unity-text-align: middle-left;
}
.room-item:hover {
    background-color: rgba(70, 90, 180, 0.2);
    border-left-color: rgba(70, 90, 180, 0.5); /* 他3辺も同じ */
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
