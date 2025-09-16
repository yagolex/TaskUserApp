using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

namespace TaskUser.Api
{
    public class AppDb(DbContextOptions<AppDb> opt) : DbContext(opt)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // InMemory sometime fails with IsUnique check, 
            // so we double-check it inside our controlles
            b.Entity<User>()
                .HasIndex(x => x.Name)
                .IsUnique();

            b.Entity<TaskItem>()
                .HasIndex(x => x.Title)
                .IsUnique();

            b.Entity<TaskAssignment>()
            .HasIndex(x => new { x.TaskId, x.AssignedAt });
        }
    }
}