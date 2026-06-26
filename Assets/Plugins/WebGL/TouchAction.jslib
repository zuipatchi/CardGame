// Unity WebGL の canvas に touch-action: none を設定する JS プラグイン。
//
// canvas に touch-action が無いと、スマホのタッチドラッグ中にブラウザがそのタッチをスクロール/パン操作と
// みなして奪い、canvas に pointercancel が飛ぶ。すると UI Toolkit のドラッグ（CardDragManipulator）で
// PointerMove が途中で止まり（カードが少しだけ追従して停止）、PointerUp が来ずにゴーストカードが残る。
//
// Unityroom は Build / StreamingAssets だけをアップロードし、HTML・canvas は Unityroom 側が生成するため、
// テンプレートの style.css は届かない。そこでホスト側の HTML に依存せず、実行時に canvas へ直接設定する。
// Module.canvas は createUnityInstance に渡された canvas（= ホストが用意した実物）を指すので確実に効く。
mergeInto(LibraryManager.library, {
  SetCanvasTouchActionNone: function () {
    try {
      var canvas = (typeof Module !== 'undefined' && Module.canvas)
        ? Module.canvas
        : (document.querySelector('#unity-canvas') || document.querySelector('canvas'));
      if (canvas) {
        canvas.style.touchAction = 'none';
        canvas.style.msTouchAction = 'none';
      }
    } catch (e) {
      console.warn('SetCanvasTouchActionNone failed: ' + e);
    }
  },
});
