namespace SkilllubLearnbox.DTOs;
public class UserRegisterWithRoleDto
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "student";
}

public class UserRegisterDto
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";
}

public class UserLoginDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ForgotPasswordDto
{
    public string Email { get; set; } = "";
}

public class ResetPasswordDto
{
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}