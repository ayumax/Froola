using System.IO;
using System.Threading.Tasks;
using Froola.Interfaces;

namespace Froola.Runners;

/// <summary>
///     Implementation of IFileSystem for performing file and directory operations using System.IO.
/// </summary>
public class SystemFileSystem : IFileSystem
{
    /// <summary>
    ///     Checks if the specified file exists.
    /// </summary>
    public bool FileExists(string path) => File.Exists(path);

    /// <summary>
    ///     Deletes the specified file.
    /// </summary>
    public void DeleteFile(string path) => File.Delete(path);

    /// <summary>
    ///     Writes the specified text to a file.
    /// </summary>
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    /// <summary>
    ///     Reads all text from the specified file.
    /// </summary>
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <summary>
    ///     Reads all text from the specified file asynchronously.
    /// </summary>
    public async Task<string> ReadAllTextAsync(string path) => await File.ReadAllTextAsync(path);

    /// <summary>
    ///     Writes the specified text to a file asynchronously.
    /// </summary>
    public async Task WriteAllTextAsync(string path, string contents) => await File.WriteAllTextAsync(path, contents);

    /// <summary>
    ///     Creates the specified directory.
    /// </summary>
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <summary>
    ///     Checks if the specified directory exists.
    /// </summary>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <summary>
    ///     Deletes the specified directory.
    /// </summary>
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    /// <summary>
    ///     Gets the files in the specified directory matching the search pattern.
    /// </summary>
    public string[] GetFiles(string path, string searchPattern) => Directory.GetFiles(path, searchPattern);

    /// <summary>
    ///     Gets the directories in the specified directory.
    /// </summary>
    public string[] GetDirectories(string path) => Directory.GetDirectories(path);

    /// <summary>
    ///     Copies a file to a new location.
    /// </summary>
    public void FileCopy(string sourceFileName, string destFileName, bool overwrite) =>
        File.Copy(sourceFileName, destFileName, overwrite);

    /// <summary>
    ///     Reads all text from a file asynchronously.
    /// </summary>
    public async Task<string> FileReadAllTextAsync(string path) => await File.ReadAllTextAsync(path);

    /// <summary>
    ///     Copies a directory and its contents to a new location.
    /// </summary>
    public void CopyDirectory(string sourceDirName, string destDirName)
    {
        // Get the subdirectories for the specified directory.
        var dir = new DirectoryInfo(sourceDirName);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        var dirs = dir.GetDirectories();

        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Get the files in the directory and copy them to the new location.
        var files = dir.GetFiles();
        foreach (var file in files)
        {
            var tempPath = Path.Combine(destDirName, file.Name);
            file.CopyTo(tempPath, true);
        }

        foreach (var subdir in dirs)
        {
            var tempPath = Path.Combine(destDirName, subdir.Name);
            CopyDirectory(subdir.FullName, tempPath);
        }
    }

    /// <summary>
    ///     Opens a file for reading and returns the read-only stream.
    /// </summary>
    public Stream OpenRead(string path) => File.OpenRead(path);

    /// <summary>
    ///     Creates or overwrites a file at the specified path and returns a writeable stream.
    /// </summary>
    public Stream Create(string path) => File.Create(path);
}