using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotesHubApi.Models
{
    public partial class Timesheet
    {
        public Guid TimesheetId { get; set; }
        public Guid LoginId { get; set; }
        public Guid TaskId { get; set; }
        public Guid ProjectId { get; set; }
        public DateOnly Date { get; set; }
        public decimal HoursWorked { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        // Navigation properties
        [ForeignKey("ProjectId")]
        public virtual ProjectHead Project { get; set; }

        [ForeignKey("TaskId")]
        public virtual Task Task { get; set; }
    }
}
