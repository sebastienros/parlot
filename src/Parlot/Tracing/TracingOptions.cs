namespace Parlot.Tracing;

/// <summary>
/// Configuration options for parser tracing.
/// </summary>
public class TracingOptions
{
    /// <summary>
    /// Gets or sets the number of characters to preview from the input at each trace point.
    /// Default is 15.
    /// </summary>
    public int PreviewLength { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether to include success/failure information in trace markers.
    /// Default is true.
    /// </summary>
    public bool IncludeSuccess { get; set; } = true;
}
