using System.Linq.Expressions;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

// Citanje/upload dopusteno svim prijavljenim ulogama - svatko dodaje/vidi
// fotografije u svom kontekstu. Brisanje je rezervirano za Admin/Manager.
[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private const string PurposeFotografijaPrije = "FotografijaPrije";
    private const string PurposeFotografijaPoslije = "FotografijaPoslije";
    private const string PurposeDokument = "Dokument";

    private static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] AllowedDocumentContentTypes = { "application/pdf" };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // Privremeni placeholder dok JWT autentikacija ne postoji u API-ju - vidi seed
    // AppUser Id=1 ("Sistem") u EKvaroviDbContext. Kad autentikacija bude ozicena,
    // ovo se zamjenjuje s identitetom prijavljenog korisnika iz JWT claima.
    private const int SystemAppUserId = 1;

    private readonly EKvaroviDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AttachmentsController> _logger;

    private static readonly Expression<Func<Attachment, AttachmentDto>> ToDtoProjection = a => new AttachmentDto
    {
        Id = a.Id,
        FaultReportId = a.FaultReportId,
        InterventionId = a.InterventionId,
        AttachmentPurposeId = a.AttachmentPurposeId,
        AttachmentPurposeName = a.AttachmentPurpose != null ? a.AttachmentPurpose.Name : string.Empty,
        OriginalFileName = a.OriginalFileName,
        ContentType = a.ContentType,
        FileSizeBytes = a.FileSizeBytes,
        UploadedAt = a.UploadedAt,
        DownloadUrl = "/uploads/" + a.StoredFileName
    };

    public AttachmentsController(EKvaroviDbContext context, IWebHostEnvironment environment, ILogger<AttachmentsController> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<AttachmentDto>>> GetAttachments([FromQuery] int faultReportId, [FromQuery] int? interventionId)
    {
        var query = _context.Attachments
            .Where(a => a.FaultReportId == faultReportId);

        if (interventionId.HasValue)
        {
            query = query.Where(a => a.InterventionId == interventionId.Value);
        }

        var attachments = await query
            .OrderBy(a => a.UploadedAt)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(attachments);
    }

    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(
        IFormFile file,
        [FromForm] int faultReportId,
        [FromForm] int? interventionId,
        [FromForm] int attachmentPurposeId)
    {
        var faultReport = await _context.FaultReports.FirstOrDefaultAsync(fr => fr.Id == faultReportId);
        if (faultReport is null)
        {
            return NotFound($"Prijava s Id={faultReportId} ne postoji.");
        }

        if (interventionId.HasValue)
        {
            var interventionExists = await _context.Interventions.AnyAsync(i => i.Id == interventionId.Value);
            if (!interventionExists)
            {
                return NotFound($"Intervencija s Id={interventionId.Value} ne postoji.");
            }
        }

        var purpose = await _context.AttachmentPurposes.FirstOrDefaultAsync(p => p.Id == attachmentPurposeId);
        if (purpose is null)
        {
            return BadRequest("Nepostojeća namjena privitka.");
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Datoteka je obavezna.");
        }

        var isPhotoPurpose = purpose.Name is PurposeFotografijaPrije or PurposeFotografijaPoslije;
        var isDocumentPurpose = purpose.Name == PurposeDokument;

        var allowedContentTypes = isPhotoPurpose
            ? AllowedImageContentTypes
            : isDocumentPurpose
                ? AllowedDocumentContentTypes
                : Array.Empty<string>();

        if (!allowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest("Nedopušten tip datoteke za ovu namjenu.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest("Datoteka je prevelika, maksimalno 10 MB.");
        }

        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var extension = Path.GetExtension(file.FileName);
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, storedFileName);

        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new Attachment
        {
            FaultReportId = faultReportId,
            InterventionId = interventionId,
            AttachmentPurposeId = attachmentPurposeId,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedByAppUserId = SystemAppUserId
        };

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync();

        var createdDto = await _context.Attachments
            .Where(a => a.Id == attachment.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetAttachments), new { faultReportId }, createdDto);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var attachment = await _context.Attachments.FirstOrDefaultAsync(a => a.Id == id);
        if (attachment is null)
        {
            return NotFound();
        }

        var physicalPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", attachment.StoredFileName);
        if (System.IO.File.Exists(physicalPath))
        {
            System.IO.File.Delete(physicalPath);
        }
        else
        {
            _logger.LogWarning(
                "Fizička datoteka \"{StoredFileName}\" nije pronađena na disku prilikom brisanja privitka Id={AttachmentId}.",
                attachment.StoredFileName, id);
        }

        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
