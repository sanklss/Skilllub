namespace SkilllubLearnbox.DTOs;
public class TokenResponseDto
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime AccessTokenExpires { get; set; }
    public DateTime RefreshTokenExpires { get; set; }
}

public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
}

public class RevokeTokenRequestDto
{
    public string RefreshToken { get; set; } = "";
}

public class LogoutRequestDto
{
    public string RefreshToken { get; set; } = "";
}