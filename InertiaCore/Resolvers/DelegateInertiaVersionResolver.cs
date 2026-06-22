using InertiaCore.Contracts;

namespace InertiaCore.Resolvers;

public class DelegateInertiaVersionResolver : IInertiaVersionResolver
{
    private readonly Func<IServiceProvider, string> _versionFactory;
    private readonly IServiceProvider _serviceProvider;

    public DelegateInertiaVersionResolver(Func<IServiceProvider, string> versionFactory, IServiceProvider serviceProvider)
    {
        _versionFactory = versionFactory;
        _serviceProvider = serviceProvider;
    }

    public string GetVersion() => _versionFactory(_serviceProvider);
}