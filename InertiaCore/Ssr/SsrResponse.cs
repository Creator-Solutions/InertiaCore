using Microsoft.AspNetCore.Html;

namespace InertiaCore.Ssr;

public class SsrResponse
{
    public List<string> Head { get; set; } = default!;
    public string Body { get; set; } = default!;

    public IHtmlContent GetBody() => new HtmlString(Body ?? string.Empty);

    public IHtmlContent GetHead() => new HtmlString(Head is null ? string.Empty : string.Join("\n", Head));
}
