namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Reusable, UI-agnostic file IO primitives for drive throughput benchmarking.
    /// All members are pure file-system operations and contain no presentation logic, so they
    /// can be shared by any front-end (WinForms widget, CLI, tests, ...).
    /// </summary>
    public static class DriveBenchmarkIo
    {
        /// <summary>
        /// Writes <paramref name="fileSizeBytes"/> bytes to <paramref name="path"/> in buffer-sized chunks,
        /// reporting progress per chunk and honoring cancellation.
        /// </summary>
        public static async Task WriteWorkerFileAsync(string path, long fileSizeBytes, byte[] buffer, int blockSizeBytes, FileOptions options, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, blockSizeBytes, options);
            long remainingBytes = fileSizeBytes;
            while (remainingBytes > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunk = (int) Math.Min(buffer.Length, remainingBytes);
                await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remainingBytes -= chunk;
                progressBytesCallback?.Invoke(chunk);
            }

            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Sequentially reads the entire file at <paramref name="path"/> in buffer-sized chunks,
        /// reporting progress per chunk and honoring cancellation.
        /// </summary>
        public static async Task ReadWorkerFileAsync(string path, byte[] buffer, int blockSizeBytes, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.SequentialScan);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                progressBytesCallback?.Invoke(bytesRead);
            }
        }

        /// <summary>
        /// Resolves a writable temp directory located on the same physical drive as <paramref name="rootPath"/>,
        /// probing a prioritized list of candidate locations until one accepts a write.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Thrown when no writable folder is found on the drive.</exception>
        public static async Task<string> ResolveWritableTempDirectoryAsync(string rootPath, CancellationToken cancellationToken)
        {
            string normalizedRootPath = Path.GetPathRoot(rootPath) ?? rootPath;
            string userTempPath = Path.GetTempPath();
            string userTempRoot = Path.GetPathRoot(userTempPath) ?? string.Empty;

            List<string> candidates = [];
            if (string.Equals(userTempRoot, normalizedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(userTempPath, "FormsSystemStatsWidget", "DriveSpeedTest"));
            }

            string userName = Environment.UserName;
            candidates.Add(Path.Combine(normalizedRootPath, "Users", userName, "AppData", "Local", "Temp", "FormsSystemStatsWidget", "DriveSpeedTest"));
            candidates.Add(Path.Combine(normalizedRootPath, "Users", "Public", "Documents", "FormsSystemStatsWidget", "DriveSpeedTest"));
            candidates.Add(Path.Combine(normalizedRootPath, "Temp", "FormsSystemStatsWidget", "DriveSpeedTest"));

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _ = Directory.CreateDirectory(candidate);
                    string probeFilePath = Path.Combine(candidate, $".fssw-probe-{Guid.NewGuid():N}.tmp");
                    await using FileStream probe = new(probeFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    await probe.WriteAsync(new byte[] { 0xAA }, cancellationToken);
                    return candidate;
                }
                catch
                {
                }
            }

            throw new UnauthorizedAccessException($"No writable temp folder was found on drive '{normalizedRootPath}'.");
        }
    }
}
