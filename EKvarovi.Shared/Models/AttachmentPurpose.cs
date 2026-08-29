namespace EKvarovi.Shared.Models;

public class AttachmentPurpose
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
