using InertiaCore;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace InertiaCore.TagHelpers;

/// <summary>
/// Renders the Vite React refresh runtime (<c>Vite.ReactRefresh()</c>) for a root view.
/// </summary>
[HtmlTargetElement("vite-react-refresh")]
public class ViteReactRefreshTagHelper : TagHelper
{
    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(Vite.ReactRefresh());
    }
}
