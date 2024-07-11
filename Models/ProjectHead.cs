using System;
using System.Collections.Generic;

namespace NotesHubApi.Models;

public partial class ProjectHead
{
    public Guid ProjectId { get; set; }

    public Guid LoginId { get; set; }

    public string ProjectName { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    public virtual ICollection<Timesheet> Timesheets { get; set; } = new List<Timesheet>();
}
