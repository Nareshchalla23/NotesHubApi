using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoMapper;
using NotesHubApi.DTO;
using NotesHubApi.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace NotesHubApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly IMapper _mapper;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ISignalRService _signalRService;
        private readonly CollabPlatformDbContext _dbContext;

        public ProjectController(
            ILogger<ProjectController> logger,
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

        private async Task<bool> IsValidLoginId(Guid loginId)
        {
            return await _dbContext.GitHubLogins.AnyAsync(g => g.LoginId == loginId) ||
                   await _dbContext.GoogleLogins.AnyAsync(g => g.LoginId == loginId) ||
                   await _dbContext.Jwtlogins.AnyAsync(j => j.LoginId == loginId);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] TimesheetDTO.Project projectDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await IsValidLoginId(projectDto.LoginId))
            {
                return BadRequest("Invalid LoginId. The provided LoginId does not exist in any login table.");
            }

            try
            {
                var project = new ProjectHead
                {
                    ProjectId = Guid.NewGuid(),
                    LoginId = projectDto.LoginId,
                    ProjectName = projectDto.ProjectName,
                    StartDate = projectDto.StartDate.HasValue ? DateOnly.FromDateTime(projectDto.StartDate.Value) : null,
                    EndDate = projectDto.EndDate.HasValue ? DateOnly.FromDateTime(projectDto.EndDate.Value) : null,
                    Status = projectDto.Status
                };

                _dbContext.ProjectHeads.Add(project);
                await _dbContext.SaveChangesAsync();

                _rabbitMQService.PublishMessage("project_created", project);
                await _signalRService.NotifyClientsAsync("ProjectCreated", project);

                _logger.LogInformation("Project created successfully. ProjectId: {ProjectId}", project.ProjectId);

                return CreatedAtAction(nameof(GetProject), new { id = project.ProjectId }, _mapper.Map<TimesheetDTO.Project>(project));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while creating project");
                await _signalRService.NotifyClientsAsync("ProjectCreationFailed", "Database error occurred");
                return StatusCode(500, $"A database error occurred: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while creating project");
                await _signalRService.NotifyClientsAsync("ProjectCreationFailed", "An unexpected error occurred");
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(Guid id)
        {
            try
            {
                var project = await _dbContext.ProjectHeads.FindAsync(id);
                if (project == null)
                {
                    return NotFound($"Project with ID '{id}' not found.");
                }
                return Ok(_mapper.Map<TimesheetDTO.Project>(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving project");
                return StatusCode(500, $"An error occurred while retrieving the project: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProjects()
        {
            try
            {
                var projects = await _dbContext.ProjectHeads.ToListAsync();
                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Project>>(projects));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving all projects");
                return StatusCode(500, $"An error occurred while retrieving all projects: {ex.Message}");
            }
        }

        [HttpPut("{projectName}")]
        public async Task<IActionResult> UpdateProject(string projectName, [FromBody] TimesheetDTO.Project projectDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var project = await _dbContext.ProjectHeads.FirstOrDefaultAsync(p => p.ProjectName == projectName);
                if (project == null)
                {
                    return NotFound($"Project with name '{projectName}' not found.");
                }

                project.LoginId = projectDto.LoginId;
                project.ProjectName = projectDto.ProjectName;
                project.StartDate = projectDto.StartDate.HasValue ? DateOnly.FromDateTime(projectDto.StartDate.Value) : null;
                project.EndDate = projectDto.EndDate.HasValue ? DateOnly.FromDateTime(projectDto.EndDate.Value) : null;
                project.Status = projectDto.Status;

                await _dbContext.SaveChangesAsync();

                _rabbitMQService.PublishMessage("project_updated", project);
                await _signalRService.NotifyClientsAsync("ProjectUpdated", project);

                _logger.LogInformation("Project updated successfully. ProjectId: {ProjectId}", project.ProjectId);

                return Ok(_mapper.Map<TimesheetDTO.Project>(project));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error occurred while updating project");
                await _signalRService.NotifyClientsAsync("ProjectUpdateFailed", "Concurrency error occurred");
                return StatusCode(409, $"A concurrency error occurred: {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while updating project");
                await _signalRService.NotifyClientsAsync("ProjectUpdateFailed", "Database error occurred");
                return StatusCode(500, $"A database error occurred: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while updating project");
                await _signalRService.NotifyClientsAsync("ProjectUpdateFailed", "An unexpected error occurred");
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

        [HttpDelete("{projectName}")]
        public async Task<IActionResult> DeleteProject(string projectName, Guid loginId)
        {
            try
            {
                var project = await _dbContext.ProjectHeads
                    .FirstOrDefaultAsync(p => p.ProjectName == projectName && p.LoginId == loginId);

                if (project == null)
                {
                    return NotFound($"Project with name '{projectName}' and LoginId '{loginId}' not found.");
                }

                _dbContext.ProjectHeads.Remove(project);
                await _dbContext.SaveChangesAsync();

                _rabbitMQService.PublishMessage("project_deleted", project);
                await _signalRService.NotifyClientsAsync("ProjectDeleted", project);

                _logger.LogInformation("Project deleted successfully. ProjectId: {ProjectId}", project.ProjectId);

                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while deleting project");
                await _signalRService.NotifyClientsAsync("ProjectDeletionFailed", "Database error occurred");
                return StatusCode(500, $"A database error occurred: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while deleting project");
                await _signalRService.NotifyClientsAsync("ProjectDeletionFailed", "An unexpected error occurred");
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }


        [HttpGet("search")]
        public async Task<IActionResult> SearchProjects([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest("Search keyword is required.");
            }

            try
            {
                var projects = await _dbContext.ProjectHeads
                    .Where(p => p.ProjectName.Contains(keyword) || p.Status.Contains(keyword))
                    .ToListAsync();

                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Project>>(projects));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching projects");
                return StatusCode(500, $"An error occurred while searching projects: {ex.Message}");
            }
        }

        [HttpGet("by-login/{loginId}")]
        public async Task<IActionResult> GetProjectsByLoginId(Guid loginId)
        {
            try
            {
                var projects = await _dbContext.ProjectHeads
                    .Where(p => p.LoginId == loginId)
                    .ToListAsync();

                return Ok(_mapper.Map<IEnumerable<TimesheetDTO.Project>>(projects));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving projects by login ID");
                return StatusCode(500, $"An error occurred while retrieving projects: {ex.Message}");
            }
        }

        [HttpPatch("{projectName}/status")]
        public async Task<IActionResult> UpdateProjectStatus(string projectName, Guid loginId, [FromBody] string newStatus)
        {
            try
            {
                var project = await _dbContext.ProjectHeads
                    .FirstOrDefaultAsync(p => p.ProjectName == projectName && p.LoginId == loginId);

                if (project == null)
                {
                    return NotFound($"Project with name '{projectName}' and LoginId '{loginId}' not found.");
                }

                project.Status = newStatus;
                await _dbContext.SaveChangesAsync();

                _rabbitMQService.PublishMessage("project_status_updated", project);
                await _signalRService.NotifyClientsAsync("ProjectStatusUpdated", project);

                _logger.LogInformation("Project status updated successfully. ProjectId: {ProjectId}, NewStatus: {NewStatus}", project.ProjectId, newStatus);

                return Ok(_mapper.Map<TimesheetDTO.Project>(project));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while updating project status");
                await _signalRService.NotifyClientsAsync("ProjectStatusUpdateFailed", "Database error occurred");
                return StatusCode(500, $"A database error occurred: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while updating project status");
                await _signalRService.NotifyClientsAsync("ProjectStatusUpdateFailed", "An unexpected error occurred");
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

    }
}

