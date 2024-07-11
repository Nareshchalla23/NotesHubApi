using System;
using System.Collections.Generic;

namespace NotesHubApi.Models;

public partial class Jwtlogin
{
    public Guid LoginId { get; set; }

    public string? Username { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public bool? IsActive { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
