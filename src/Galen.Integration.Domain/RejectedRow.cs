namespace Galen.Integration.Domain;

/// <summary>
/// Represents a row that failed validation or normalization for dead-letter reporting.
/// </summary>
public sealed class RejectedRow
{
    public int RowIndex { get; set; }
    public string? RawLine { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
}
