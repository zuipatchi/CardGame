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
GameObject imgObj = new GameObject("EffectImage");
imgObj.transform.SetParent(canvasObj.transform, false);
RawImage img = imgObj.AddComponent<RawImage>();
img.texture = rt;
img.material = new Material(Shader.Find("Custom/FireworkAdditiveUI"));

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

UI Toolkit の `backgroundImage` は UI レンダリングの一部として描画される。RenderTexture の RawImage は Canvas に乗っているが、それでも UI Toolkit のルート要素の background は「UI の底」にあるため、加算合成が正しく機能しない場合がある。

### 対処法：背景を `SpriteRenderer`（ワールド空間）に移す

```csharp
private void SpawnBattleFieldBackground(Texture2D texture)
{
    Camera cam = Camera.main;
    float dist = Mathf.Abs(cam.transform.position.z);
    float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
    float halfWidth = halfHeight * cam.aspect;

    Sprite sprite = Sprite.Create(
        texture,
        new Rect(0f, 0f, texture.width, texture.height),
        new Vector2(0.5f, 0.5f),
        100f);

    GameObject bgObj = new GameObject("Background");
    SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
    sr.sprite = sprite;
    sr.sortingOrder = -10; // エフェクトより後ろ

    // アート素材にパース感がある場合は X 軸回転で調整
    bgObj.transform.SetPositionAndRotation(
        new Vector3(0f, 1.5f, -0.5f),
        Quaternion.Euler(10f, 0f, 0f));

    // カメラ画角にフィットするようスケール
    Vector2 spriteSize = sprite.bounds.size;
    float scale = Mathf.Max(halfWidth * 2f / spriteSize.x, halfHeight * 2f / spriteSize.y);
    bgObj.transform.localScale = new Vector3(scale, scale, 1f);
}
```

**注意:** `_cardStore.BattleField` は `Texture2D` 型。`SpriteRenderer.sprite` に直接代入できないため `Sprite.Create(...)` で変換する。

---

## 4. レイヤー設定（TagManager.asset）

エフェクト専用レイヤーを `ProjectSettings/TagManager.asset` に追加する。現在の割り当て:

| レイヤー番号 | 名前 |
|---|---|
| 6 | Firework |

カリングマスクはレイヤー番号を直接使う: `1 << 6`

プレハブは子オブジェクト含め再帰的にレイヤーを設定する必要がある（パーティクルの子エミッターも含む）:

```csharp
private static void SetLayerRecursive(GameObject go, int layer)
{
    go.layer = layer;
    foreach (Transform child in go.transform)
        SetLayerRecursive(child.gameObject, layer);
}
```

---

## 5. ワールド座標と画面端の対応

カメラ設定（Main シーン）:
- 位置: `(0, 3.15, -10)`
- FOV: 60°、パースペクティブ
- Z=0 平面での画面半高さ: `tan(30°) × 10 ≈ 5.77`
- **画面下端 Y ≈ 3.15 − 5.77 = −2.62**
- **画面上端 Y ≈ 3.15 + 5.77 = +8.92**

エフェクトを画面外の下から打ち上げる場合は `Y = -5 〜 -7` が目安。
