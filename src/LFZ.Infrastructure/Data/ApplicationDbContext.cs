using LFZ.Application.Abstractions;
using LFZ.Domain.Entities;
using LFZ.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LFZ.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PlotRequest> PlotRequests => Set<PlotRequest>();
    public DbSet<PlotTenantBlock> PlotTenantBlocks => Set<PlotTenantBlock>();
    public DbSet<PlotStatusHistory> PlotStatusHistory => Set<PlotStatusHistory>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Name).IsRequired().HasMaxLength(200);
            entity.Property(tenant => tenant.LegalName).HasMaxLength(200);
            entity.Property(tenant => tenant.Contact).HasMaxLength(500);
            entity.Property(tenant => tenant.Industry).HasMaxLength(120);
        });

        builder.Entity<Plot>(entity =>
        {
            entity.HasKey(plot => plot.Id);
            entity.HasIndex(plot => plot.Code).IsUnique();
            entity.Property(plot => plot.Code).IsRequired().HasMaxLength(50);
            entity.Property(plot => plot.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(plot => plot.LandUseType).IsRequired().HasMaxLength(100);
            entity.Property(plot => plot.Phase).HasMaxLength(100);
            entity.Property(plot => plot.HatchColor).HasMaxLength(20);
            entity.Property(plot => plot.AreaHectares).HasColumnType("decimal(18,4)");
            entity.Property(plot => plot.Status).HasConversion<string>().IsRequired().HasMaxLength(50);
            entity.Property(plot => plot.Boundary).HasColumnType("geometry");
            entity.Property(plot => plot.SvgPath).HasColumnType("nvarchar(max)");
            entity.Property(plot => plot.Centroid).HasColumnType("geometry");
            entity.Property(plot => plot.RowVersion).IsRowVersion();

            entity.HasOne(plot => plot.CurrentTenant)
                .WithMany(tenant => tenant.CurrentPlots)
                .HasForeignKey(plot => plot.CurrentTenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PlotRequest>(entity =>
        {
            entity.HasKey(request => request.Id);
            entity.Property(request => request.RequestedByUserId).IsRequired();
            entity.Property(request => request.RequestType).HasConversion<string>().HasMaxLength(50);
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(request => request.IntendedUse).HasMaxLength(1000);
            entity.Property(request => request.Notes).HasMaxLength(2000);

            entity.HasOne(request => request.Plot)
                .WithMany(plot => plot.Requests)
                .HasForeignKey(request => request.PlotId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(request => request.Tenant)
                .WithMany(tenant => tenant.PlotRequests)
                .HasForeignKey(request => request.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(request => new { request.PlotId, request.Status });
        });

        builder.Entity<PlotTenantBlock>(entity =>
        {
            entity.HasKey(block => new { block.PlotId, block.TenantId });
            entity.Property(block => block.Notes).HasMaxLength(1000);

            entity.HasOne(block => block.Plot)
                .WithMany(plot => plot.TenantBlocks)
                .HasForeignKey(block => block.PlotId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(block => block.Tenant)
                .WithMany(tenant => tenant.PlotTenantBlocks)
                .HasForeignKey(block => block.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlotStatusHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.FromStatus).HasConversion<string>().HasMaxLength(50);
            entity.Property(history => history.ToStatus).HasConversion<string>().HasMaxLength(50);
            entity.Property(history => history.ActorUserId).IsRequired();
            entity.Property(history => history.Reason).HasMaxLength(1000);

            entity.HasOne(history => history.Plot)
                .WithMany(plot => plot.StatusHistory)
                .HasForeignKey(history => history.PlotId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(history => history.Tenant)
                .WithMany()
                .HasForeignKey(history => history.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(history => history.OriginatingRequest)
                .WithMany()
                .HasForeignKey(history => history.OriginatingRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(history => new { history.PlotId, history.ChangedAtUtc });
        });

        builder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(setting => setting.Id);
            entity.HasIndex(setting => setting.Key).IsUnique();
            entity.Property(setting => setting.Key).IsRequired().HasMaxLength(150);
            entity.Property(setting => setting.Value).IsRequired().HasMaxLength(2000);
            entity.Property(setting => setting.ValueType).IsRequired().HasMaxLength(50);
            entity.Property(setting => setting.Description).HasMaxLength(500);
        });
    }
}
