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
using Google.Apis.Auth;

namespace NotesHubApi.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("GlobalLimiter")]
    public class GoogleLoginController : ControllerBase
    {
        private readonly CollabPlatformDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<GoogleLoginController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly bool _useMockGoogleAuth;

        public GoogleLoginController(
            CollabPlatformDbContext context,
            IMapper mapper,
            ILogger<GoogleLoginController> logger,
            IConfiguration configuration,
            IRabbitMQService rabbitMQService)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
            _rabbitMQService = rabbitMQService;
            _useMockGoogleAuth = configuration.GetValue<bool>("UseMockGoogleAuth");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto.GoogleLogin loginDto)
        {
            try
            {
                _logger.LogInformation("Starting Google login process for ClientId: {ClientId}", loginDto.ClientId);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state during Google login for ClientId: {ClientId}", loginDto.ClientId);
                    return BadRequest(ModelState);
                }

                GoogleLogin user;

                if (_useMockGoogleAuth)
                {
                    user = await GetOrCreateMockUserAsync(loginDto);
                }
                else
                {
                    var payload = await VerifyGoogleTokenAsync(loginDto.Credential);
                    if (payload == null)
                    {
                        _logger.LogWarning("Invalid Google token for ClientId: {ClientId}", loginDto.ClientId);
                        return BadRequest(new { message = "Invalid Google token" });
                    }
                    user = await GetOrCreateUserAsync(payload, loginDto);
                }

                var token = GenerateJwtToken(user);

                // Publish login event to RabbitMQ
                try
                {
                    var message = new { Event = "UserLoggedInWithGoogle", User = user.Email, Timestamp = DateTime.UtcNow };
                    _logger.LogInformation("Attempting to publish message to RabbitMQ: {@Message}", message);
                    _rabbitMQService.PublishMessage("user_events", message);
                    _logger.LogInformation("Successfully published UserLoggedInWithGoogle event to RabbitMQ for user: {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish UserLoggedInWithGoogle event to RabbitMQ for user: {Email}", user.Email);
                }

                _logger.LogInformation("Google login successful for email: {Email}", user.Email);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during Google login for ClientId: {ClientId}", loginDto.ClientId);
                return StatusCode(500, new { message = "An unexpected error occurred" });
            }
        }

        private async Task<GoogleLogin> GetOrCreateMockUserAsync(LoginDto.GoogleLogin loginDto)
        {
            var user = await _context.GoogleLogins.FirstOrDefaultAsync(u => u.Sub == loginDto.Sub);

            if (user == null)
            {
                user = _mapper.Map<GoogleLogin>(loginDto);
                user.LoginId = Guid.NewGuid();
                user.CreatedDate = DateTime.UtcNow;
                user.LastLoginDate = DateTime.UtcNow;
                user.IsActive = true;

                _context.GoogleLogins.Add(user);
                await _context.SaveChangesAsync();

                // Publish registration event to RabbitMQ
                try
                {
                    var message = new { Event = "NewGoogleUserRegistered", User = user.Email, Timestamp = DateTime.UtcNow };
                    _logger.LogInformation("Attempting to publish message to RabbitMQ: {@Message}", message);
                    _rabbitMQService.PublishMessage("user_events", message);
                    _logger.LogInformation("Successfully published NewGoogleUserRegistered event to RabbitMQ for user: {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish NewGoogleUserRegistered event to RabbitMQ for user: {Email}", user.Email);
                }
            }
            else
            {
                _mapper.Map(loginDto, user);
                user.LastLoginDate = DateTime.UtcNow;
                _context.GoogleLogins.Update(user);
                await _context.SaveChangesAsync();
            }

            return user;
        }

        private async Task<GoogleLogin> GetOrCreateUserAsync(GoogleJsonWebSignature.Payload payload, LoginDto.GoogleLogin loginDto)
        {
            var user = await _context.GoogleLogins.FirstOrDefaultAsync(u => u.Sub == payload.Subject);

            if (user == null)
            {
                user = new GoogleLogin
                {
                    LoginId = Guid.NewGuid(),
                    ClientId = loginDto.ClientId,
                    Credential = loginDto.Credential,
                    SelectBy = loginDto.SelectBy,
                    Email = payload.Email,
                    Name = payload.Name,
                    Picture = payload.Picture,
                    Sub = payload.Subject,
                    CreatedDate = DateTime.UtcNow,
                    LastLoginDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.GoogleLogins.Add(user);
                await _context.SaveChangesAsync();

                // Publish registration event to RabbitMQ
                try
                {
                    var message = new { Event = "NewGoogleUserRegistered", User = user.Email, Timestamp = DateTime.UtcNow };
                    _logger.LogInformation("Attempting to publish message to RabbitMQ: {@Message}", message);
                    _rabbitMQService.PublishMessage("user_events", message);
                    _logger.LogInformation("Successfully published NewGoogleUserRegistered event to RabbitMQ for user: {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish NewGoogleUserRegistered event to RabbitMQ for user: {Email}", user.Email);
                }
            }
            else
            {
                user.LastLoginDate = DateTime.UtcNow;
                user.Credential = loginDto.Credential;
                user.Name = payload.Name;
                user.Picture = payload.Picture;
                user.Email = payload.Email;
                _context.GoogleLogins.Update(user);
                await _context.SaveChangesAsync();
            }

            return user;
        }

        private async Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(string token)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new[] { _configuration["Google:ClientId"] }
                };
                return await GoogleJsonWebSignature.ValidateAsync(token, settings);
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning(ex, "Invalid Google token");
                return null;
            }
        }

        private string GenerateJwtToken(GoogleLogin user)
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
