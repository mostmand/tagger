using Tagger.Application.Abstractions;

namespace Tagger.Infrastructure.Files;

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void MoveFile(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath, overwrite: false);

    public void CopyFile(string sourcePath, string destinationPath) =>
        File.Copy(sourcePath, destinationPath, overwrite: false);

    public void DeleteFile(string path) => File.Delete(path);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(path, cancellationToken);

    public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) =>
        File.WriteAllBytesAsync(path, data, cancellationToken);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
