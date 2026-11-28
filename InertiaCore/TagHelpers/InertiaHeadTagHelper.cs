using System.Threading.Tasks;
using InertiaCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace InertiaCore.TagHelpers;

/// <summary>
/// Renders the Inertia head fragment (<c>Inertia.Head(Model)</c>) for a root view.
/// </summary>
[HtmlTargetElement("inertia-head")]
public class InertiaHeadTagHelper : TagHelper
{
    /// <summary>The current view context, used to resolve the page model.</summary>
    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var model = ViewContext.ViewData.Model!;
        var content = await Inertia.Head(model);

        output.Content.SetHtmlContent(content);
    }
}
