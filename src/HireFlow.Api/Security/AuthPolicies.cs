namespace HireFlow.Api.Security;

public static class AuthPolicies
{
    public const string Recruiter = "Recruiter";
    public const string Bot = "Bot";
    public const string RecruiterOrBot = "RecruiterOrBot";
    public const string PublicApi = "PublicApi";
    public const string LoginRateLimit = "login";
    public const string PublicApiRateLimit = "public-api";
}
