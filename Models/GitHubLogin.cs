using System;
using System.Collections.Generic;

namespace NotesHubApi.Models;

public partial class GitHubLogin
{
    public Guid LoginId { get; set; }

    public string GitHubId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? AccessToken { get; set; }

    public string? Code { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime LastLoginDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public string? AvatarUrl { get; set; }

    public string ClientId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Username { get; set; } = null!;
}
