using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;

namespace Project.Data;

// Province -> Town -> Suburb (script 012_address_normalization.sql).
// Kept in its own partial file so re-scaffolding the main context never loses it.
public partial class ApplicationDbContext
{
    public virtual DbSet<Province> Provinces { get; set; }

    public virtual DbSet<Town> Towns { get; set; }

    public virtual DbSet<Suburb> Suburbs { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Province>(entity =>
        {
            entity.ToTable("Province");
            entity.HasKey(e => e.ProvinceId);
            entity.Property(e => e.ProvinceId).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Town>(entity =>
        {
            entity.ToTable("Town");
            entity.HasKey(e => e.TownId);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.HasOne(d => d.Province).WithMany(p => p.Towns)
                .HasForeignKey(d => d.ProvinceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasOne(d => d.Suburb).WithMany().HasForeignKey(d => d.SuburbId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AdminProfile>(entity =>
        {
            entity.HasOne(d => d.Suburb).WithMany().HasForeignKey(d => d.SuburbId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Suburb>(entity =>
        {
            entity.ToTable("Suburb");
            entity.HasKey(e => e.SuburbId);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(4).IsFixedLength().IsUnicode(false);
            entity.HasOne(d => d.Town).WithMany(p => p.Suburbs)
                .HasForeignKey(d => d.TownId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }
}
