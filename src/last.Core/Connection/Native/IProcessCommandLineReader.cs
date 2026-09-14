namespace last.Core.Connection.Native;

public interface IProcessCommandLineReader
{
    string? GetCommandLine(int pid);
    string? GetProcessExecutablePath(int pid);
}