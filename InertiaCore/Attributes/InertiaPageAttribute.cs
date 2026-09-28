namespace InertiaCore;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class InertiaPageAttribute : Attribute
{
    public string Component { get; }

    public InertiaPageAttribute(string component)
    {
        Component = component;
    }
}
