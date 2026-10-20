namespace InertiaCore.Models;

/// <summary>
/// The JavaScript package manager used to run the Vite dev server script.
/// </summary>
public enum VitePackageManager
{
    /// <summary>npm (<c>npm run &lt;script&gt;</c>).</summary>
    Npm,

    /// <summary>pnpm (<c>pnpm run &lt;script&gt;</c>).</summary>
    Pnpm,

    /// <summary>Yarn (<c>yarn run &lt;script&gt;</c>).</summary>
    Yarn,

    /// <summary>Bun (<c>bun run &lt;script&gt;</c>).</summary>
    Bun,
}
