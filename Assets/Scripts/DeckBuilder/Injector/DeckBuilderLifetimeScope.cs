using DeckBuilder.Sound;
using Main.Card;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DeckBuilder.Injector
{
    public class DeckBuilderLifetimeScope : LifetimeScope
    {
        [SerializeField] private CardDatabase _cardDatabase;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_cardDatabase);
            builder.RegisterEntryPoint<CardStore>().AsSelf();
            builder.RegisterEntryPoint<DeckBuilderAudioManager>().AsSelf();
            builder.RegisterComponentInHierarchy<DeckBuilderPresenter>().AsSelf().AsImplementedInterfaces();
        }
    }
}
