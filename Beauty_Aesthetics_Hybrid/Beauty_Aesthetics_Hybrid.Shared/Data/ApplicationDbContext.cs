using Microsoft.EntityFrameworkCore;
using Beauty_Aesthetics_WebPos.Models;

namespace Beauty_Aesthetic_WebPos.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CustomerRating> CustomerRatings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CustomerRating>(entity =>
            {
                entity.ToTable("tbl_CustomerRating");
                entity.Property(e => e.Comment).HasMaxLength(500);
                entity.Property(e => e.MasterAccountID).HasMaxLength(15).IsRequired();
                entity.Property(e => e.AccountName).HasMaxLength(256).IsRequired();
            });
        }
    }
}