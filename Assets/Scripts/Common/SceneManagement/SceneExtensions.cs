using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Common.SceneManagement
{
    internal static class SceneExtensions
    {
        internal static void BuildLifetimeScopes(this Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (LifetimeScope scope in root.GetComponentsInChildren<LifetimeScope>(true))
                {
                    scope.Build();
                }
            }
        }
    }
}
