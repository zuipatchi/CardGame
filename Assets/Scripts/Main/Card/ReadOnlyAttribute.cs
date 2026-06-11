using UnityEngine;

namespace Main.Card
{
    // インスペクタで読み取り専用（グレー表示）にするためのフィールド属性。
    // 実際の描画は Editor 側の ReadOnlyDrawer が行う。
    public sealed class ReadOnlyAttribute : PropertyAttribute
    {
    }
}
