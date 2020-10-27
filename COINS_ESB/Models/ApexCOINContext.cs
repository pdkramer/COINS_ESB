using Microsoft.EntityFrameworkCore;

namespace COINS_ESB.Models
{
    public partial class ApexCOINContext : DbContext
    {
        public ApexCOINContext()
        {
        }

        public ApexCOINContext(DbContextOptions<ApexCOINContext> options)
            : base(options)
        {
        }

        public virtual DbSet<CoinsXml> CoinsXml { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CoinsXml>(entity =>
            {
                entity.ToTable("CoinsXML");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.RawXml)
                    .HasColumnName("RawXML")
                    .IsUnicode(false);

                entity.Property(e => e.RecDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");
            });

            modelBuilder.Entity<CoinsArchiveXml>(entity =>
            {
                entity.ToTable("CoinsArchiveXML");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.RawXml)
                    .HasColumnName("RawXML")
                    .IsUnicode(false);

                entity.Property(e => e.RecDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.ArchiveDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");
            });
        }
    }
}
