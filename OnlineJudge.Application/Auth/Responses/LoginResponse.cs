using OnlineJudge.Application.Auth.Dtos;

namespace OnlineJudge.Application.Auth.Responses;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public AuthUserDto User { get; set; } = new();
}
