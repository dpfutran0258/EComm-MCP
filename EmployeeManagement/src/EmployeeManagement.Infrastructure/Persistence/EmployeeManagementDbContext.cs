using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Infrastructure.Persistence;

public class EmployeeManagementDbContext : DbContext
{
    public EmployeeManagementDbContext(DbContextOptions<EmployeeManagementDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("employees");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.FullName)
                .HasColumnName("full_name")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .HasColumnName("email")
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Department)
                .HasColumnName("department")
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Salary)
                .HasColumnName("salary")
                .HasPrecision(18, 2);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                
                ;

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Department);
            entity.HasIndex(e => new { e.FullName, e.Department });
        });
    }
}

