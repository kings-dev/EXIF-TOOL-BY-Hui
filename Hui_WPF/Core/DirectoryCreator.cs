using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hui_WPF.Models;

namespace Hui_WPF.Core
{
    public class DirectoryCreator
    {
        public DirectoryCreator() { }

        public async Task<int> CreateDirectoriesAsync(
            string basePath,
            DirectoryRule rule,
             Func<int, string, Task> reportProgressCallback,
            CancellationToken cancellationToken = default)
        {
            int createdCount = 0;
            try
            {
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }
                createdCount = await CreateDirectoriesRecursiveAsync(basePath, rule, reportProgressCallback, cancellationToken, 0);
                return createdCount;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException($"Directory creation failed: {ex.Message}", ex);
            }
        }

        private async Task<int> CreateDirectoriesRecursiveAsync(
            string currentBasePath,
            DirectoryRule rule,
            Func<int, string, Task> reportProgressCallback,
            CancellationToken cancellationToken,
            int currentTotalCreatedCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int locallyCreated = 0;

            for (int i = 1; i <= rule.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string folderName = $"{DirectoryRule.CleanPathSegment(rule.Prefix)}_{i:D3}";
                string fullPath = Path.Combine(currentBasePath, folderName);

                try
                {
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                        locallyCreated++;
                        await reportProgressCallback(currentTotalCreatedCount + locallyCreated, fullPath);
                    }

                    if (rule.Recursive && rule.SubRules != null)
                    {
                        foreach (var subRule in rule.SubRules)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            locallyCreated += await CreateDirectoriesRecursiveAsync(fullPath, subRule, reportProgressCallback, cancellationToken, currentTotalCreatedCount + locallyCreated);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { throw; }
                catch (IOException) { throw; }
                catch (Exception ex) { throw new Exception($"Error creating directory '{fullPath}': {ex.Message}", ex); }
            }
            return locallyCreated;
        }
    }
}