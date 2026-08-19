using System;
using System.Collections.Generic;
using System.Text;

namespace FormsSystemStatsWidget.Core
{
    public static class LlamaCppModelLoader
    {
        public static string GgufModelsDirectory { get; set; } = $@"D:\\Models\GGUF\Others";
        public static string[] ModelFilePaths => GetModelFilePaths();


        /// <summary>
        /// Get all model file paths in the GGUF models directory, sorted by newest created model file first.
        /// </summary>
        /// <param name="mmprojRegex">Optional regex to filter mmproj files, so they won't get recognized as main model file. Default value filters for *mmproj*.gguf ('mmproj' substring somewhere in file name and with .gguf extension)</param>
        /// <returns>Array of model file paths.</returns>
        public static string[] GetModelFilePaths(string? mmprojRegex = @".*mmproj.*\.gguf$")
        {
            if (!System.IO.Directory.Exists(GgufModelsDirectory))
            {
                return [];
            }

            // Get all model root dirs in the GGUF models directory (a model root dir contains at least one GGUF file, smaller one is optionally *mmproj*.gguf, get return biggest gguf file path)
            var modelRootDirs = System.IO.Directory.GetDirectories(GgufModelsDirectory);
            var modelFilePaths = new List<string>();
            foreach (var modelRootDir in modelRootDirs)
            {
                var ggufFiles = System.IO.Directory.GetFiles(modelRootDir, "*.gguf");
                if (ggufFiles.Length > 0)
                {
                    // Get the biggest gguf file path
                    var biggestGgufFilePath = ggufFiles.OrderByDescending(f => new System.IO.FileInfo(f).Length).First();
                    
                    if (string.IsNullOrEmpty(mmprojRegex))
                    {
                        modelFilePaths.Add(biggestGgufFilePath);
                    }
                    else
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(biggestGgufFilePath, mmprojRegex))
                        {
                            modelFilePaths.Add(biggestGgufFilePath);
                        }
                    }
                }
            }

            // Sort by newest created model file first
            modelFilePaths = modelFilePaths.OrderByDescending(f => new System.IO.FileInfo(f).CreationTimeUtc).ToList();

            Logger.Log($"Found {modelFilePaths.Count} model(s) in GGUF models directory: {GgufModelsDirectory}");
            return modelFilePaths.ToArray();
        }

        public static string? GetModelMmprojFilePath(string modelFilePathOrName)
        {
            if (!File.Exists(modelFilePathOrName))
            {
                // Try resolving file name with model directory
                modelFilePathOrName = System.IO.Path.Combine(GgufModelsDirectory, modelFilePathOrName);
            }
            else
            {
                modelFilePathOrName = System.IO.Path.GetDirectoryName(modelFilePathOrName) ?? string.Empty;
                if (!Directory.Exists(modelFilePathOrName))
                {
                    return null;
                }
            }

            var modelRootDir = System.IO.Path.GetFullPath(modelFilePathOrName);
            if (modelRootDir == null)
            {
                return null;
            }

            var ggufFiles = System.IO.Directory.GetFiles(modelRootDir, "*.gguf");
            if (ggufFiles.Length <= 1)
            {
                Logger.Log($"Model root dir {modelRootDir} does not contain more than 1 GGUF file, skip searching for mmproj file.");
                return null;
            }

            var smallerGgufFile = ggufFiles.OrderBy(f => new System.IO.FileInfo(f).Length).First();
            Logger.Log($"Found smaller GGUF file: {smallerGgufFile} (probably the mmproj file)");
            return smallerGgufFile;
        }


    }
}
