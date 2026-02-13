using CsvHelper.Configuration;
using Galen.Integration.Domain;

namespace Galen.Integration.Infrastructure.Csv;

/// <summary>
/// Maps CSV columns to CsvRowInput. Supports header names: id, patient_id, doc_type, doc_date, description, source_system.
/// </summary>
public sealed class CsvRowInputMap : ClassMap<CsvRowInput>
{
    public CsvRowInputMap()
    {
        Map(m => m.Id).Name("id");
        Map(m => m.PatientId).Name("patient_id");
        Map(m => m.DocType).Name("doc_type");
        Map(m => m.DocDate).Name("doc_date");
        Map(m => m.Description).Name("description");
        Map(m => m.SourceSystem).Name("source_system");
    }
}
