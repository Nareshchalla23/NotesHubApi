using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoMapper;
using NotesHubApi.DTO;
using NotesHubApi.Models;
using NotesHubApi.Profiles;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading.Tasks;

namespace NotesHubApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TimesheetController : ControllerBase
    {
        private readonly ILogger<TimesheetController> _logger;
        private readonly IMapper _mapper;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ISignalRService _signalRService;
        private readonly CollabPlatformDbContext _dbContext;

        public TimesheetController(ILogger<TimesheetController> logger, IMapper mapper, IRabbitMQService rabbitMQService,
                                   ISignalRService signalRService, CollabPlatformDbContext dbContext)
        {
            _logger = logger;
            _mapper = mapper;
            _rabbitMQService = rabbitMQService;
            _signalRService = signalRService;
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTimesheet([FromBody] TimesheetDTO.Timesheet timesheetDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (!await ValidateTimesheetRelationsAsync(timesheetDto))
                    return BadRequest("Invalid Task, Project, or LoginId.");

                var timesheet = _mapper.Map<Timesheet>(timesheetDto);
                timesheet.TimesheetId = Guid.NewGuid();
                timesheet.CreatedAt = DateTime.UtcNow;
                timesheet.LastModifiedAt = timesheet.CreatedAt;

                _dbContext.Timesheets.Add(timesheet);
                await _dbContext.SaveChangesAsync();

                await NotifyTimesheetChangeAsync("timesheet_created", "TimesheetCreated", timesheet);

                return CreatedAtAction(nameof(GetTimesheet), new { id = timesheet.TimesheetId },
                                       _mapper.Map<TimesheetDTO.Timesheet>(timesheet));
            }
            catch (Exception ex)
            {
                return HandleException(ex, "creating", "TimesheetCreationFailed");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTimesheet(Guid id)
        {
            try
            {
                var timesheet = await _dbContext.Timesheets.FindAsync(id);
                if (timesheet == null)
                    return NotFound($"Timesheet with ID '{id}' not found.");

                return Ok(_mapper.Map<TimesheetDTO.Timesheet>(timesheet));
            }
            catch (Exception ex)
            {
                return HandleException(ex, "retrieving");
            }
        }
        [HttpGet("user/{loginId}")]
        public async Task<IActionResult> GetTimesheetsByLoginId(Guid loginId)
        {
            try
            {
                var timesheets = await _dbContext.Timesheets
                    .Where(t => t.LoginId == loginId)
                    .OrderByDescending(t => t.Date)
                    .ThenByDescending(t => t.StartTime)
                    .ToListAsync();

                if (timesheets == null || !timesheets.Any())
                {
                    return NotFound($"No timesheets found for LoginId: {loginId}");
                }

                var timesheetDtos = _mapper.Map<List<TimesheetDTO.Timesheet>>(timesheets);

                return Ok(timesheetDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving timesheets for LoginId: {LoginId}", loginId);
                return StatusCode(500, "An error occurred while retrieving the timesheets. Please try again later.");
            }
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTimesheet(Guid id, [FromBody] TimesheetDTO.Timesheet timesheetDto)
        {
            if (id != timesheetDto.TimesheetId || !ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var timesheet = await _dbContext.Timesheets.FindAsync(id);
                if (timesheet == null)
                    return NotFound($"Timesheet with ID '{id}' not found.");

                _mapper.Map(timesheetDto, timesheet);
                timesheet.LastModifiedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                await NotifyTimesheetChangeAsync("timesheet_updated", "TimesheetUpdated", timesheet);

                return Ok(_mapper.Map<TimesheetDTO.Timesheet>(timesheet));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return HandleException(ex, "updating", "TimesheetUpdateFailed", 409);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "updating", "TimesheetUpdateFailed");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTimesheet(Guid id)
        {
            try
            {
                var timesheet = await _dbContext.Timesheets.FindAsync(id);
                if (timesheet == null)
                    return NotFound($"Timesheet with ID '{id}' not found.");

                _dbContext.Timesheets.Remove(timesheet);
                await _dbContext.SaveChangesAsync();
                await NotifyTimesheetChangeAsync("timesheet_deleted", "TimesheetDeleted", timesheet);

                return NoContent();
            }
            catch (Exception ex)
            {
                return HandleException(ex, "deleting", "TimesheetDeletionFailed");
            }
        }

        [HttpPatch("{id}/hours")]
        public async Task<IActionResult> UpdateTimesheetHours(Guid id, [FromBody] TimesheetDTO.TimesheetPatchDTO patchDto)
        {
            if (patchDto == null)
                return BadRequest("Patch data is null.");

            try
            {
                var timesheet = await _dbContext.Timesheets.FindAsync(id);
                if (timesheet == null)
                    return NotFound($"Timesheet with ID '{id}' not found.");

                if (patchDto.HoursWorked <= 0 || patchDto.HoursWorked > 24)
                    return BadRequest("HoursWorked must be greater than 0 and not exceed 24.");

                timesheet.HoursWorked = patchDto.HoursWorked;
                timesheet.LastModifiedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                await NotifyTimesheetChangeAsync("timesheet_hours_updated", "TimesheetHoursUpdated", timesheet);

                return Ok(_mapper.Map<TimesheetDTO.Timesheet>(timesheet));
            }
            catch (Exception ex)
            {
                return HandleException(ex, "updating hours", "TimesheetHoursUpdateFailed");
            }
        }

        private async Task<bool> ValidateTimesheetRelationsAsync(TimesheetDTO.Timesheet timesheetDto)
        {
            var taskExists = await _dbContext.Tasks.AnyAsync(t => t.TaskId == timesheetDto.TaskId);
            var projectExists = await _dbContext.ProjectHeads.AnyAsync(p => p.ProjectId == timesheetDto.ProjectId);
            var loginExists = await IsValidLoginIdAsync(timesheetDto.LoginId);

            return taskExists && projectExists && loginExists;
        }

        private async Task<bool> IsValidLoginIdAsync(Guid loginId)
        {
            return await _dbContext.GitHubLogins.AnyAsync(g => g.LoginId == loginId) ||
                   await _dbContext.GoogleLogins.AnyAsync(g => g.LoginId == loginId) ||
                   await _dbContext.Jwtlogins.AnyAsync(j => j.LoginId == loginId);
        }

        private async System.Threading.Tasks.Task NotifyTimesheetChangeAsync(string rabbitMqTopic, string signalREvent, Timesheet timesheet)
        {
            var timesheetDto = _mapper.Map<TimesheetDTO.Timesheet>(timesheet);
            var json = JsonSerializer.Serialize(timesheetDto, new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                MaxDepth = 64
            });

            _rabbitMQService.PublishMessage(rabbitMqTopic, json);
            await _signalRService.NotifyClientsAsync(signalREvent, json);
            _logger.LogInformation("{Event} for TimesheetId: {TimesheetId}", signalREvent, timesheet.TimesheetId);
        }

        private IActionResult HandleException(Exception ex, string action, string notificationEvent = null, int statusCode = 500)
        {
            _logger.LogError(ex, "Error occurred while {Action} timesheet", action);
            if (notificationEvent != null)
            {
                _signalRService.NotifyClientsAsync(notificationEvent, "An unexpected error occurred").Wait();
            }
            return StatusCode(statusCode, $"An error occurred while {action} the timesheet: {ex.Message}");
        }
    }
}
