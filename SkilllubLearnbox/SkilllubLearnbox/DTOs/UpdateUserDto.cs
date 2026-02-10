namespace SkilllubLearnbox.DTOs;

public class UpdateUserDto
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}

public class UpdateUserPasswordDto
{
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}