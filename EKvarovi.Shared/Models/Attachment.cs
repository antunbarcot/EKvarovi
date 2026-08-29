namespace EKvarovi.Shared.Models;

public class Attachment
{
    public int Id { get; set; }
    public int FaultReportId { get; set; }
    public int? InterventionId { get; set; }
    public int AttachmentPurposeId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public int UploadedByAppUserId { get; set; }

    public FaultReport? FaultReport { get; set; }
    public Intervention? Intervention { get; set; }
    public AttachmentPurpose? AttachmentPurpose { get; set; }
    public AppUser? UploadedByAppUser { get; set; }
}
