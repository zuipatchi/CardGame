using UnityEngine;
using UnityEngine.InputSystem;

namespace Home
{
    public sealed class HomeFoodSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject _foodPrefab;

        [SerializeField]
        private Camera _camera;

        [SerializeField]
        private float _spawnDepth = 10f;

        [SerializeField]
        private float _spawnPadding = 50f;

        [SerializeField]
        private HomeLive2DPresenter _dogPresenter;

        private GameObject _currentFood;

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector3 mousePos = Mouse.current.position.ReadValue();
            if (mousePos.x < _spawnPadding
                || mousePos.x >= Screen.width * 0.5f - _spawnPadding
                || mousePos.y < _spawnPadding
                || mousePos.y >= Screen.height - _spawnPadding)
            {
                return;
            }

            if (_currentFood != null)
            {
                return;
            }

            Vector3 worldPos = _camera.ScreenToWorldPoint(
                new Vector3(mousePos.x, mousePos.y, _spawnDepth)
            );
            GameObject food = Instantiate(_foodPrefab, worldPos, Quaternion.identity);
            _currentFood = food;
            Animator foodAnimator = food.GetComponentInChildren<Animator>();

            // CubismFadeController  : FadeMotionList 未設定による NullRef を防ぐため無効化
            // CubismParameterStore  : CubismModel.Update が RestoreParameters() を呼び Eat アニメーション値を
            //                         0 に戻してしまうため無効化（idle.anim のパスが不正で保存値が常に 0 のため）
            foreach (Component c in food.GetComponentsInChildren<Component>())
            {
                string typeName = c.GetType().Name;
                if ((typeName == "CubismFadeController" || typeName == "CubismParameterStore")
                    && c is Behaviour b)
                {
                    b.enabled = false;
                }
            }

            _dogPresenter?.NotifyFoodSpawned(worldPos, foodAnimator);
        }
    }
}
