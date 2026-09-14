using System.Text;

namespace last.Core.Connection.Models;

public sealed record LcuCredentials(
    int Port,
    string Token,
    int Pid,
    string PlatformId,
    string Region)
{
    public string BaseUrl => $"https://127.0.0.1:{Port}/";

    public string BasicAuthHeaderValue =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes($"riot:{Token}"));
}