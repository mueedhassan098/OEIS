using Microsoft.EntityFrameworkCore;
using OEIS.Domain.Entities;

namespace OEIS.Infrastructure.Data;

public class OeisDbContext : DbContext
{
    public OeisDbContext(DbContextOptions<OeisDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<Salary> Salaries => Set<Salary>();
    public DbSet<PayScale> PayScales => Set<PayScale>();
    public DbSet<PortalActivity> PortalActivities => Set<PortalActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId);
            entity.Property(e => e.EmployeeCode).HasMaxLength(30);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PasswordResetToken).HasMaxLength(128);
            entity.Property(e => e.PasswordResetTokenExpiresUtc).HasColumnType("datetime2");
            entity.Property(e => e.Department).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Salary).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.RegistrationStatus).IsRequired().HasMaxLength(20).HasDefaultValue("Approved");
            entity.Property(e => e.RegistrationReviewNote).HasMaxLength(300);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2");
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.ProfileImageUrl);

            entity.Property(e => e.AnnualLeaveQuota);
            entity.Property(e => e.RemainingLeaves);

            entity.HasOne(e => e.PayScale)
                .WithMany(p => p.Employees)
                .HasForeignKey(e => e.PayScaleId)
                .OnDelete(DeleteBehavior.SetNull);

            // Seed a default admin account for initial login
            entity.HasData(new Employee
            {
                EmployeeId = 1000,
                FullName = "System Admin",
                Email = "admin@company.com",
                PasswordHash = "Admin@123",
                Department = "IT",
                Salary = 0m,
                Role = "Admin",
                RegistrationStatus = "Approved",
                CreatedAt = DateTime.UtcNow,
                AnnualLeaveQuota = 30,
                RemainingLeaves = 30
            });
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(a => a.AttendanceId);
            entity.Property(a => a.EntryTime).IsRequired().HasColumnType("datetime2");
            entity.Property(a => a.ExitTime).HasColumnType("datetime2");
            entity.Property(a => a.Date).HasColumnType("date");

            entity.HasOne(a => a.Employee)
                .WithMany(e => e.Attendances)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // One attendance record per employee per day
            entity.HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique();
        });

        modelBuilder.Entity<Leave>(entity =>
        {
            entity.HasKey(l => l.LeaveId);
            entity.Property(l => l.LeaveType).IsRequired().HasMaxLength(50);
            entity.Property(l => l.Status).IsRequired().HasMaxLength(20);
            entity.Property(l => l.FromDate).HasColumnType("date");
            entity.Property(l => l.ToDate).HasColumnType("date");

            entity.HasOne(l => l.Employee)
                .WithMany(e => e.Leaves)
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Salary>(entity =>
        {
            entity.HasKey(s => s.SalaryId);
            entity.Property(s => s.Month).IsRequired().HasMaxLength(20);
            entity.Property(s => s.Amount).HasColumnType("decimal(18,2)");
            entity.Property(s => s.BasicAmount).HasColumnType("decimal(18,2)");
            entity.Property(s => s.OvertimeAmount).HasColumnType("decimal(18,2)");
            entity.Property(s => s.Bonus).HasColumnType("decimal(18,2)");
            entity.Property(s => s.Deductions).HasColumnType("decimal(18,2)");
            entity.Property(s => s.GeneratedOn).HasColumnType("datetime2");

            entity.HasOne(s => s.Employee)
                .WithMany(e => e.Salaries)
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PayScale>(entity =>
        {
            entity.HasKey(p => p.PayScaleId);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(50);
            entity.Property(p => p.BaseSalary).HasColumnType("decimal(18,2)");
            entity.Property(p => p.AnnualLeaveQuota);
        });

        modelBuilder.Entity<PortalActivity>(entity =>
        {
            entity.HasKey(p => p.PortalActivityId);
            entity.Property(p => p.Title).IsRequired().HasMaxLength(120);
            entity.Property(p => p.Message).IsRequired().HasMaxLength(500);
            entity.Property(p => p.Type).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Link).HasMaxLength(200);
            entity.Property(p => p.CreatedAtUtc).HasColumnType("datetime2");
        });
    }
}
