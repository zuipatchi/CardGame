using Matching;
using Matching.Sound;
using VContainer;
using VContainer.Unity;

namespace Matching.Injector
{
    public class MatchingLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<MatchingModel>(Lifetime.Scoped).AsSelf();
            builder.Register<MatchingService>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<MatchingAudioManager>().AsSelf();
            builder.RegisterComponentInHierarchy<MatchingPresenter>().AsSelf().AsImplementedInterfaces();
        }
    }
}
