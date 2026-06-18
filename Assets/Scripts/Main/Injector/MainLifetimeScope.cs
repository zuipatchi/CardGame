using Main.Card;
using Main.Game;
using Main.Network;
using Main.Sound;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Main.Injector
{
    public class MainLifetimeScope : LifetimeScope
    {
        [SerializeField] private CardDatabase _cardDatabase;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_cardDatabase);
            builder.RegisterEntryPoint<CardStore>().AsSelf();
            builder.RegisterEntryPoint<MainAudioManager>();
            builder.Register<FlavorVoiceStore>(Lifetime.Scoped);
            builder.Register<GameModel>(Lifetime.Scoped);
            builder.Register<NetworkGameService>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<MainPresenter>().AsSelf().AsImplementedInterfaces();
        }
    }
}
