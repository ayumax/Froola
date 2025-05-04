using System.IO;
using System.Threading.Tasks;

namespace Froola.Interfaces;

/// <summary>
///     Interface for file and directory operations.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    ///     Checks if the specified file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    ///     Deletes the specified file.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    ///     Writes the specified text to a file.
    /// </summary>
    void WriteAllText(string path, string contents);

    /// <summary>
    ///     Reads all text from the specified file.
    /// </summary>
    string ReadAllText(string path);

    /// <summary>
    ///     Reads all text from the specified file asynchronously.
    /// </summary>
    Task<string> ReadAllTextAsync(string path);

    /// <summary>
    ///     Writes the specified text to a file asynchronously.
    /// </summary>
    Task WriteAllTextAsync(string path, string contents);

    /// <summary>
    ///     Creates the specified directory.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    ///     Checks if the specified directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    ///     Deletes the specified directory.
    /// </summary>
    void DeleteDirectory(string path, bool recursive);

    /// <summary>
    ///     Gets the files in the specified directory matching the search pattern.
    /// </summary>
    string[] GetFiles(string path, string searchPattern);

    /// <summary>
    ///     Gets the directories in the specified directory.
    /// </summary>
    string[] GetDirectories(string path);

    /// <summary>
    ///     Copies a file to a new location.
    /// </summary>
    void FileCopy(string sourceFileName, string destFileName, bool overwrite);

    /// <summary>
    ///     Reads all text from a file asynchronously.
    /// </summary>
    Task<string> FileReadAllTextAsync(string path);

    /// <summary>
    ///     Copies a directory and its contents to a new location.
    /// </summary>
    void CopyDirectory(string sourceDir, string destDir);

    /// <summary>
    ///     Opens a file for reading and returns the read-only stream.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    ///     Creates or overwrites a file at the specified path and returns a writeable stream.
    /// </summary>
    Stream Create(string path);
}
