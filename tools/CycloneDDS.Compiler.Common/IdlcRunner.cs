using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CycloneDDS.Compiler.Common
{
    public class IdlcRunner
    {
        public string? IdlcPathOverride { get; set; }
        public string? IdlcExtraArgs { get; set; }

        private static string IdlcExeName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "idlc.exe" : "idlc";

        private static string IdlcAltName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "idlc" : "idlc.exe";

        private static string[] RuntimeIds => new[] {
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "linux-x64" : "win-x64"
        };

        private static bool ExistsOnPath(string fileName, out string foundPath)
        {
            foundPath = string.Empty;
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null) return false;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    string path = Path.Combine(dir, fileName);
                    if (File.Exists(path)) { foundPath = path; return true; }
                }
                catch { }
            }
            return false;
        }

        public string FindIdlc()
        {
            if (!string.IsNullOrEmpty(IdlcPathOverride))
            {
                if (File.Exists(IdlcPathOverride)) return IdlcPathOverride;
                throw new FileNotFoundException($"idlc not found at override path: {IdlcPathOverride}");
            }

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;

            // Search strategy: try current-platform name first, then alt-name
            string[] candidateNames = { IdlcExeName, IdlcAltName };

            foreach (var name in candidateNames)
            {
                // Check current directory
                string local = Path.Combine(currentDir, name);
                if (File.Exists(local)) return local;

                // Check NuGet package location: tools/ -> ../runtimes/{rid}/native/
                foreach (var rid in RuntimeIds)
                {
                    try
                    {
                        string nugetPath = Path.Combine(currentDir, "..", "runtimes", rid, "native", name);
                        if (File.Exists(nugetPath)) return Path.GetFullPath(nugetPath);
                    }
                    catch { }
                }

                // DEV: workspace locations
                var searchDir = new DirectoryInfo(currentDir);
                for (int i = 0; i < 6; i++)
                {
                    if (searchDir == null) break;

                    string checkPath = Path.Combine(searchDir.FullName, "cyclonedds", "install", "bin", name);
                    if (File.Exists(checkPath)) return checkPath;

                    string repoPath = Path.Combine(searchDir.FullName, "cyclone-compiled", "bin", name);
                    if (File.Exists(repoPath)) return repoPath;

                    foreach (var rid in RuntimeIds)
                    {
                        repoPath = Path.Combine(searchDir.FullName, "artifacts", "native", rid, name);
                        if (File.Exists(repoPath)) return repoPath;
                    }

                    searchDir = searchDir.Parent;
                }

                // Check environment variable
                string? cycloneHome = Environment.GetEnvironmentVariable("CYCLONEDDS_HOME");
                if (!string.IsNullOrEmpty(cycloneHome))
                {
                    string path = Path.Combine(cycloneHome, "bin", name);
                    if (File.Exists(path)) return path;
                    path = Path.Combine(cycloneHome, name);
                    if (File.Exists(path)) return path;
                }

                // Check PATH
                if (ExistsOnPath(name, out string foundPath))
                    return foundPath;
            }

            throw new FileNotFoundException(
                $"idlc not found (tried {IdlcExeName} and {IdlcAltName}). Set CYCLONEDDS_HOME or add to PATH.");
        }

        public IdlcResult RunIdlc(string idlFilePath, string outputDir, string? includePath = null)
        {
            string idlcPath = FindIdlc();
            
            // Ensure output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = idlcPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // On Linux, set LD_LIBRARY_PATH so idlc can find its .so dependencies
            // that are packaged alongside it in the tools/ directory.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string idlcDir = Path.GetDirectoryName(idlcPath)!;
                string? existingLdPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
                string ldPath = string.IsNullOrEmpty(existingLdPath)
                    ? idlcDir
                    : idlcDir + Path.PathSeparator + existingLdPath;
                startInfo.EnvironmentVariables["LD_LIBRARY_PATH"] = ldPath;
            }
            
            if (!string.IsNullOrWhiteSpace(IdlcExtraArgs))
            {
                // Simple split by whitespace is sufficient for most compiler flags like "-Werror"
                // But if they have spaces inside quotes, this simple split would break. 
                // System.CommandLine parsing is better handled by caller, so we assume caller provides simple args
                foreach(var arg in IdlcExtraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    startInfo.ArgumentList.Add(arg);
                }
            }

            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add("json");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputDir);
            
            if (!string.IsNullOrEmpty(includePath))
            {
                startInfo.ArgumentList.Add("-I");
                startInfo.ArgumentList.Add(includePath);
            }
            
            startInfo.ArgumentList.Add(idlFilePath);
            
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("Failed to start idlc process.");
            }
            
            var stdoutBuilder = new System.Text.StringBuilder();
            var stderrBuilder = new System.Text.StringBuilder();

            // 1. Subscribe to the events
            process.OutputDataReceived += (sender, e) => 
            {
                if (e.Data != null) stdoutBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) => 
            {
                if (e.Data != null) stderrBuilder.AppendLine(e.Data);
            };

            // 2. Begin reading asynchronously
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 3. Wait for the process to finish
            process.WaitForExit();

            // 4. Extract the final strings
            string stdout = stdoutBuilder.ToString();
            string stderr = stderrBuilder.ToString();
            
            return new IdlcResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                GeneratedFiles = FindGeneratedFiles(outputDir, idlFilePath)
            };
        }

        public string GetArguments(string idlFilePath, string outputDir, string? includePath)
        {
            var args = $"-l json -o \"{outputDir}\"";
            if (!string.IsNullOrEmpty(includePath))
            {
                args += $" -I \"{includePath}\"";
            }
            args += $" \"{idlFilePath}\"";
            return args;
        }
        
        private string[] FindGeneratedFiles(string outputDir, string idlFile)
        {
            // idlc -l json generates: <basename>.json
            string baseName = Path.GetFileNameWithoutExtension(idlFile);
            var jsonFile = Path.Combine(outputDir, baseName + ".json");
            
            var files = new System.Collections.Generic.List<string>();
            if (File.Exists(jsonFile)) files.Add(jsonFile);
            
            return files.ToArray();
        }
    }
}
