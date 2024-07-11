namespace NotesHubApi.DTO
{
    public class TimesheetDTO
    {
        public class Project
        {
            public Guid ProjectId { get; set; }
            public Guid LoginId { get; set; }
            public string ProjectName { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public string Status { get; set; }
        }

        public class Task
        {
            public Guid TaskId { get; set; }
            public Guid ProjectId { get; set; }
            public Guid LoginId { get; set; }
            public string TaskName { get; set; }
            public string TaskDescription { get; set; }
            public DateTime LastModifiedOn { get; set; }
        }

        public class Timesheet
        {
            public Guid TimesheetId { get; set; }
            public Guid LoginId { get; set; }
            public Guid TaskId { get; set; }
            public Guid ProjectId { get; set; }
            public DateTime Date { get; set; }
            public decimal HoursWorked { get; set; }
            public string Description { get; set; }
            public string Status { get; set; }
            public string StartTime { get; set; } // Changed to string
            public string EndTime { get; set; } // Changed to string
        }

        public class TimesheetPatchDTO
        {
            public decimal HoursWorked { get; set; }
            public string StartTime { get; set; } // Added
            public string EndTime { get; set; } // Added
        }
    }
}
