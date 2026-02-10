using System;

namespace SkilllubLearnbox.DTOs;
public class RoleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class UserRoleDto
{
    public string UserId { get; set; } = "";
    public string RoleId { get; set; } = "";
}

public class CleanupResultDto
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public string Message { get; set; } = "";
}