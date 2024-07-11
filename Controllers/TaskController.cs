using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoMapper;
using NotesHubApi.DTO;
using NotesHubApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace NotesHubApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly ILogger<TaskController> _logger;
        private readonly IMapper _mapper;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ISignalRService _signalRService;
        private readonly CollabPlatformDbContext _dbContext;

        public TaskController(
            ILogger<TaskController> logger,
            IMapper mapper,
            IRabbitMQService rabbitMQService,
            ISignalRService signalRService,
            CollabPlatformDbContext dbContext)
        {
            _logger = logger;
            _mapper = mapper;
            _rabbitMQService = rabbitMQService;
            _signalRService = signalRService;
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTaskAsync([FromBody] TimesheetDTO.Task taskDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var project = await _dbContext.ProjectHeads.FindAsync(taskDto.ProjectId);
                if (project == null)
                {
                    return BadRequest($"Project with ID {taskDto.ProjectId} does not exist.");
                }

                if (!await IsValidLoginIdAsync(taskDto.LoginId))
                {
                    return BadRequest("Invalid LoginId. The provided LoginId does not exist in any login table.");
                }

                var task = _mapper.Map<Models.Task>(taskDto);
                task.TaskId = Guid.NewGuid();
                task.LastModifiedOn = DateTime.UtcNow;

                _dbContext.Tasks.Add(task);
                await _dbContext.SaveChangesAsync();

                var taskDtoResult = _mapper.Map<TimesheetDTO.Task>(task);
                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 64
                };
                var taskJson = JsonSerializer.Serialize(taskDtoResult, jsonOptions);

                _rabbitMQService.PublishMessage("task_created", taskJson);
                await _signalRService.NotifyClientsAsync("TaskCreated", taskJson);

                _logger.LogInformation("Task created successfully. TaskId: {TaskId}", task.TaskId);

                return CreatedAtAction(nameof(GetTaskAsync), new { id = task.TaskId }, taskDtoResult);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while creating task");
                await _signalRService.NotifyClientsAsync("TaskCreationFailed", "Database error occurred");
                return StatusCode(500, $"A database error occurred: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while creating task");
                await _signalRService.NotifyClientsAsync("TaskCreationFailed", "An unexpected error occurred");
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskAsync(Guid id)
        {
            try
            {
                var task = await _dbContext.Tasks.FindAsync(id);
                if (task == null)
                {
                    return NotFound($"Task with ID '{id}' not found.");
                }
                return Ok(_mapper.Map<TimesheetDTO.Task>(task));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving task");
                return StatusCode(500, $"An error occurred while retrieving the task: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasksAsync()
        {
            try
            {
                var tasks = await _dbContext.Tasks.ToListAsync();
                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Task>>(tasks));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving all tasks");
                return StatusCode(500, $"An error occurred while retrieving all tasks: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTaskAsync(Guid id, [FromBody] TimesheetDTO.Task taskDto)
        {
            if (id != taskDto.TaskId)
            {
                return BadRequest("Task ID mismatch");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var task = await _dbContext.Tasks.FindAsync(id);
                if (task == null)
                {
                    return NotFound($"Task with ID '{id}' not found.");
                }

                _mapper.Map(taskDto, task);
                task.LastModifiedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 64
                };
                var taskJson = JsonSerializer.Serialize(_mapper.Map<TimesheetDTO.Task>(task), jsonOptions);

                _rabbitMQService.PublishMessage("task_updated", taskJson);
                await _signalRService.NotifyClientsAsync("TaskUpdated", taskJson);

                _logger.LogInformation("Task updated successfully. TaskId: {TaskId}", task.TaskId);

                return Ok(_mapper.Map<TimesheetDTO.Task>(task));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error occurred while updating task");
                await _signalRService.NotifyClientsAsync("TaskUpdateFailed", "Concurrency error occurred");
                return StatusCode(409, $"A concurrency error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating task");
                await _signalRService.NotifyClientsAsync("TaskUpdateFailed", "An unexpected error occurred");
                return StatusCode(500, $"An error occurred while updating the task: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTaskAsync(Guid id)
        {
            try
            {
                var task = await _dbContext.Tasks.FindAsync(id);
                if (task == null)
                {
                    return NotFound($"Task with ID '{id}' not found.");
                }

                _dbContext.Tasks.Remove(task);
                await _dbContext.SaveChangesAsync();

                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 64
                };
                var taskJson = JsonSerializer.Serialize(_mapper.Map<TimesheetDTO.Task>(task), jsonOptions);

                _rabbitMQService.PublishMessage("task_deleted", taskJson);
                await _signalRService.NotifyClientsAsync("TaskDeleted", taskJson);

                _logger.LogInformation("Task deleted successfully. TaskId: {TaskId}", task.TaskId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting task");
                await _signalRService.NotifyClientsAsync("TaskDeletionFailed", "An unexpected error occurred");
                return StatusCode(500, $"An error occurred while deleting the task: {ex.Message}");
            }
        }

        [HttpGet("by-project/{projectId}")]
        public async Task<IActionResult> GetTasksByProjectAsync(Guid projectId)
        {
            try
            {
                var tasks = await _dbContext.Tasks
                    .Where(t => t.ProjectId == projectId)
                    .ToListAsync();

                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Task>>(tasks));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving tasks by project");
                return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
            }
        }

        [HttpGet("by-login/{loginId}")]
        public async Task<IActionResult> GetTasksByLoginAsync(Guid loginId)
        {
            try
            {
                if (!await IsValidLoginIdAsync(loginId))
                {
                    return BadRequest("Invalid LoginId. The provided LoginId does not exist in any login table.");
                }

                var tasks = await _dbContext.Tasks
                    .Where(t => t.LoginId == loginId)
                    .ToListAsync();

                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Task>>(tasks));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving tasks by login");
                return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
            }
        }

        [HttpPatch("{id}/description")]
        public async Task<IActionResult> UpdateTaskDescriptionAsync(Guid id, [FromBody] string newDescription)
        {
            try
            {
                var task = await _dbContext.Tasks.FindAsync(id);
                if (task == null)
                {
                    return NotFound($"Task with ID '{id}' not found.");
                }

                task.TaskDescription = newDescription;
                task.LastModifiedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 64
                };
                var taskJson = JsonSerializer.Serialize(_mapper.Map<TimesheetDTO.Task>(task), jsonOptions);

                _rabbitMQService.PublishMessage("task_description_updated", taskJson);
                await _signalRService.NotifyClientsAsync("TaskDescriptionUpdated", taskJson);

                _logger.LogInformation("Task description updated successfully. TaskId: {TaskId}", task.TaskId);

                return Ok(_mapper.Map<TimesheetDTO.Task>(task));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating task description");
                await _signalRService.NotifyClientsAsync("TaskDescriptionUpdateFailed", "An unexpected error occurred");
                return StatusCode(500, $"An error occurred while updating the task description: {ex.Message}");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchTasksAsync([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest("Search keyword is required.");
            }

            try
            {
                var tasks = await _dbContext.Tasks
                    .Where(t => t.TaskName.Contains(keyword) || t.TaskDescription.Contains(keyword))
                    .ToListAsync();

                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Task>>(tasks));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching tasks");
                return StatusCode(500, $"An error occurred while searching tasks: {ex.Message}");
            }
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentTasksAsync([FromQuery] int count = 10, [FromQuery] Guid? loginId = null)
        {
            try
            {
                IQueryable<Models.Task> query = _dbContext.Tasks;

                if (loginId.HasValue)
                {
                    if (!await IsValidLoginIdAsync(loginId.Value))
                    {
                        return BadRequest("Invalid LoginId. The provided LoginId does not exist in any login table.");
                    }
                    query = query.Where(t => t.LoginId == loginId.Value);
                }

                var tasks = await query
                    .OrderByDescending(t => t.LastModifiedOn)
                    .Take(count)
                    .ToListAsync();

                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Task>>(tasks));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving recent tasks. LoginId: {LoginId}", loginId);
                return StatusCode(500, $"An error occurred while retrieving recent tasks: {ex.Message}");
            }
        }

        private async Task<bool> IsValidLoginIdAsync(Guid loginId)
        {
            return await _dbContext.GitHubLogins.AnyAsync(g => g.LoginId == loginId) ||
                   await _dbContext.GoogleLogins.AnyAsync(g => g.LoginId == loginId) ||
                   await _dbContext.Jwtlogins.AnyAsync(j => j.LoginId == loginId);
        }
    }
}

