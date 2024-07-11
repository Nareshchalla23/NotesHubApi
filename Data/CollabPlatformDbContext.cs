using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NotesHubApi.Models;

namespace NotesHubApi.Data;

public partial class CollabPlatformDbContext : DbContext
{
    public CollabPlatformDbContext()
    {
    }

    public CollabPlatformDbContext(DbContextOptions<CollabPlatformDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Timesheet> Timesheets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=CHANDU;Initial Catalog=CollabPlatformDB;Integrated Security=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
