using System;
using System.Collections.Generic;

namespace NotesHubApi.Models;

public partial class GoogleLogin
{
    public Guid LoginId { get; set; }

    public string Sub { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public bool IsActive { get; set; }

    public string ClientId { get; set; } = null!;

    public string Credential { get; set; } = null!;

    public string? Name { get; set; }

    public string? Picture { get; set; }

    public string? SelectBy { get; set; }
}
