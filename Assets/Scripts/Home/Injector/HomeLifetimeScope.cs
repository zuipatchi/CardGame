using VContainer;
using VContainer.Unity;

namespace Home.Injector
{
    public sealed class HomeLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<HomePresenter>();
            builder.RegisterComponentInHierarchy<HomeLive2DPresenter>();
            builder.RegisterComponentInHierarchy<DogSpeechPresenter>().AsImplementedInterfaces().AsSelf();
        }
    }
}
