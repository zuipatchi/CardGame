using Common.Cpu;
using Common.Deck;
using Common.GameSession;
using Common.Option;
using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Common.Transition;
using Common.Tutorial;
using Common.Username;
using VContainer;
using VContainer.Unity;

namespace Common.Injector
{
    public class CommonLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<DeckModel>(Lifetime.Singleton).AsSelf();
            builder.Register<DeckRuleModel>(Lifetime.Singleton).AsSelf();
            builder.Register<DeckRepository>(Lifetime.Singleton).AsSelf();
            builder.Register<UsernameRepository>(Lifetime.Singleton).AsSelf();
            builder.Register<GameSessionModel>(Lifetime.Singleton).AsSelf();
            builder.Register<TutorialModel>(Lifetime.Singleton).AsSelf();
            builder.Register<CpuBattleModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<CpuRosterStore>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<ModalStore>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<OptionPresenter>().AsSelf();
            builder.RegisterEntryPoint<OptionModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<SoundPlayer>().AsSelf();
            builder.RegisterEntryPoint<SoundStore>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<TransitionPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<SceneTransitioner>().AsSelf();
        }
    }
}
