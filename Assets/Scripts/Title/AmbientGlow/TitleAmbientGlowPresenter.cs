using UnityEngine;

namespace Title.AmbientGlow
{
    public sealed class TitleAmbientGlowPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _ambientGlowPrefab;

        private const int SortingOrder = -50;

        private void Start()
        {
            if (_ambientGlowPrefab == null)
            {
                Debug.LogError("AmbientGlowPrefab が未アサインです");
                return;
            }

            GameObject instance = Instantiate(_ambientGlowPrefab, new Vector3(0f, -5f, 0f), Quaternion.identity);

            foreach (ParticleSystemRenderer renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.sortingOrder = SortingOrder;
            }
        }
    }
}
