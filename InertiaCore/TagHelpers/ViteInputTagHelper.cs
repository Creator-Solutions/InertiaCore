using InertiaCore;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace InertiaCore.TagHelpers;

/// <summary>
/// Renders Vite input tags (<c>Vite.Input(src)</c>) for a root view.
/// </summary>
[HtmlTargetElement("vite-input")]
public class ViteInputTagHelper : TagHelper
{
    /// <summary>The entry point (or comma-separated entry points) to render.</summary>
    public string Src { get; set; } = string.Empty;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(Vite.Input(Src));
    }
}
