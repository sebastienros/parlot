using BenchmarkDotNet.Attributes;
using Fluid;

namespace Parlot.ExternalBenchmarks;

/// <summary>
/// Parses Liquid templates with Fluid, which builds its grammar with the Parlot runtime (Fluent) API.
/// </summary>
public class FluidParse
{
    // Typical short templates, as found in CMS content and shapes
    internal static readonly string[] Snippets =
    [
        "{{ Model.ContentItem.DisplayText | upcase }}",
        "Hello {{ name | default: 'World' | capitalize }}!",
        "{% if user.isAdmin and user.roles contains 'editor' %}Admin{% elsif user %}User{% else %}Guest{% endif %}",
        "{% for product in products limit: 3 offset: 1 %}<li>{{ product.name | escape }} - {{ product.price | times: 1.2 | round: 2 }}</li>{% endfor %}",
        "{% assign total = items | map: 'price' | sum %}{{ total | format_number: '0.00' }}",
        "{% capture title %}{{ page.title }} - {{ site.name }}{% endcapture %}<title>{{ title | strip }}</title>",
        "{% case status %}{% when 'draft' %}Draft{% when 'published', 'live' %}Live{% else %}Unknown{% endcase %}",
        "{{ '2024-01-15T10:30:00Z' | date: '%Y-%m-%d %H:%M' }} {% comment %}ignored {{ text }}{% endcomment %}",
        "{% unless items == empty %}{{ items | size }} items: {{ items | join: ', ' }}{% endunless %}",
        "{% raw %}{{ not parsed }}{% endraw %}{%- if x > 1 -%} {{- x | minus: 1 -}} {%- endif -%}",
    ];

    internal static readonly string Product = Resources.Read("product.liquid");
    internal static readonly string BlogPost = Resources.Read("blogpost.liquid");
    internal static readonly string Large = string.Concat(Enumerable.Repeat(BlogPost, 20));

    private readonly FluidParser _parser = new();

    [GlobalSetup]
    public void Setup()
    {
        foreach (var template in Snippets.Append(Product).Append(BlogPost).Append(Large))
        {
            if (!_parser.TryParse(template, out _, out var error))
            {
                throw new InvalidOperationException($"Fluid couldn't parse a benchmark template: {error}\n{template}");
            }
        }
    }

    [Benchmark]
    public object Product_216B() => _parser.Parse(Product);

    [Benchmark]
    public object BlogPost_1_6KB() => _parser.Parse(BlogPost);

    [Benchmark]
    public object Large_32KB() => _parser.Parse(Large);

    [Benchmark(OperationsPerInvoke = 10)]
    public object Snippets_x10()
    {
        object last = null;

        foreach (var snippet in Snippets)
        {
            last = _parser.Parse(snippet);
        }

        return last;
    }
}

internal static class Resources
{
    public static string Read(string name)
    {
        using var stream = typeof(Resources).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing resource {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
