using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using NotesHubApi.Models;
using Microsoft.Extensions.Logging;
using BCrypt.Net;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.Mail;
using System.Text.RegularExpressions;
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
    public class LoginController : ControllerBase
    {
        private readonly CollabPlatformDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<LoginController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IRabbitMQService _rabbitMQService;

        public LoginController(CollabPlatformDbContext context, IMapper mapper, ILogger<LoginController> logger,
                               IConfiguration configuration, IRabbitMQService rabbitMQService)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LoginDto.Register registerDto)
        {
            try
            {
                _logger.LogInformation("Starting registration process for email: {Email}", registerDto.Email);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state during registration for email: {Email}", registerDto.Email);
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(registerDto.Email))
                {
                    _logger.LogWarning("Email is null or empty during registration");
                    return BadRequest("Email is required.");
                }

                if (string.IsNullOrWhiteSpace(registerDto.Password))
                {
                    _logger.LogWarning("Password is null or empty during registration for email: {Email}", registerDto.Email);
                    return BadRequest("Password is required.");
                }

                if (!IsValidEmail(registerDto.Email))
                {
                    _logger.LogWarning("Invalid email format during registration: {Email}", registerDto.Email);
                    return BadRequest("Invalid email format.");
                }

                var passwordStrength = IsStrongPassword(registerDto.Password);
                if (!string.IsNullOrEmpty(passwordStrength))
                {
                    _logger.LogWarning("Weak password during registration for email: {Email}. Reason: {Reason}", registerDto.Email, passwordStrength);
                    return BadRequest(passwordStrength);
                }

                if (await _context.Jwtlogins.AnyAsync(u => u.Email == registerDto.Email))
                {
                    _logger.LogWarning("Registration attempt with existing email: {Email}", registerDto.Email);
                    return Conflict("Email already exists");
                }

                var user = _mapper.Map<Jwtlogin>(registerDto);
                user.PasswordHash = HashPassword(registerDto.Password);

                _context.Jwtlogins.Add(user);
                await _context.SaveChangesAsync();

                // Publish registration event to RabbitMQ
                _rabbitMQService.PublishMessage("user_events", new { Event = "UserRegistered", User = user.Email, Timestamp = DateTime.UtcNow });

                _logger.LogInformation("Registration successful for email: {Email}", user.Email);
                return CreatedAtAction(nameof(Register), new { id = user.LoginId }, new { Message = "User registered successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during registration for email: {Email}", registerDto.Email);
                throw; // Re-throw to be caught by the global error handler
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto.Login loginDto)
        {
            try
            {
                _logger.LogInformation("Starting login process for email: {Email}", loginDto.Email);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state during login for email: {Email}", loginDto.Email);
                    return BadRequest(ModelState);
                }

                var user = await _context.Jwtlogins.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", loginDto.Email);
                    return Unauthorized("Invalid email or password.");
                }

                if (!VerifyPassword(loginDto.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login attempt with incorrect password for email: {Email}", loginDto.Email);
                    return Unauthorized("Invalid email or password.");
                }

                var token = GenerateJwtToken(user);

                // Publish login event to RabbitMQ
                _rabbitMQService.PublishMessage("user_events", new { Event = "UserLoggedIn", User = user.Email, Timestamp = DateTime.UtcNow });

                _logger.LogInformation("Login successful for email: {Email}", user.Email);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login for email: {Email}", loginDto.Email);
                throw; // Re-throw to be caught by the global error handler
            }
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return "Password cannot be empty.";

            var errors = new List<string>();

            if (password.Length < 8)
                errors.Add("Password must be at least 8 characters long.");

            if (!Regex.IsMatch(password, @"[A-Z]"))
                errors.Add("Password must contain at least one uppercase letter.");

            if (!Regex.IsMatch(password, @"[a-z]"))
                errors.Add("Password must contain at least one lowercase letter.");

            if (!Regex.IsMatch(password, @"[0-9]"))
                errors.Add("Password must contain at least one digit.");

            if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
                errors.Add("Password must contain at least one special character.");

            return errors.Count > 0 ? string.Join(" ", errors) : string.Empty;
        }

        private string GenerateJwtToken(Jwtlogin user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, user.LoginId.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Name, user.Username ?? string.Empty)
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