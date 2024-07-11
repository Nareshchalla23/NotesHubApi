using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NotesHubApi.Models;

public partial class CollabPlatformDbContext : DbContext
{
    public CollabPlatformDbContext()
    {
    }

    public CollabPlatformDbContext(DbContextOptions<CollabPlatformDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<GitHubLogin> GitHubLogins { get; set; }

    public virtual DbSet<GoogleLogin> GoogleLogins { get; set; }

    public virtual DbSet<Jwtlogin> Jwtlogins { get; set; }

    public virtual DbSet<ProjectHead> ProjectHeads { get; set; }

    public virtual DbSet<Task> Tasks { get; set; }

    public virtual DbSet<Timesheet> Timesheets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=CHANDU;Initial Catalog=CollabPlatformDB;Integrated Security=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitHubLogin>(entity =>
        {
            entity.HasKey(e => e.LoginId).HasName("PK__GitHubLo__4DDA28188BBF941C");

            entity.HasIndex(e => e.LoginId, "IX_GitHubLogins_LoginId");

            entity.HasIndex(e => e.GitHubId, "UQ__GitHubLo__32315B2EC84D1ED4").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__GitHubLo__A9D1053439699534").IsUnique();

            entity.Property(e => e.LoginId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AccessToken).HasMaxLength(255);
            entity.Property(e => e.AvatarUrl).HasMaxLength(2048);
            entity.Property(e => e.ClientId)
                .HasMaxLength(255)
                .HasDefaultValue("");
            entity.Property(e => e.Code).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.GitHubId)
                .HasMaxLength(255)
                .HasColumnName("GitHubID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasDefaultValue("");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasDefaultValue("");
        });

        modelBuilder.Entity<GoogleLogin>(entity =>
        {
            entity.HasKey(e => e.LoginId).HasName("PK__GoogleLo__4DDA28189A7FAF00");

            entity.HasIndex(e => e.Email, "IX_GoogleLogins_Email").IsUnique();

            entity.HasIndex(e => e.LoginId, "IX_GoogleLogins_LoginId");

            entity.HasIndex(e => e.Sub, "IX_GoogleLogins_Sub").IsUnique();

            entity.Property(e => e.LoginId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ClientId)
                .HasMaxLength(255)
                .HasDefaultValue("");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Credential)
                .HasMaxLength(2048)
                .HasDefaultValue("");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Picture).HasMaxLength(2048);
            entity.Property(e => e.SelectBy).HasMaxLength(50);
            entity.Property(e => e.Sub).HasMaxLength(255);
        });

        modelBuilder.Entity<Jwtlogin>(entity =>
        {
            entity.HasKey(e => e.LoginId).HasName("PK__JWTLogin__4DDA28186D4D3B39");

            entity.ToTable("JWTLogins");

            entity.HasIndex(e => e.LoginId, "IX_Jwtlogins_LoginId");

            entity.HasIndex(e => e.Email, "UQ__JWTLogin__A9D105346857AA5D").IsUnique();

            entity.Property(e => e.LoginId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(255);
        });

        modelBuilder.Entity<ProjectHead>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("PK__ProjectH__761ABEF0F4DD1C37");

            entity.ToTable("ProjectHead");

            entity.HasIndex(e => e.LoginId, "IX_ProjectHead_LoginId");

            entity.Property(e => e.ProjectId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ProjectName).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__Task__7C6949B1197A5ADB");

            entity.ToTable("Task");

            entity.HasIndex(e => e.LoginId, "IX_Task_LoginId");

            entity.HasIndex(e => e.ProjectId, "IX_Task_ProjectId");

            entity.HasIndex(e => e.TaskName, "IX_Task_TaskName");

            entity.Property(e => e.TaskId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.LastModifiedOn).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TaskName).HasMaxLength(255);

            entity.HasOne(d => d.Project).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Task_ProjectHead");
        });

        modelBuilder.Entity<Timesheet>(entity =>
        {
            entity.HasKey(e => e.TimesheetId).HasName("PK__Timeshee__848CBE2D8F4C9CC5");

            entity.ToTable("Timesheet");

            entity.HasIndex(e => e.Date, "IX_Timesheet_Date");

            entity.HasIndex(e => e.LoginId, "IX_Timesheet_LoginId");

            entity.HasIndex(e => e.ProjectId, "IX_Timesheet_ProjectId");

            entity.HasIndex(e => e.TaskId, "IX_Timesheet_TaskId");

            entity.Property(e => e.TimesheetId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.HoursWorked).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.LastModifiedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Project).WithMany(p => p.Timesheets)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Timesheet_Project");

            entity.HasOne(d => d.Task).WithMany(p => p.Timesheets)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Timesheet_Task");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
