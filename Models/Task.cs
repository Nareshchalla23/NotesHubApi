using System;
using System.Collections.Generic;

namespace NotesHubApi.Models;

public partial class Task
{
    public Guid TaskId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid LoginId { get; set; }

    public string TaskName { get; set; } = null!;

    public string? TaskDescription { get; set; }

    public DateTime LastModifiedOn { get; set; }

    public virtual ProjectHead Project { get; set; } = null!;

    public virtual ICollection<Timesheet> Timesheets { get; set; } = new List<Timesheet>();
}
