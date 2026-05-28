# エフェクト実装ノウハウ

パーティクル・VFX エフェクトを実装するときのハマりポイントと対処法。

---

## 1. UI Toolkit の上にパーティクルを表示できない問題

### 現象

`ParticleSystem` や VFX Graph のエフェクトをワールド空間に配置しても、UI Toolkit の画面に隠れて見えない。`SortingOrder` をいくら上げても無効。

### 原因

UI Toolkit の `PanelSettings` デフォルトは `ScreenSpaceOverlay` モード。このモードではすべての UI がカメラのレンダリング後に最後に描画されるため、**ワールド空間のオブジェクトは必ず UI の下になる**。`SortingOrder` は Canvas 間の順序には効くが、UI Toolkit の ScreenSpaceOverlay には効かない。

また `PanelSettings` には `targetCamera` プロパティが存在せず、`PanelRenderMode` は `ScreenSpaceOverlay` と `WorldSpace` の2値のみ（`ScreenSpaceCamera` はない）。

### 対処法：RenderTexture + 加算ブレンド Canvas

エフェクト専用カメラで RenderTexture に描画し、uGUI の Screen Space Overlay Canvas 上の `RawImage` でカスタムシェーダーを使って合成する。

```
[エフェクト専用カメラ] → RenderTexture → RawImage（加算ブレンド）→ Canvas（SortingOrder=100）
                                                                        ↑ UI Toolkit より上に描画される
```

**実装手順:**

```csharp
// 1. メインカメラからエフェクトレイヤーを除外
const int EffectLayer = 6; // TagManager.asset で "Firework" 等に設定
Camera mainCam = Camera.main;
mainCam.cullingMask &= ~(1 << EffectLayer);

// 2. RenderTexture を作成
RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 0);
rt.Create();

// 3. エフェクト専用カメラを作成
GameObject camObj = new GameObject("EffectCamera");
Camera effectCam = camObj.AddComponent<Camera>();
effectCam.clearFlags = CameraClearFlags.SolidColor;
effectCam.backgroundColor = Color.black; // 黒=加算で透明になる
effectCam.cullingMask = 1 << EffectLayer;
effectCam.targetTexture = rt;
effectCam.fieldOfView = mainCam.fieldOfView;
effectCam.nearClipPlane = mainCam.nearClipPlane;
effectCam.farClipPlane = mainCam.farClipPlane;
camObj.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);

// 4. Screen Space Overlay Canvas を作成
GameObject canvasObj = new GameObject("EffectCanvas");
Canvas canvas = canvasObj.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.sortingOrder = 100; // UI Toolkit より上

// 5. RawImage で RT を表示（カスタムシェーダー必須）
// ※ Shader.Find はビルドに含まれないため [SerializeField] で参照を保持すること
// [SerializeField] private Shader _fireworkAdditiveUIShader;
GameObject imgObj = new GameObject("EffectImage");
imgObj.transform.SetParent(canvasObj.transform, false);
RawImage img = imgObj.AddComponent<RawImage>();
img.texture = rt;
img.material = new Material(_fireworkAdditiveUIShader);

RectTransform rect = imgObj.GetComponent<RectTransform>();
rect.anchorMin = Vector2.zero;
rect.anchorMax = Vector2.one;
rect.sizeDelta = Vector2.zero;
rect.anchoredPosition = Vector2.zero;

// 6. エフェクトプレハブのレイヤーを専用レイヤーに設定
SetLayerRecursive(effectPrefabInstance, EffectLayer);

// 7. 後片付け
mainCam.cullingMask = originalCullingMask;
Destroy(canvasObj);
Destroy(camObj);
rt.Release();
Destroy(rt);
```

---

## 2. `UI/Default` シェーダーのブレンドモードはランタイムで変更できない

### 現象

RawImage に `UI/Default` シェーダーのマテリアルを使い、`SetInt("_SrcBlend", ...)` / `SetInt("_DstBlend", ...)` でランタイムに加算ブレンドへ変更しようとしても無効。RenderTexture の黒背景（alpha=1）が画面全体を覆い**画面が真っ黒**になる。

### 原因

URP では `UI/Default` シェーダーのブレンドステートはシェーダーバリアント内にベイクされており、`SetInt` でのランタイム変更が反映されない。シェーダーはデフォルトの `SrcAlpha / OneMinusSrcAlpha`（通常αブレンド）のまま動作する。αブレンドでは RT の黒背景（alpha=1）が「完全不透明の黒」として描画されるため、画面が黒くなる。

### 対処法：`Blend One One` をハードコードしたカスタムシェーダーを使う

[Assets/Shaders/FireworkAdditiveUI.shader](../Assets/Shaders/FireworkAdditiveUI.shader) を使用する。

```hlsl
// Blend One One: FinalColor = SrcRGB * 1 + DstRGB * 1
// → 黒(0,0,0)は 0 を加算するので透明、明るい色はそのまま加算される
Blend One One
```

加算ブレンドの原理:
- RT の黒背景 `(0, 0, 0)` → 画面色に 0 を加算 → **見えない（透明）**
- エフェクトの発光色 `(r, g, b)` → 画面色に加算 → **光が乗る**

---

## 3. UI Toolkit の背景画像（`backgroundImage`）はパーティクルと共存できない

### 現象

`mainRoot.style.backgroundImage = new StyleBackground(texture)` で盤面背景を設定すると、RenderTexture の加算ブレンドが背景の上に乗らず、背景のみが表示される（またはパーティクルが背景に隠れる）。

### 原因

UI Toolkit の `backgroundImage` は UI レンダリングの一部として描画される。RenderTexture の RawImage は Canvas に乗っているが、UI Toolkit のルート要素の background は「UI の底」にあるため、加算合成が正しく機能しない場合がある。

### 対処法：背景を `SpriteRenderer`（ワールド空間）に移す

```csharp
Camera cam = Camera.main;
float dist = Mathf.Abs(cam.transform.position.z);
float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
float halfWidth = halfHeight * cam.aspect;

// Addressables 等で Texture2D としてロードした場合は Sprite に変換する
Sprite sprite = Sprite.Create(
    texture,
    new Rect(0f, 0f, texture.width, texture.height),
    new Vector2(0.5f, 0.5f),
    100f);

GameObject bgObj = new GameObject("Background");
SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
sr.sprite = sprite;
sr.sortingOrder = -10; // エフェクトより後ろ

// カメラ画角にフィットするようスケール
Vector2 spriteSize = sprite.bounds.size;
float scale = Mathf.Max(halfWidth * 2f / spriteSize.x, halfHeight * 2f / spriteSize.y);
bgObj.transform.localScale = new Vector3(scale, scale, 1f);
```

**注意:** Addressables や `Texture2D` としてロードしたアセットは `SpriteRenderer.sprite` に直接代入できない。`Sprite.Create(...)` で変換する。

---

## 4. パーティクルプレハブのレイヤー設定

エフェクト専用レイヤーを `ProjectSettings/TagManager.asset` に追加し、カリングマスクで使う。

プレハブは子オブジェクト（サブエミッターなど）も含めて再帰的にレイヤーを設定する必要がある:

```csharp
private static void SetLayerRecursive(GameObject go, int layer)
{
    go.layer = layer;
    foreach (Transform child in go.transform)
        SetLayerRecursive(child.gameObject, layer);
}
```

---

## 5. パースペクティブカメラのワールド座標と画面端の計算式

エフェクトのスポーン位置を画面端に合わせたいときは以下の式で求める:

```csharp
Camera cam = Camera.main;
float dist = Mathf.Abs(cam.transform.position.z - spawnZ);
float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
float halfWidth  = halfHeight * cam.aspect;

float screenBottom = cam.transform.position.y - halfHeight;
float screenTop    = cam.transform.position.y + halfHeight;
float screenLeft   = cam.transform.position.x - halfWidth;
float screenRight  = cam.transform.position.x + halfWidth;
```

画面外から打ち上げる場合は `screenBottom` より小さい Y 値を使う。

---

## 6. UI Toolkit の worldBound をワールド座標に変換する

### 問題

`VisualElement.worldBound.center` は **パネル空間の座標**（Y=0 が上）で、`Screen.width` / `Screen.height`（実際の画面ピクセル数）と縮尺が異なる場合がある。

`PanelSettings` の Scale Mode が「Scale With Screen Size」かつ参照解像度とゲームビュー解像度が異なる場合、`worldBound` の値はゲームビューの実解像度より小さくなるため、`ScreenToWorldPoint` に直接渡すと位置がずれる。

### 対処法：パネル全体サイズで正規化してビューポート経由で変換

```csharp
Camera cam = Camera.main;

// パネル全体の幅・高さを取得して [0,1] のビューポート座標に正規化
Rect panelBounds = element.panel.visualTree.worldBound;
Vector2 uiCenter = element.worldBound.center;
float nx = uiCenter.x / panelBounds.width;
float ny = 1f - uiCenter.y / panelBounds.height; // UI は Y=0 が上なので反転

// ビューポート座標 → レイ → Z=0 平面との交差でワールド座標を求める
Ray ray = cam.ViewportPointToRay(new Vector3(nx, ny, 0f));
float t = -ray.origin.z / ray.direction.z;
Vector3 worldPos = ray.origin + ray.direction * t;
```

これでゲームビュー解像度・パネルスケール・カメラ位置を問わず正確なワールド座標が得られる。

**注意:** `ray.direction.z` がほぼ 0 のとき（カメラが Z 軸と水平）は交差しないため、このプロジェクトのようにカメラが Z 方向を向いている前提で使用する。
