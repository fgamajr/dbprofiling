namespace DbConnect.Web.Auth;

public sealed record JwtOptions(string Issuer, string Audience, string Secret, int ExpMinutes);
