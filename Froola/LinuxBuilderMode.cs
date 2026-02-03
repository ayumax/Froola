namespace Froola;

/// <summary>
/// Linux builder execution mode.
/// </summary>
public enum LinuxBuilderMode
{
    /// <summary>
    /// Build using Docker on the host machine.
    /// </summary>
    Docker,

    /// <summary>
    /// Build on a remote Linux machine via SSH.
    /// </summary>
    Remote
}
