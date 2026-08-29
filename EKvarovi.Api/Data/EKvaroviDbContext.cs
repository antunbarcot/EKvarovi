using EKvarovi.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Data;

public class EKvaroviDbContext : DbContext
{
    public EKvaroviDbContext(DbContextOptions<EKvaroviDbContext> options) : base(options)
    {
    }

    public DbSet<LocationType> LocationTypes => Set<LocationType>();
    public DbSet<FaultType> FaultTypes => Set<FaultType>();
    public DbSet<FaultPriority> FaultPriorities => Set<FaultPriority>();
    public DbSet<FaultStatus> FaultStatuses => Set<FaultStatus>();
    public DbSet<InterventionStatus> InterventionStatuses => Set<InterventionStatus>();
    public DbSet<MaterialUnit> MaterialUnits => Set<MaterialUnit>();
    public DbSet<AttachmentPurpose> AttachmentPurposes => Set<AttachmentPurpose>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<FaultReport> FaultReports => Set<FaultReport>();
    public DbSet<WorkAssignment> WorkAssignments => Set<WorkAssignment>();
    public DbSet<Intervention> Interventions => Set<Intervention>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<InterventionMaterial> InterventionMaterials => Set<InterventionMaterial>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureLookups(modelBuilder);
        ConfigureCore(modelBuilder);
        ConfigureAssignmentsAndInterventions(modelBuilder);
        ConfigureAttachments(modelBuilder);
        ConfigureUsers(modelBuilder);
        SeedLookups(modelBuilder);
    }

    private static void ConfigureLookups(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocationType>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FaultType>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FaultPriority>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FaultStatus>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<InterventionStatus>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<MaterialUnit>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AttachmentPurpose>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AppRole>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });
    }

    private static void ConfigureCore(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Location>(e =>
        {
            e.HasOne(x => x.LocationType)
                .WithMany(x => x.Locations)
                .HasForeignKey(x => x.LocationTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasOne(x => x.Location)
                .WithMany(x => x.Employees)
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FaultReport>(e =>
        {
            e.HasOne(x => x.Location)
                .WithMany(x => x.FaultReports)
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Reporter)
                .WithMany(x => x.ReportedFaultReports)
                .HasForeignKey(x => x.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            // FaultTypeId je nullable FK - vrstu kvara odreduje Manager tek kod pregleda
            e.HasOne(x => x.FaultType)
                .WithMany(x => x.FaultReports)
                .HasForeignKey(x => x.FaultTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // FaultPriorityId je nullable FK - prioritet odreduje Manager tek kod pregleda
            e.HasOne(x => x.FaultPriority)
                .WithMany(x => x.FaultReports)
                .HasForeignKey(x => x.FaultPriorityId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.FaultStatus)
                .WithMany(x => x.FaultReports)
                .HasForeignKey(x => x.FaultStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAssignmentsAndInterventions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkAssignment>(e =>
        {
            // Prijave s dodjelama se ne smiju fizicki brisati - Restrict umjesto Cascade
            e.HasOne(x => x.FaultReport)
                .WithMany(x => x.WorkAssignments)
                .HasForeignKey(x => x.FaultReportId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Technician)
                .WithMany(x => x.WorkAssignments)
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedByAppUser)
                .WithMany(x => x.AssignedWorkAssignments)
                .HasForeignKey(x => x.AssignedByAppUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Preporuceni filtered unique index: najvise jedna aktivna dodjela po prijavi.
            // SQLite podrzava "partial index" preko HasFilter - EF Core ga prevodi u
            // CREATE UNIQUE INDEX ... WHERE "IsActive" = 1
            e.HasIndex(x => x.FaultReportId)
                .IsUnique()
                .HasFilter("\"IsActive\" = 1")
                .HasDatabaseName("IX_WorkAssignments_FaultReportId_ActiveOnly");
        });

        modelBuilder.Entity<Intervention>(e =>
        {
            // KRITICNO: Intervention se vezuje na WorkAssignment, NE direktno na FaultReport
            e.HasOne(x => x.WorkAssignment)
                .WithMany(x => x.Interventions)
                .HasForeignKey(x => x.WorkAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.InterventionStatus)
                .WithMany(x => x.Interventions)
                .HasForeignKey(x => x.InterventionStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.DurationMinutes);
        });

        modelBuilder.Entity<Material>(e =>
        {
            e.HasOne(x => x.MaterialUnit)
                .WithMany(x => x.Materials)
                .HasForeignKey(x => x.MaterialUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InterventionMaterial = M:N spojna tablica Intervention <-> Material s Quantity poljem
        modelBuilder.Entity<InterventionMaterial>(e =>
        {
            e.HasOne(x => x.Intervention)
                .WithMany(x => x.InterventionMaterials)
                .HasForeignKey(x => x.InterventionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Material)
                .WithMany(x => x.InterventionMaterials)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        });
    }

    private static void ConfigureAttachments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attachment>(e =>
        {
            e.HasOne(x => x.FaultReport)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.FaultReportId)
                .OnDelete(DeleteBehavior.Restrict);

            // InterventionId je opcionalna veza - ne smijemo lancano obrisati privitke
            // (brisanje privitka provodi API servis, ukljucujuci fizicku datoteku)
            e.HasOne(x => x.Intervention)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.InterventionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AttachmentPurpose)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AttachmentPurposeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UploadedByAppUser)
                .WithMany(x => x.UploadedAttachments)
                .HasForeignKey(x => x.UploadedByAppUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();

            // EmployeeId je opcionalna veza - ne svaki AppUser ima poslovni profil (npr. cisti Admin)
            e.HasOne(x => x.Employee)
                .WithOne(x => x.AppUser)
                .HasForeignKey<AppUser>(x => x.EmployeeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AppUserRole = M:N spojna tablica s composite primarnim kljucem (AppUserId, AppRoleId)
        modelBuilder.Entity<AppUserRole>(e =>
        {
            e.HasKey(x => new { x.AppUserId, x.AppRoleId });

            e.HasOne(x => x.AppUser)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.AppRole)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.AppRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void SeedLookups(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocationType>().HasData(
            new LocationType { Id = 1, Name = "Upravna zgrada" },
            new LocationType { Id = 2, Name = "Škola" },
            new LocationType { Id = 3, Name = "Zdravstvena ustanova" },
            new LocationType { Id = 4, Name = "Skladište" }
        );

        modelBuilder.Entity<FaultType>().HasData(
            new FaultType { Id = 1, Name = "Elektrika" },
            new FaultType { Id = 2, Name = "Voda" },
            new FaultType { Id = 3, Name = "Grijanje" },
            new FaultType { Id = 4, Name = "Mreža" },
            new FaultType { Id = 5, Name = "Građevinski radovi" },
            new FaultType { Id = 6, Name = "Ostalo" }
        );

        modelBuilder.Entity<FaultPriority>().HasData(
            new FaultPriority { Id = 1, Name = "Nizak", SortOrder = 1 },
            new FaultPriority { Id = 2, Name = "Srednji", SortOrder = 2 },
            new FaultPriority { Id = 3, Name = "Visok", SortOrder = 3 },
            new FaultPriority { Id = 4, Name = "Kritičan", SortOrder = 4 }
        );

        modelBuilder.Entity<FaultStatus>().HasData(
            new FaultStatus { Id = 1, Name = "Zaprimljeno", SortOrder = 1 },
            new FaultStatus { Id = 2, Name = "Pregledano", SortOrder = 2 },
            new FaultStatus { Id = 3, Name = "Dodijeljeno", SortOrder = 3 },
            new FaultStatus { Id = 4, Name = "U radu", SortOrder = 4 },
            new FaultStatus { Id = 5, Name = "Riješeno", SortOrder = 5 },
            new FaultStatus { Id = 6, Name = "Zatvoreno", SortOrder = 6 }
        );

        modelBuilder.Entity<InterventionStatus>().HasData(
            new InterventionStatus { Id = 1, Name = "Planirana" },
            new InterventionStatus { Id = 2, Name = "U tijeku" },
            new InterventionStatus { Id = 3, Name = "Završena" },
            new InterventionStatus { Id = 4, Name = "Neuspješna" }
        );

        modelBuilder.Entity<MaterialUnit>().HasData(
            new MaterialUnit { Id = 1, Name = "Komad", Abbreviation = "kom" },
            new MaterialUnit { Id = 2, Name = "Metar", Abbreviation = "m" },
            new MaterialUnit { Id = 3, Name = "Litra", Abbreviation = "l" },
            new MaterialUnit { Id = 4, Name = "Kilogram", Abbreviation = "kg" },
            new MaterialUnit { Id = 5, Name = "Paket", Abbreviation = "pak" }
        );

        modelBuilder.Entity<AttachmentPurpose>().HasData(
            new AttachmentPurpose { Id = 1, Name = "FotografijaPrije" },
            new AttachmentPurpose { Id = 2, Name = "FotografijaPoslije" },
            new AttachmentPurpose { Id = 3, Name = "Dokument" }
        );

        modelBuilder.Entity<AppRole>().HasData(
            new AppRole { Id = 1, Name = "Admin" },
            new AppRole { Id = 2, Name = "Manager" },
            new AppRole { Id = 3, Name = "Technician" },
            new AppRole { Id = 4, Name = "Reporter" }
        );

        // Privremeni sistemski AppUser dok JWT autentikacija nije ozicena u API-ju.
        // IsActive = false - ovaj racun se ne moze koristiti za prijavu, sluzi samo
        // kao AssignedByAppUserId placeholder dok WorkAssignmentsController ne
        // cita stvarni identitet iz JWT claima.
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = 1,
                Email = "sistem@ekvarovi.local",
                PasswordHash = "SISTEM-RACUN-BEZ-PRIJAVE",
                DisplayName = "Sistem",
                IsActive = false,
                EmployeeId = null,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
