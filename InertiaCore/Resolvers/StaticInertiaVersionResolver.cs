using InertiaCore.Contracts;

namespace InertiaCore.Resolvers;

public class StaticInertiaVersionResolver : IInertiaVersionResolver
{
    private readonly string _version;

    public StaticInertiaVersionResolver(string version) => _version = version;

    public string GetVersion() => _version;
}