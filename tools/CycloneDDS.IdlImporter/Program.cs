using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace CycloneDDS.IdlImporter;

/// <summary>
/// Entry point for the CycloneDDS IDL Importer tool.
/// Converts IDL files to C# DSL using idlc JSON output.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var masterIdlArg = new Argument<string>("master-idl")
        {
            Description = "Path to the entry-point IDL file"
        };

        var sourceRootOption = new Option<string?>("--source-root")
        {
            Description = "Root directory containing all IDL files (default: master-idl directory)"
        };

        var outputRootOption = new Option<string?>("--output-root")
        {
            Description = "Root directory for generated C# files (default: current directory)"
        };

        var idlcPathOption = new Option<string?>("--idlc-path")
        {
            Description = "Path to idlc executable (default: auto-detect)"
        };

        var idlcArgsOption = new Option<string?>("--idlc-args")
        {
            Description = "Extra arguments to pass to idlc"
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable detailed logging"
        };

        var rootCommand = new RootCommand("CycloneDDS IDL Importer v1.0")
        {
            masterIdlArg,
            sourceRootOption,
            outputRootOption,
            idlcPathOption,
            idlcArgsOption,
            verboseOption
        };

        rootCommand.SetAction(
            async (parseResult) =>
            {
                var masterIdl = parseResult.GetValue(masterIdlArg);
                if (string.IsNullOrEmpty(masterIdl))
                {
                    Console.Error.WriteLine("master-idl is required!");
                    return;
                }
                var sourceRoot = parseResult.GetValue(sourceRootOption);
                var outputRoot = parseResult.GetValue(outputRootOption);
                var idlcPath = parseResult.GetValue(idlcArgsOption);
                var idlcArgs = parseResult.GetValue(idlcArgsOption);
                var verbose = parseResult.GetValue(verboseOption);
                // Default logic
                if (string.IsNullOrEmpty(sourceRoot))
                {
                    sourceRoot = Path.GetDirectoryName(Path.GetFullPath(masterIdl)) ?? Directory.GetCurrentDirectory();
                }

                if (string.IsNullOrEmpty(outputRoot))
                {
                    outputRoot = Directory.GetCurrentDirectory();
                }

                try
                {
                    await RunImporter(masterIdl, sourceRoot, outputRoot, idlcPath, idlcArgs, verbose);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();

                    if (verbose)
                    {
                        Console.Error.WriteLine(ex.StackTrace);
                    }

                    Environment.Exit(1);
                }
            });

        rootCommand.Parse(args);
        return 0;
    }

    private static async Task RunImporter(
        string masterIdl,
        string sourceRoot,
        string outputRoot,
        string? idlcPath,
        string? idlcArgs,
        bool verbose)
    {
        // Validate arguments
        if (!File.Exists(masterIdl))
        {
            throw new FileNotFoundException($"Master IDL file not found: {masterIdl}");
        }

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source root directory not found: {sourceRoot}");
        }

        var fullMasterPath = Path.GetFullPath(masterIdl);
        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        var fullOutputRoot = Path.GetFullPath(outputRoot);

        if (!fullMasterPath.StartsWith(fullSourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Master IDL file must be located within the source root directory");
        }

        Console.WriteLine("CycloneDDS IDL Importer");
        Console.WriteLine("=======================");
        Console.WriteLine($"Master IDL:   {masterIdl}");
        Console.WriteLine($"Source Root:  {sourceRoot}");
        Console.WriteLine($"Output Root:  {outputRoot}");
        if (!string.IsNullOrEmpty(idlcPath))
        {
            Console.WriteLine($"IDLC Path:    {idlcPath}");
        }
        if (!string.IsNullOrEmpty(idlcArgs))
        {
            Console.WriteLine($"IDLC Args:    {idlcArgs}");
        }
        Console.WriteLine();

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(fullOutputRoot);

        // Run the Importer
        try
        {
            var importer = new Importer(verbose, idlcPath, idlcArgs);
            importer.Import(fullMasterPath, fullSourceRoot, fullOutputRoot);
        }
        catch (Exception)
        {
            // Log error but allow Main to catch it for consistent exit codes
            throw;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Import complete");
        Console.ResetColor();

        await Task.CompletedTask;
    }
}
