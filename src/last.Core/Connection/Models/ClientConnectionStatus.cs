namespace last.Core.Connection.Models;

public enum ClientConnectionStatus
{
    Disconnected,

    Connecting,

    Connected,

    MultipleClientsDetected,

    AccessDenied
}