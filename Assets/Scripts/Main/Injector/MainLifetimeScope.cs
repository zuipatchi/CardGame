using Main.Card;
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
            builder.RegisterComponentInHierarchy<MainPresenter>().AsSelf().AsImplementedInterfaces();
        }
    }
}
