using System;
using System.ComponentModel.DataAnnotations;

namespace NotesHubApi.Models
{
    public class LoginDto
    {
        public class Register
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long")]
            public string Password { get; set; } = null!;

            [StringLength(50, MinimumLength = 2, ErrorMessage = "Username must be between 2 and 50 characters")]
            public string? Username { get; set; }
        }

        public class Login
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "Password is required")]
            public string Password { get; set; } = null!;
        }

        public class GoogleLogin
        {
            [Required(ErrorMessage = "ClientId is required")]
            [StringLength(255, ErrorMessage = "ClientId must not exceed 255 characters")]
            public string ClientId { get; set; } = null!;

            [Required(ErrorMessage = "Credential is required")]
            [StringLength(2048, ErrorMessage = "Credential must not exceed 2048 characters")]
            public string Credential { get; set; } = null!;

            [StringLength(50, ErrorMessage = "SelectBy must not exceed 50 characters")]
            public string? SelectBy { get; set; }

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "Name is required")]
            [StringLength(255, ErrorMessage = "Name must not exceed 255 characters")]
            public string Name { get; set; } = null!;

            [Url(ErrorMessage = "Invalid URL format for Picture")]
            [StringLength(2048, ErrorMessage = "Picture URL must not exceed 2048 characters")]
            public string? Picture { get; set; }

            [Required(ErrorMessage = "Sub is required")]
            [StringLength(255, ErrorMessage = "Sub must not exceed 255 characters")]
            public string Sub { get; set; } = null!;
        }
        public class GitHubLogin
        {
            [Required(ErrorMessage = "ClientId is required")]
            [StringLength(255, ErrorMessage = "ClientId must not exceed 255 characters")]
            public string ClientId { get; set; } = null!;

            [Required(ErrorMessage = "Code is required")]
            [StringLength(255, ErrorMessage = "Code must not exceed 255 characters")]
            public string Code { get; set; } = null!;

            [Required(ErrorMessage = "SelectBy is required")]
            [StringLength(50, ErrorMessage = "SelectBy must not exceed 50 characters")]
            public string SelectBy { get; set; } = null!;

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "Name is required")]
            [StringLength(255, ErrorMessage = "Name must not exceed 255 characters")]
            public string Name { get; set; } = null!;

            [Url(ErrorMessage = "Invalid URL format for Picture")]
            [StringLength(2048, ErrorMessage = "Picture URL must not exceed 2048 characters")]
            public string? Picture { get; set; }

            [Required(ErrorMessage = "Sub is required")]
            [StringLength(255, ErrorMessage = "Sub must not exceed 255 characters")]
            public string Sub { get; set; } = null!;
        }
    }
}
