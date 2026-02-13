-- Create database if not exists
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'GalenIntegration')
    CREATE DATABASE GalenIntegration;
GO

USE GalenIntegration;
GO

-- Canonical Record Type for Table-Valued Parameter (TVP)
-- Batch insert with idempotency via MERGE on (SourceFile, SourceRowIndex)
IF TYPE_ID(N'dbo.CanonicalRecordType') IS NOT NULL
    DROP TYPE dbo.CanonicalRecordType;
GO

CREATE TYPE dbo.CanonicalRecordType AS TABLE (
    ExternalId        NVARCHAR(500)  NOT NULL,
    PatientIdentifier NVARCHAR(500)  NOT NULL,
    DocumentType      NVARCHAR(100)  NULL,
    DocumentDate      DATETIME2      NULL,
    Description       NVARCHAR(1000) NULL,
    SourceSystem      NVARCHAR(500)  NULL,
    SourceFile        NVARCHAR(500)  NOT NULL,
    SourceRowIndex    INT            NOT NULL
);
GO

-- Target table for canonical records
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CanonicalRecords')
BEGIN
    CREATE TABLE dbo.CanonicalRecords (
        Id                BIGINT IDENTITY(1,1) PRIMARY KEY,
        ExternalId        NVARCHAR(500)  NOT NULL,
        PatientIdentifier NVARCHAR(500)  NOT NULL,
        DocumentType      NVARCHAR(100)  NULL,
        DocumentDate      DATETIME2      NULL,
        Description       NVARCHAR(1000) NULL,
        SourceSystem      NVARCHAR(500)  NULL,
        SourceFile        NVARCHAR(500)  NOT NULL,
        SourceRowIndex    INT            NOT NULL,
        CreatedAt         DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt         DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CanonicalRecords_SourceFile_Row UNIQUE (SourceFile, SourceRowIndex)
    );

    CREATE INDEX IX_CanonicalRecords_Patient ON dbo.CanonicalRecords(PatientIdentifier);
    CREATE INDEX IX_CanonicalRecords_ExternalId ON dbo.CanonicalRecords(ExternalId);
END
GO

-- Stored procedure: batch MERGE for idempotency
IF OBJECT_ID(N'dbo.usp_ImportCanonicalRecords', N'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ImportCanonicalRecords;
GO

CREATE PROCEDURE dbo.usp_ImportCanonicalRecords
    @Records dbo.CanonicalRecordType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.CanonicalRecords AS target
    USING @Records AS source
    ON target.SourceFile = source.SourceFile AND target.SourceRowIndex = source.SourceRowIndex
    WHEN MATCHED THEN
        UPDATE SET
            ExternalId        = source.ExternalId,
            PatientIdentifier = source.PatientIdentifier,
            DocumentType      = source.DocumentType,
            DocumentDate      = source.DocumentDate,
            Description       = source.Description,
            SourceSystem      = source.SourceSystem,
            UpdatedAt         = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalId, PatientIdentifier, DocumentType, DocumentDate, Description, SourceSystem, SourceFile, SourceRowIndex, CreatedAt, UpdatedAt)
        VALUES (source.ExternalId, source.PatientIdentifier, source.DocumentType, source.DocumentDate, source.Description, source.SourceSystem, source.SourceFile, source.SourceRowIndex, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO
