using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using NotesHubApi.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace NotesHubApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("GlobalLimiter")]
    public class GitHubLoginController : ControllerBase
    {
        private readonly CollabPlatformDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<GitHubLoginController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IRabbitMQService _rabbitMQService;

        public GitHubLoginController(
            CollabPlatformDbContext context,
            IMapper mapper,
            ILogger<GitHubLoginController> logger,
            IConfiguration configuration,
            IRabbitMQService rabbitMQService)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto.GitHubLogin loginDto)
        {
            try
            {
                _logger.LogInformation("Starting GitHub login process for ClientId: {ClientId}", loginDto.ClientId);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state during GitHub login for ClientId: {ClientId}", loginDto.ClientId);
                    return BadRequest(ModelState);
                }

                var user = await _context.GitHubLogins.FirstOrDefaultAsync(u => u.GitHubId == loginDto.Sub);

                if (user == null)
                {
                    user = _mapper.Map<GitHubLogin>(loginDto);
                    user.LoginId = Guid.NewGuid();
                    user.CreatedDate = DateTime.UtcNow;
                    user.LastLoginDate = DateTime.UtcNow;
                    user.IsActive = true;

                    _context.GitHubLogins.Add(user);
                    await _context.SaveChangesAsync();

                    // Publish registration event to RabbitMQ
                    try
                    {
                        var message = new { Event = "NewGitHubUserRegistered", User = user.Email, Timestamp = DateTime.UtcNow };
                        _logger.LogInformation("Attempting to publish message to RabbitMQ: {@Message}", message);
                        _rabbitMQService.PublishMessage("user_events", message);
                        _logger.LogInformation("Successfully published NewGitHubUserRegistered event to RabbitMQ for user: {Email}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish NewGitHubUserRegistered event to RabbitMQ for user: {Email}", user.Email);
                    }
                }
                else
                {
                    // Update existing user information
                    user.LastLoginDate = DateTime.UtcNow;
                    user.Name = loginDto.Name;
                    user.Email = loginDto.Email;
                    user.AvatarUrl = loginDto.Picture;
                    user.AccessToken = loginDto.Code; // Note: In a real scenario, you'd want to exchange this for a real access token

                    _context.GitHubLogins.Update(user);
                    await _context.SaveChangesAsync();
                }

                var token = GenerateJwtToken(user);

                // Publish login event to RabbitMQ
                try
                {
                    var message = new { Event = "UserLoggedInWithGitHub", User = user.Email, Timestamp = DateTime.UtcNow };
                    _logger.LogInformation("Attempting to publish message to RabbitMQ: {@Message}", message);
                    _rabbitMQService.PublishMessage("user_events", message);
                    _logger.LogInformation("Successfully published UserLoggedInWithGitHub event to RabbitMQ for user: {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish UserLoggedInWithGitHub event to RabbitMQ for user: {Email}", user.Email);
                }

                _logger.LogInformation("GitHub login successful for email: {Email}", user.Email);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during GitHub login for ClientId: {ClientId}", loginDto.ClientId);
                return StatusCode(500, new { message = "An unexpected error occurred" });
            }
        }

        private string GenerateJwtToken(GitHubLogin user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.LoginId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.Name ?? string.Empty)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JWT:TokenValidityInMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
