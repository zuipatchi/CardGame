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

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector3 mousePos = Mouse.current.position.ReadValue();
            if (mousePos.x >= Screen.width * 0.5f)
            {
                return;
            }

            Vector3 worldPos = _camera.ScreenToWorldPoint(
                new Vector3(mousePos.x, mousePos.y, _spawnDepth)
            );
            Instantiate(_foodPrefab, worldPos, Quaternion.identity);
        }
    }
}
