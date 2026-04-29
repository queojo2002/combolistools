# User:Pass Folder Filter Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one new WPF tab that reads all files in a user-chosen folder, filters lines matching `user:pass`, transforms the first alphabetic character in `pass` to uppercase (only if it is currently lowercase), skips lines where the first alphabetic character in `pass` is already uppercase, then writes results to a user-chosen output file.

**Architecture:** Keep the existing Clean Architecture split. Implement pure parsing/transformation in `Core`, streaming folder->output processing in `Application`, then wire UI in `Presentation.Wpf` via a new `ViewModel` and a new method on `IJobExecutionService`.

**Tech Stack:** .NET 8, C# async streams (`IAsyncEnumerable`), WPF (XAML + MVVM), `Microsoft.Extensions.DependencyInjection`, xUnit.

---

## Scope Check

This spec adds a new, cohesive feature that reuses the existing abstractions for streaming file I/O and dependency injection. It can be implemented as one subsystem with three integration layers (Core/Application/Presentation.Wpf) and covered by unit tests for the parsing/transformation and for the folder streaming service.

---

### Task 1: Implement line transformer + unit tests

**Files:**
- Create: `src/CombolistTools.Core/UserPassLineTransformer.cs`
- Create: `tests/CombolistTools.UnitTests/UserPassLineTransformerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CombolistTools.UnitTests/UserPassLineTransformerTests.cs`:

```csharp
using Xunit;

namespace CombolistTools.UnitTests;

public class UserPassLineTransformerTests
{
    [Theory]
    [InlineData("zanduc:asd123", "zanduc:Asd123")]
    [InlineData("zanduc:123asd", "zanduc:123Asd")]
    [InlineData("zanduc:1w2e", "zanduc:1W2e")]
    [InlineData("user:1234", "user:1234")] // no letters -> keep as-is
    [InlineData("user:", "user:")] // empty pass -> keep as-is
    public void TransformUserPassLine_WhenMatchedAndFirstLetterIsLowercaseOrMissingLetters_ReturnsTransformedLine(string input, string expected)
    {
        var output = CombolistTools.Core.UserPassLineTransformer.TransformUserPassLine(input);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("zanduc:Asd123")]
    [InlineData("zanduc:1Asd2")]
    [InlineData("a:B:C")] // strict format requires exactly one ':'
    [InlineData("badformat")] // no ':'
    public void TransformUserPassLine_WhenFormatIsNotUserPassOrFirstLetterInPassIsUppercase_ReturnsNull(string input)
    {
        var output = CombolistTools.Core.UserPassLineTransformer.TransformUserPassLine(input);
        Assert.Null(output);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" --filter UserPassLineTransformerTests -v minimal
```

Expected: FAIL (compile error: missing `CombolistTools.Core.UserPassLineTransformer`).

- [ ] **Step 3: Write minimal implementation**

Create `src/CombolistTools.Core/UserPassLineTransformer.cs`:

```csharp
using System;
using System.Globalization;

namespace CombolistTools.Core;

public static class UserPassLineTransformer
{
    // Returns:
    // - transformed line if the input matches `user:pass` and should be kept
    // - null if the input does not match the strict `user:pass` format, or if it should be skipped
    public static string? TransformUserPassLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        var firstColon = line.IndexOf(':');
        if (firstColon <= 0) return null; // user must be non-empty
        if (firstColon != line.LastIndexOf(':')) return null; // strict exactly one ':'

        var user = line[..firstColon];
        var pass = line[(firstColon + 1)..];

        if (string.IsNullOrEmpty(user)) return null;

        var firstLetterIndex = FindFirstLetterIndex(pass);
        if (firstLetterIndex < 0)
        {
            // No letters in pass => keep as-is (rule only mentions skipping when first letter is uppercase)
            return line;
        }

        var firstLetter = pass[firstLetterIndex];
        if (char.IsUpper(firstLetter))
        {
            // Skip if the first alphabetic character in pass is uppercase
            return null;
        }

        // Uppercase only the first alphabetic character, keep the rest unchanged.
        var uppercased = char.ToUpper(firstLetter, CultureInfo.InvariantCulture);
        if (uppercased == firstLetter) return line; // defensive; shouldn't happen given IsUpper check

        var transformedPass = pass[..firstLetterIndex] + uppercased + pass[(firstLetterIndex + 1)..];
        return user + ":" + transformedPass;
    }

    private static int FindFirstLetterIndex(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i])) return i;
        }

        return -1;
    }
}
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run:

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" --filter UserPassLineTransformerTests -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/CombolistTools.Core/UserPassLineTransformer.cs tests/CombolistTools.UnitTests/UserPassLineTransformerTests.cs
git commit -m "$(cat <<'EOF'
feat: add user:pass line transformer

Add deterministic parsing and uppercase-first-letter transformation with unit tests.
EOF
)"
```

---

### Task 2: Implement folder streaming service + unit tests

**Files:**
- Create: `src/CombolistTools.Core/UserPassFilterOptions.cs`
- Create: `src/CombolistTools.Application/FolderUserPassFilterService.cs`
- Modify: `src/CombolistTools.Application/DependencyInjection.cs`
- Create: `tests/CombolistTools.UnitTests/FolderUserPassFilterServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CombolistTools.UnitTests/FolderUserPassFilterServiceTests.cs`:

```csharp
using System.Threading.Tasks;
using CombolistTools.Application;
using CombolistTools.Core;
using CombolistTools.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CombolistTools.UnitTests;

public class FolderUserPassFilterServiceTests
{
    [Fact]
    public async Task ExecuteAsync_FiltersAndTransformsAcrossAllFiles_AndIgnoresOutputFileInSameFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var input1 = Path.Combine(dir, "1.txt");
        var input2 = Path.Combine(dir, "2.txt");
        var output = Path.Combine(dir, "out.txt");

        // Include a line in the output file that would be kept/transformed,
        // to verify the service does not read the output file as an input.
        await File.WriteAllLinesAsync(output, ["zanduc:asd123"]);

        await File.WriteAllLinesAsync(input1, new[]
        {
            "zanduc:asd123",   // kept => zanduc:Asd123
            "zanduc:Asd123",   // skipped (first letter in pass is uppercase)
            "hello"             // not user:pass => skipped
        });

        await File.WriteAllLinesAsync(input2, new[]
        {
            "zanduc:123asd",   // kept => zanduc:123Asd
            "zanduc:1Asd2"     // skipped
        });

        var svc = new FolderUserPassFilterService(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter());

        await svc.ExecuteAsync(
            new UserPassFilterOptions { InputFolderPath = dir, OutputPath = output },
            CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(new[] { "zanduc:Asd123", "zanduc:123Asd" }, lines);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" --filter FolderUserPassFilterServiceTests -v minimal
```

Expected: FAIL (compile error: missing `UserPassFilterOptions` and/or `FolderUserPassFilterService`).

- [ ] **Step 3: Write minimal implementation**

Create `src/CombolistTools.Core/UserPassFilterOptions.cs`:

```csharp
namespace CombolistTools.Core;

public sealed class UserPassFilterOptions
{
    public required string InputFolderPath { get; init; }
    public required string OutputPath { get; init; }
}
```

Create `src/CombolistTools.Application/FolderUserPassFilterService.cs`:

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CombolistTools.Core;
using CombolistTools.Infrastructure;

namespace CombolistTools.Application;

public sealed class FolderUserPassFilterService
{
    private readonly IFileReader _reader;
    private readonly IFileWriter _writer;

    public FolderUserPassFilterService(IFileReader reader, IFileWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public Task ExecuteAsync(UserPassFilterOptions options, CancellationToken cancellationToken)
    {
        var outputFullPath = Path.GetFullPath(options.OutputPath);

        var files = Directory.EnumerateFiles(options.InputFolderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFullPath(path), outputFullPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        async IAsyncEnumerable<string> Enumerate()
        {
            foreach (var file in files)
            {
                await foreach (var line in _reader.ReadLinesAsync(file, cancellationToken).WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var transformed = UserPassLineTransformer.TransformUserPassLine(line);
                    if (transformed is not null)
                    {
                        yield return transformed;
                    }
                }
            }
        }

        return _writer.WriteLinesAsync(options.OutputPath, Enumerate(), cancellationToken);
    }
}
```

Modify `src/CombolistTools.Application/DependencyInjection.cs` to register the service:

```csharp
using CombolistTools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CombolistTools.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(new ProcessingOptions());
        services.AddSingleton<DuplicateRemoverService>();
        services.AddSingleton<FileMergeService>();
        services.AddSingleton<FileSplitService>();
        services.AddSingleton<FolderUserPassFilterService>();
        return services;
    }
}
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run:

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" --filter FolderUserPassFilterServiceTests -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/CombolistTools.Core/UserPassFilterOptions.cs src/CombolistTools.Application/FolderUserPassFilterService.cs src/CombolistTools.Application/DependencyInjection.cs tests/CombolistTools.UnitTests/FolderUserPassFilterServiceTests.cs
git commit -m "$(cat <<'EOF'
feat: add folder user:pass filter service

Stream all files from a folder, transform/filter matching user:pass lines, and write to the chosen output file.
EOF
)"
```

---

### Task 3: Wire new tab into WPF UI (MVVM + DI)

**Files:**
- Modify: `src/CombolistTools.Presentation.Wpf/MainWindow.xaml`
- Modify: `src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs`
- Create: `src/CombolistTools.Presentation.Wpf/ViewModels/FilterUserPassViewModel.cs`
- Modify: `src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs` (interface + implementation)
- Modify: `src/CombolistTools.Application/DependencyInjection.cs` (already in Task 2, ensure it includes new registration)

- [ ] **Step 1: Make the build fail (interface change without implementation)**

Edit `src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs` and update only the `IJobExecutionService` interface to add the new method:

```csharp
public interface IJobExecutionService
{
    Task RemoveDuplicateAsync(DuplicateRemovalOptions options, CancellationToken cancellationToken);
    Task MergeAsync(MergeOptions options, CancellationToken cancellationToken);
    Task SplitAsync(SplitOptions options, CancellationToken cancellationToken);
    Task FilterUserPassAsync(UserPassFilterOptions options, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Run build to verify it fails**

Run:

```powershell
dotnet build "src/CombolistTools.Presentation.Wpf/CombolistTools.Presentation.Wpf.csproj" -v minimal
```

Expected: FAIL (compile error: `JobExecutionService` does not implement `IJobExecutionService.FilterUserPassAsync`).

- [ ] **Step 3: Implement UI wiring**

1) Update `src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs` (inject filter service + implement method):

```csharp
public sealed class JobExecutionService : IJobExecutionService
{
    private readonly DuplicateRemoverService _duplicateRemover;
    private readonly FileMergeService _mergeService;
    private readonly FileSplitService _splitService;
    private readonly FolderUserPassFilterService _folderUserPassFilterService;
    private readonly IUiLogSink _logSink;

    public JobExecutionService(
        DuplicateRemoverService duplicateRemover,
        FileMergeService mergeService,
        FileSplitService splitService,
        FolderUserPassFilterService folderUserPassFilterService,
        IUiLogSink logSink)
    {
        _duplicateRemover = duplicateRemover;
        _mergeService = mergeService;
        _splitService = splitService;
        _folderUserPassFilterService = folderUserPassFilterService;
        _logSink = logSink;
    }

    public async Task RemoveDuplicateAsync(DuplicateRemovalOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running remove-duplicate: {options.InputPath} -> {options.OutputPath}");
        await _duplicateRemover.ExecuteAsync(options, cancellationToken);
        _logSink.Write("remove-duplicate completed.");
    }

    public async Task MergeAsync(MergeOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running merge: {options.InputFolderPath} -> {options.OutputPath}");
        await _mergeService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("merge completed.");
    }

    public async Task SplitAsync(SplitOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running split: {options.InputPath} -> {options.OutputFolder}");
        await _splitService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("split completed.");
    }

    public async Task FilterUserPassAsync(UserPassFilterOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running user:pass filter: {options.InputFolderPath} -> {options.OutputPath}");
        await _folderUserPassFilterService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("user:pass filter completed.");
    }
}
```

2) Create `src/CombolistTools.Presentation.Wpf/ViewModels/FilterUserPassViewModel.cs`:

```csharp
using System.Windows.Input;
using CombolistTools.Core;
using CombolistTools.Presentation.Wpf.Commands;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class FilterUserPassViewModel : ViewModelBase
{
    private readonly IJobExecutionService _jobs;
    private readonly IUiLogSink _log;
    private readonly IPathPickerService _picker;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private string _inputFolder = "";
    private string _outputPath = "";

    public string InputFolder { get => _inputFolder; set { if (SetProperty(ref _inputFolder, value)) RaiseCommands(); } }
    public string OutputPath { get => _outputPath; set { if (SetProperty(ref _outputPath, value)) RaiseCommands(); } }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) RaiseCommands(); } }

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseInputFolderCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public FilterUserPassViewModel(IJobExecutionService jobs, IUiLogSink log, IPathPickerService picker)
    {
        _jobs = jobs;
        _log = log;
        _picker = picker;

        RunCommand = new AsyncRelayCommand(RunAsync,
            () => !IsRunning && !string.IsNullOrWhiteSpace(InputFolder) && !string.IsNullOrWhiteSpace(OutputPath));
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsRunning);
        BrowseInputFolderCommand = new RelayCommand(BrowseInputFolder);
        BrowseOutputCommand = new RelayCommand(BrowseOutputFile);
    }

    private async Task RunAsync()
    {
        try
        {
            IsRunning = true;
            _cts = new CancellationTokenSource();
            await _jobs.FilterUserPassAsync(
                new UserPassFilterOptions
                {
                    InputFolderPath = InputFolder,
                    OutputPath = OutputPath
                },
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Write("user:pass filter canceled.");
        }
        catch (Exception ex)
        {
            _log.Write($"user:pass filter failed: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private Task CancelAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private void RaiseCommands()
    {
        if (RunCommand is AsyncRelayCommand run) run.RaiseCanExecuteChanged();
        if (CancelCommand is AsyncRelayCommand cancel) cancel.RaiseCanExecuteChanged();
    }

    private void BrowseInputFolder()
    {
        var selected = _picker.PickFolder(InputFolder);
        if (!string.IsNullOrWhiteSpace(selected)) InputFolder = selected;
    }

    private void BrowseOutputFile()
    {
        var selected = _picker.PickOutputFile(OutputPath);
        if (!string.IsNullOrWhiteSpace(selected)) OutputPath = selected;
    }
}
```

3) Update `src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs`:

Add a property and instantiate it:

```csharp
public sealed class MainViewModel : ViewModelBase
{
    public RemoveDuplicateViewModel RemoveDuplicate { get; }
    public MergeViewModel Merge { get; }
    public SplitViewModel Split { get; }
    public FilterUserPassViewModel FilterUserPass { get; }
    public ObservableCollection<string> Logs { get; } = [];

    public MainViewModel(IJobExecutionService jobs, IUiLogSink logSink, IPathPickerService picker)
    {
        RemoveDuplicate = new RemoveDuplicateViewModel(jobs, logSink, picker);
        Merge = new MergeViewModel(jobs, logSink, picker);
        Split = new SplitViewModel(jobs, logSink, picker);
        FilterUserPass = new FilterUserPassViewModel(jobs, logSink, picker);

        // Keep the existing log subscription code exactly as it is in MainViewModel.
    }
}
```

4) Update `src/CombolistTools.Presentation.Wpf/MainWindow.xaml`:

Add a new `TabItem` inside the `TabControl`:

```xml
<TabItem Header="User:Pass Filter" DataContext="{Binding FilterUserPass}">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="160" />
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>

        <TextBlock Text="Input folder" Grid.Row="0" Grid.Column="0" Margin="0,6" VerticalAlignment="Center" />
        <TextBox Text="{Binding InputFolder, UpdateSourceTrigger=PropertyChanged}" Grid.Row="0" Grid.Column="1" Margin="0,6" />
        <Button Grid.Row="0" Grid.Column="2" Content="Choose..." Margin="8,6,0,6" MinWidth="90" Command="{Binding BrowseInputFolderCommand}" />

        <TextBlock Text="Output file" Grid.Row="1" Grid.Column="0" Margin="0,6" VerticalAlignment="Center" />
        <TextBox Text="{Binding OutputPath, UpdateSourceTrigger=PropertyChanged}" Grid.Row="1" Grid.Column="1" Margin="0,6" />
        <Button Grid.Row="1" Grid.Column="2" Content="Choose..." Margin="8,6,0,6" MinWidth="90" Command="{Binding BrowseOutputCommand}" />

        <StackPanel Grid.Row="2" Grid.ColumnSpan="3" Orientation="Horizontal" Margin="0,14,0,0">
            <Button Content="Run" Width="140" Margin="0,0,8,0" Command="{Binding RunCommand}" />
            <Button Content="Cancel" Width="140" Command="{Binding CancelCommand}" />
            <TextBlock Margin="16,0,0,0" VerticalAlignment="Center" Text="{Binding IsRunning, StringFormat=Running: {0}}" />
        </StackPanel>
    </Grid>
</TabItem>
```

Place it where you want among the existing tabs (e.g. between “Merge Files” and “Split File”).

- [ ] **Step 4: Run the build and make sure it passes**

Run:

```powershell
dotnet build "src/CombolistTools.Presentation.Wpf/CombolistTools.Presentation.Wpf.csproj" -v minimal
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" -v minimal
dotnet test "tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/CombolistTools.Presentation.Wpf/MainWindow.xaml src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs src/CombolistTools.Presentation.Wpf/ViewModels/FilterUserPassViewModel.cs src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs
git commit -m "$(cat <<'EOF'
feat: add user:pass folder filter tab

Wire new folder->output processing pipeline into the WPF UI as a dedicated tab.
EOF
)"
```

