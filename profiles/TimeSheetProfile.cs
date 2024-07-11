using AutoMapper;
using NotesHubApi.Models;
using NotesHubApi.DTO;
using System;
using Task = NotesHubApi.Models.Task;

namespace NotesHubApi.Profiles
{
    public class TimesheetProfile : Profile
    {
        public TimesheetProfile()
        {
            CreateMap<TimesheetDTO.Project, ProjectHead>()
                .ForMember(dest => dest.ProjectId, opt => opt.Ignore())
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => src.LoginId))
                .ForMember(dest => dest.ProjectName, opt => opt.MapFrom(src => src.ProjectName))
                .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate.HasValue ? DateOnly.FromDateTime(src.StartDate.Value) : (DateOnly?)null))
                .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate.HasValue ? DateOnly.FromDateTime(src.EndDate.Value) : (DateOnly?)null))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status));

            CreateMap<ProjectHead, TimesheetDTO.Project>()
                .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.ProjectId))
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => src.LoginId))
                .ForMember(dest => dest.ProjectName, opt => opt.MapFrom(src => src.ProjectName))
                .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate.HasValue ? src.StartDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null))
                .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate.HasValue ? src.EndDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status));

            CreateMap<TimesheetDTO.Task, Task>()
                .ForMember(dest => dest.TaskId, opt => opt.Ignore())
                .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.ProjectId))
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => src.LoginId))
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.TaskName))
                .ForMember(dest => dest.TaskDescription, opt => opt.MapFrom(src => src.TaskDescription))
                .ForMember(dest => dest.LastModifiedOn, opt => opt.MapFrom(src => src.LastModifiedOn));

            CreateMap<Task, TimesheetDTO.Task>()
                .ForMember(dest => dest.TaskId, opt => opt.MapFrom(src => src.TaskId))
                .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.ProjectId))
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => src.LoginId))
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.TaskName))
                .ForMember(dest => dest.TaskDescription, opt => opt.MapFrom(src => src.TaskDescription))
                .ForMember(dest => dest.LastModifiedOn, opt => opt.MapFrom(src => src.LastModifiedOn));

            CreateMap<TimesheetDTO.Timesheet, Timesheet>()
                .ForMember(dest => dest.TimesheetId, opt => opt.Ignore())
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => src.LoginId))
                .ForMember(dest => dest.TaskId, opt => opt.MapFrom(src => src.TaskId))
                .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.ProjectId))
                .ForMember(dest => dest.Date, opt => opt.MapFrom(src => DateOnly.FromDateTime(src.Date)))
                .ForMember(dest => dest.HoursWorked, opt => opt.MapFrom(src => src.HoursWorked))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => ParseTimeSpan(src.StartTime)))
                .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => ParseTimeSpan(src.EndTime)))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.LastModifiedAt, opt => opt.Ignore());

            CreateMap<Timesheet, TimesheetDTO.Timesheet>()
                .ForMember(dest => dest.TimesheetId, opt => opt.MapFrom(src => src.TimesheetId))
                .ForMember(dest => dest.LoginId, opt => opt.MapFrom(src => src.LoginId))
                .ForMember(dest => dest.TaskId, opt => opt.MapFrom(src => src.TaskId))
                .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.ProjectId))
                .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Date.ToDateTime(TimeOnly.MinValue)))
                .ForMember(dest => dest.HoursWorked, opt => opt.MapFrom(src => src.HoursWorked))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => FormatTimeSpan(src.StartTime)))
                .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => FormatTimeSpan(src.EndTime)));
        }

        private static TimeSpan? ParseTimeSpan(string time)
        {
            return TimeSpan.TryParse(time, out var result) ? result : (TimeSpan?)null;
        }

        private static string FormatTimeSpan(TimeSpan? time)
        {
            return time?.ToString(@"hh\:mm");
        }
    }
}
