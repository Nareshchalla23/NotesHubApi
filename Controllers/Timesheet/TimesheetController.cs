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

namespace NotesHubApi.Controllers.Timesheet
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
        [HttpPatch("{id}/description")]
        public async Task<IActionResult> UpdateTimesheetDescription(Guid id, [FromBody] TimesheetDTO.TimesheetDescriptionPatchDTO patchDto)
        {
            if (patchDto == null || string.IsNullOrEmpty(patchDto.Description))
            {
                _logger.LogWarning("Patch data is null or description is empty for TimesheetId: {TimesheetId}", id);
                return BadRequest("Patch data is null or description is empty.");
            }

            try
            {
                var timesheet = await _dbContext.Timesheets.FindAsync(id);
                if (timesheet == null)
                {
                    _logger.LogWarning("Timesheet with ID '{TimesheetId}' not found.", id);
                    return NotFound($"Timesheet with ID '{id}' not found.");
                }

                timesheet.Description = patchDto.Description;
                timesheet.LastModifiedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Publish update event to RabbitMQ
                _rabbitMQService.PublishMessage("timesheet_events", new { Event = "TimesheetDescriptionUpdated", timesheet.TimesheetId, Timestamp = DateTime.UtcNow });

                // Notify clients via SignalR
                await _signalRService.NotifyClientsAsync("TimesheetDescriptionUpdated", new { timesheet.TimesheetId, Timestamp = DateTime.UtcNow });

                return Ok(_mapper.Map<TimesheetDTO.Timesheet>(timesheet));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error occurred while updating description for TimesheetId: {TimesheetId}", id);
                await _signalRService.NotifyClientsAsync("TimesheetDescriptionUpdateFailed", new { TimesheetId = id, Error = "Database error occurred", Timestamp = DateTime.UtcNow });
                return StatusCode(500, "A database error occurred while updating the description. Please try again later.");
            }
            catch (TimeoutException timeoutEx)
            {
                _logger.LogError(timeoutEx, "Timeout error occurred while updating description for TimesheetId: {TimesheetId}", id);
                await _signalRService.NotifyClientsAsync("TimesheetDescriptionUpdateFailed", new { TimesheetId = id, Error = "Timeout error occurred", Timestamp = DateTime.UtcNow });
                return StatusCode(504, "A timeout error occurred while updating the description. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating description for TimesheetId: {TimesheetId}", id);
                await _signalRService.NotifyClientsAsync("TimesheetDescriptionUpdateFailed", new { TimesheetId = id, Error = "An unexpected error occurred", Timestamp = DateTime.UtcNow });
                return StatusCode(500, "An unexpected error occurred while updating the description. Please try again later.");
            }
        }

        [HttpGet("total-hours/{loginId}")]
        public async Task<IActionResult> GetTotalHoursByLoginId(Guid loginId)
        {
            try
            {
                // Check if the loginId is valid
                if (loginId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid loginId provided: {LoginId}", loginId);
                    return BadRequest("Invalid loginId.");
                }

                // Check if the user exists
                var userExists = await _dbContext.Jwtlogins.AnyAsync(u => u.LoginId == loginId);
                if (!userExists)
                {
                    _logger.LogWarning("No user found with loginId: {LoginId}", loginId);
                    return NotFound($"No user found with loginId: {loginId}");
                }

                // Calculate total hours
                var totalHours = await _dbContext.Timesheets
                    .Where(t => t.LoginId == loginId)
                    .SumAsync(t => t.HoursWorked);

                // Publish total hours event to RabbitMQ
                _rabbitMQService.PublishMessage("timesheet_events", new { Event = "TotalHoursCalculated", LoginId = loginId, TotalHours = totalHours, Timestamp = DateTime.UtcNow });

                // Notify clients via SignalR
                await _signalRService.NotifyClientsAsync("TotalHoursCalculated", new { LoginId = loginId, TotalHours = totalHours, Timestamp = DateTime.UtcNow });

                return Ok(new { LoginId = loginId, TotalHours = totalHours });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error occurred while retrieving total hours for LoginId: {LoginId}", loginId);
                await _signalRService.NotifyClientsAsync("TotalHoursCalculationFailed", new { LoginId = loginId, Error = "Database error occurred", Timestamp = DateTime.UtcNow });
                return StatusCode(500, "A database error occurred while retrieving the total hours. Please try again later.");
            }
            catch (TimeoutException timeoutEx)
            {
                _logger.LogError(timeoutEx, "Timeout error occurred while retrieving total hours for LoginId: {LoginId}", loginId);
                await _signalRService.NotifyClientsAsync("TotalHoursCalculationFailed", new { LoginId = loginId, Error = "Timeout error occurred", Timestamp = DateTime.UtcNow });
                return StatusCode(504, "A timeout error occurred while retrieving the total hours. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving total hours for LoginId: {LoginId}", loginId);
                await _signalRService.NotifyClientsAsync("TotalHoursCalculationFailed", new { LoginId = loginId, Error = "An unexpected error occurred", Timestamp = DateTime.UtcNow });
                return StatusCode(500, "An unexpected error occurred while retrieving the total hours. Please try again later.");
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

                if (!string.IsNullOrEmpty(patchDto.StartTime))
                {
                    timesheet.StartTime = ParseTimeSpan(patchDto.StartTime);
                }

                if (!string.IsNullOrEmpty(patchDto.EndTime))
                {
                    timesheet.EndTime = ParseTimeSpan(patchDto.EndTime);
                }

                if (timesheet.StartTime.HasValue && timesheet.EndTime.HasValue)
                {
                    var duration = timesheet.EndTime.Value - timesheet.StartTime.Value;
                    timesheet.HoursWorked = (decimal)duration.TotalHours;
                }

                timesheet.LastModifiedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();


                _rabbitMQService.PublishMessage("timesheet_events", new { Event = "TimesheetHoursUpdated", timesheet.TimesheetId, Timestamp = DateTime.UtcNow });
                await _signalRService.NotifyClientsAsync("TimesheetHoursUpdated", new { timesheet.TimesheetId, Timestamp = DateTime.UtcNow });

                return Ok(_mapper.Map<TimesheetDTO.Timesheet>(timesheet));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating hours for TimesheetId: {TimesheetId}", id);
                return StatusCode(500, "An error occurred while updating the timesheet. Please try again later.");
            }
        }

        private static TimeSpan? ParseTimeSpan(string time)
        {
            return TimeSpan.TryParse(time, out var result) ? result : null;
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
