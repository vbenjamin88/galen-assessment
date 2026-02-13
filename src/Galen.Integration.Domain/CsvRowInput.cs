namespace Galen.Integration.Domain;

/// <summary>
/// Raw CSV row schema - partner system format. Adjust column names per partner spec.
/// Column mapping is configured in Infrastructure (CsvHelper).
/// </summary>
public sealed class CsvRowInput
{
    public string? Id { get; set; }
    public string? PatientId { get; set; }
    public string? DocType { get; set; }
    public string? DocDate { get; set; }
    public string? Description { get; set; }
    public string? SourceSystem { get; set; }
}
