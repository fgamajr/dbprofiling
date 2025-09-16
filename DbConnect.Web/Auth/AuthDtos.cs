namespace DbConnect.Web.Auth;

public sealed record RegisterDto(string Username, string Password);
public sealed record LoginDto(string Username, string Password);
public sealed record MeDto(int Id, string Username);
