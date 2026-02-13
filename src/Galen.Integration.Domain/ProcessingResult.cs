namespace Galen.Integration.Domain;

/// <summary>
/// Result of processing a single CSV file.
/// </summary>
public sealed class ProcessingResult
{
    public string FileName { get; set; } = string.Empty;
    public int TotalRowsRead { get; set; }
    public int RowsAccepted { get; set; }
    public int RowsRejected { get; set; }
    public IReadOnlyList<RejectedRow> RejectedRows { get; set; } = [];
    public bool Succeeded => RowsRejected == 0 || RowsAccepted > 0;
}
