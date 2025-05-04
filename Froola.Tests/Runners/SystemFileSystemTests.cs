using System.Text;
using Froola.Runners;

namespace Froola.Tests.Runners;

// Prevent parallel execution of tests in this class
[Collection("SystemFileSystemTests_Collection")]
public class SystemFileSystemTests : IDisposable
{
    private readonly string _testRootDir;
    private readonly SystemFileSystem _fs;

    public SystemFileSystemTests()
    {
        // Create a unique temporary directory for each test class instance
        _testRootDir = Path.Combine(Path.GetTempPath(), "SystemFileSystemTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testRootDir);
        _fs = new SystemFileSystem();
    }

    [Fact]
    public void CreateAndReadFile_WorksCorrectly()
    {
        var filePath = Path.Combine(_testRootDir, "testfile.txt");
        const string content = "Hello, SystemFileSystem!";

        // Write to file via SystemFileSystem
        _fs.WriteAllText(filePath, content);

        // Check file exists via SystemFileSystem
        Assert.True(_fs.FileExists(filePath));

        // Read file content via SystemFileSystem
        var readContent = _fs.ReadAllText(filePath);
        Assert.Equal(content, readContent);
    }

    [Fact]
    public void DeleteFile_WorksCorrectly()
    {
        var filePath = Path.Combine(_testRootDir, "deletefile.txt");
        _fs.WriteAllText(filePath, "to delete");
        Assert.True(_fs.FileExists(filePath));

        // Delete file via SystemFileSystem
        _fs.DeleteFile(filePath);
        Assert.False(_fs.FileExists(filePath));
    }

    [Fact]
    public void CreateAndDeleteDirectory_WorksCorrectly()
    {
        var dirPath = Path.Combine(_testRootDir, "subdir");
        _fs.CreateDirectory(dirPath);
        Assert.True(_fs.DirectoryExists(dirPath));

        _fs.DeleteDirectory(dirPath, false);
        Assert.False(_fs.DirectoryExists(dirPath));
    }

    [Fact]
    public void FileAndDirectoryExistence_ChecksWork()
    {
        var filePath = Path.Combine(_testRootDir, "existfile.txt");
        var dirPath = Path.Combine(_testRootDir, "existdir");
        _fs.WriteAllText(filePath, "exists");
        _fs.CreateDirectory(dirPath);

        Assert.True(_fs.FileExists(filePath));
        Assert.True(_fs.DirectoryExists(dirPath));
        _fs.DeleteFile(filePath);
        _fs.DeleteDirectory(dirPath, false);
        Assert.False(_fs.FileExists(filePath));
        Assert.False(_fs.DirectoryExists(dirPath));
    }

    [Fact]
    public async Task WriteAndReadAllTextAsync_WorksCorrectly()
    {
        var filePath = Path.Combine(_testRootDir, "asyncfile.txt");
        var content = "Async Hello!";
        await _fs.WriteAllTextAsync(filePath, content);
        Assert.True(_fs.FileExists(filePath));
        var read = await _fs.ReadAllTextAsync(filePath);
        Assert.Equal(content, read);
    }

    [Fact]
    public async Task FileReadAllTextAsync_WorksCorrectly()
    {
        var filePath = Path.Combine(_testRootDir, "fileasync.txt");
        var content = "FileReadAllTextAsync!";
        _fs.WriteAllText(filePath, content);
        var read = await _fs.FileReadAllTextAsync(filePath);
        Assert.Equal(content, read);
    }

    [Fact]
    public void GetFilesAndDirectories_WorksCorrectly()
    {
        var subDir = Path.Combine(_testRootDir, "dir1");
        _fs.CreateDirectory(subDir);
        var file1 = Path.Combine(_testRootDir, "file1.txt");
        var file2 = Path.Combine(subDir, "file2.txt");
        _fs.WriteAllText(file1, "a");
        _fs.WriteAllText(file2, "b");

        var filesRoot = _fs.GetFiles(_testRootDir, "*.txt");
        Assert.Contains(file1, filesRoot);
        var dirs = _fs.GetDirectories(_testRootDir);
        Assert.Contains(subDir, dirs);
    }

    [Fact]
    public void FileCopy_WorksCorrectly()
    {
        var src = Path.Combine(_testRootDir, "src.txt");
        var dst = Path.Combine(_testRootDir, "dst.txt");
        const string content = "copy!";
        _fs.WriteAllText(src, content);
        _fs.FileCopy(src, dst, false);
        Assert.True(_fs.FileExists(dst));
        Assert.Equal(content, _fs.ReadAllText(dst));
    }

    [Fact]
    public void CopyDirectory_WorksCorrectly()
    {
        var srcDir = Path.Combine(_testRootDir, "srcdir");
        var dstDir = Path.Combine(_testRootDir, "dstdir");
        _fs.CreateDirectory(srcDir);
        var fileInSrc = Path.Combine(srcDir, "file.txt");
        _fs.WriteAllText(fileInSrc, "dircopy");
        _fs.CopyDirectory(srcDir, dstDir);
        var fileInDst = Path.Combine(dstDir, "file.txt");
        Assert.True(_fs.FileExists(fileInDst));
        Assert.Equal("dircopy", _fs.ReadAllText(fileInDst));
    }

    [Fact]
    public void OpenReadAndCreate_WorksCorrectly()
    {
        var filePath = Path.Combine(_testRootDir, "streamfile.txt");
        var content = "stream!";
        // Write file using Create (returns Stream)
        using (var stream = _fs.Create(filePath))
        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, false))
        {
            writer.Write(content);
        }

        // Read file using OpenRead (returns Stream)
        using (var stream = _fs.OpenRead(filePath))
        using (var reader = new StreamReader(stream, Encoding.UTF8, false))
        {
            var read = reader.ReadToEnd();
            Assert.Equal(content, read);
        }
    }

    [Fact]
    public void RemoveReadOnlyAttribute_RemovesReadOnlyFromFilesAndDirectories()
    {
        // Create directory and file
        var dirPath = Path.Combine(_testRootDir, "readonlydir");
        var filePath = Path.Combine(dirPath, "readonlyfile.txt");
        Directory.CreateDirectory(dirPath);
        File.WriteAllText(filePath, "readonly");

        // Set ReadOnly attribute
        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
        File.SetAttributes(dirPath, File.GetAttributes(dirPath) | FileAttributes.ReadOnly);

        // Ensure ReadOnly is set
        Assert.True((File.GetAttributes(filePath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
        Assert.True((File.GetAttributes(dirPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);

        // Remove ReadOnly attribute using SystemFileSystem
        _fs.RemoveReadOnlyAttribute(dirPath);

        // Check ReadOnly is removed
        Assert.False((File.GetAttributes(filePath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
        Assert.False((File.GetAttributes(dirPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
    }

    public void Dispose()
    {
        // Cleanup: Remove all files and directories created during tests
        try
        {
            if (!Directory.Exists(_testRootDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(_testRootDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    /* ignore */
                }
            }

            foreach (var dir in Directory.GetDirectories(_testRootDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    /* ignore */
                }
            }

            Directory.Delete(_testRootDir, true);
        }
        catch
        {
            /* ignore */
        }
    }
}

// Collection definition to prevent parallel execution
[CollectionDefinition("SystemFileSystemTests_Collection", DisableParallelization = true)]
public class SystemFileSystemTests_Collection : ICollectionFixture<SystemFileSystemTests>
{
}