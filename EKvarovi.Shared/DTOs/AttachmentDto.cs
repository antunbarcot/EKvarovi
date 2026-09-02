namespace EKvarovi.Shared.DTOs;

public class AttachmentDto
{
    public int Id { get; set; }
    public int FaultReportId { get; set; }
    public int? InterventionId { get; set; }
    public int AttachmentPurposeId { get; set; }
    public string AttachmentPurposeName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
