using System.Threading.Tasks;
using InertiaCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace InertiaCore.TagHelpers;

/// <summary>
/// Renders the Inertia page payload (<c>Inertia.Html(Model)</c>) for a root view.
/// </summary>
[HtmlTargetElement("inertia")]
public class InertiaTagHelper : TagHelper
{
    /// <summary>The current view context, used to resolve the page model.</summary>
    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var model = ViewContext.ViewData.Model!;
        var content = await Inertia.Html(model);

        output.Content.SetHtmlContent(content);
    }
}
