using Microsoft.EntityFrameworkCore;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Project>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.OwnerId);

        modelBuilder.Entity<ChatSession>()
            .HasOne(s => s.User)
            .WithMany(u => u.ChatSessions)
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<ChatSession>()
            .HasOne(s => s.Project)
            .WithMany(p => p.ChatSessions)
            .HasForeignKey(s => s.ProjectId);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.Project)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.ProjectId);

        // Create composite index on Documents for faster lookups
        modelBuilder.Entity<Document>()
            .HasIndex(d => new { d.ProjectId, d.Path });
    }
}