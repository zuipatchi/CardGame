using System.Threading;
using Common.Username;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Title.Username
{
    internal sealed class UsernameModalService : IAsyncStartable
    {
        private readonly UsernameRepository _usernameRepository;
        private readonly UsernameModalPresenter _presenter;

        public UsernameModalService(UsernameRepository usernameRepository, UsernameModalPresenter presenter)
        {
            _usernameRepository = usernameRepository;
            _presenter = presenter;
        }

        public UniTask StartAsync(CancellationToken ct)
        {
            if (!ct.IsCancellationRequested && string.IsNullOrEmpty(_usernameRepository.Load()))
            {
                _presenter.ShowModal();
            }
            return UniTask.CompletedTask;
        }
    }
}
