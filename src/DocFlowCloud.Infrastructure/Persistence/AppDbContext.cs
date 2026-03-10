using DocFlowCloud.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocFlowCloud.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Job> Jobs => Set<Job>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Job>(entity =>
            {
                entity.ToTable("Jobs");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Type).HasMaxLength(100).IsRequired();
                entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            });

        }
    }
}
