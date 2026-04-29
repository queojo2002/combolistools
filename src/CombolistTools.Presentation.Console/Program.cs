using CombolistTools.Application;
using CombolistTools.Core;
using CombolistTools.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.SetBasePath(AppContext.BaseDirectory);
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddLogging(c => c.AddSimpleConsole(o => o.SingleLine = true));
        services.AddApplication();
        services.AddInfrastructure(ctx.Configuration);
    })
    .Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cli");
var duplicateService = host.Services.GetRequiredService<DuplicateRemoverService>();
var mergeService = host.Services.GetRequiredService<FileMergeService>();
var splitService = host.Services.GetRequiredService<FileSplitService>();

if (args.Length == 0)
{
    PrintUsage();
    return;
}

try
{
    var command = args[0].ToLowerInvariant();
    switch (command)
    {
        case "remove-duplicate":
            {
                if (args.Length < 3) throw new ArgumentException("Usage: remove-duplicate <input> <output> [--columns=0,2] [--delimiter=,]");
                var options = new DuplicateRemovalOptions
                {
                    InputPath = args[1],
                    OutputPath = args[2],
                    ColumnIndexes = ParseColumns(args),
                    Delimiter = ParseDelimiter(args)
                };
                await duplicateService.ExecuteAsync(options, cts.Token);
                break;
            }
        case "merge":
            {
                if (args.Length < 3) throw new ArgumentException("Usage: merge <folder> <output> [--columns=0,2] [--delimiter=,] [--pattern=*.csv]");
                var options = new MergeOptions
                {
                    InputFolderPath = args[1],
                    OutputPath = args[2],
                    SearchPattern = ParsePattern(args),
                    ColumnIndexes = ParseColumns(args),
                    Delimiter = ParseDelimiter(args)
                };
                await mergeService.ExecuteAsync(options, cts.Token);
                break;
            }
        case "split":
            {
                if (args.Length < 4) throw new ArgumentException("Usage: split <input> <outputFolder> <threshold> [--mode=lines|size]");
                var mode = ParseMode(args);
                var options = new SplitOptions
                {
                    InputPath = args[1],
                    OutputFolder = args[2],
                    Threshold = long.Parse(args[3]),
                    Mode = mode
                };
                await splitService.ExecuteAsync(options, cts.Token);
                break;
            }
        default:
            PrintUsage();
            break;
    }
}
catch (OperationCanceledException)
{
    logger.LogWarning("Operation canceled.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Execution failed.");
    Environment.ExitCode = 1;
}

return;

static int[] ParseColumns(string[] args)
{
    var arg = args.FirstOrDefault(a => a.StartsWith("--columns=", StringComparison.OrdinalIgnoreCase));
    if (arg is null) return [];
    return arg.Split('=')[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
}

static char ParseDelimiter(string[] args)
{
    var arg = args.FirstOrDefault(a => a.StartsWith("--delimiter=", StringComparison.OrdinalIgnoreCase));
    if (arg is null) return ',';
    return arg.Split('=')[1][0];
}

static string ParsePattern(string[] args)
{
    var arg = args.FirstOrDefault(a => a.StartsWith("--pattern=", StringComparison.OrdinalIgnoreCase));
    return arg is null ? "*.*" : arg.Split('=')[1];
}

static SplitMode ParseMode(string[] args)
{
    var arg = args.FirstOrDefault(a => a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase));
    var value = arg is null ? "lines" : arg.Split('=')[1].ToLowerInvariant();
    return value == "size" ? SplitMode.BySizeBytes : SplitMode.ByLineCount;
}

static void PrintUsage()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project src/CombolistTools.Presentation.Console -- remove-duplicate input.csv output.csv");
    Console.WriteLine("  dotnet run --project src/CombolistTools.Presentation.Console -- merge folder output.csv");
    Console.WriteLine("  dotnet run --project src/CombolistTools.Presentation.Console -- split input.csv outputFolder 100000");
}
