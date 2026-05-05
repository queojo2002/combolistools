# Capitalize First Character Per Line Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tool that reads an input text file line by line and writes an output file where each line has its **first character** converted with invariant uppercasing (example: `abc` → `Abc`), leaving the rest of the line unchanged.

**Architecture:** Pure per-line transformation lives in `CombolistTools.Core`. A thin `Application` service streams lines through `IFileReader` / `IFileWriter` (same pattern as `DuplicateRemoverService` / `FolderUserPassFilterService`). The WPF app exposes a new tab with a dedicated `ViewModel` and a new method on `IJobExecutionService`.

**Tech Stack:** .NET 8, C#, WPF (MVVM), `Microsoft.Extensions.DependencyInjection`, xUnit, async streams (`IAsyncEnumerable`).

---

## File structure (repository root: `CombolistTools/`)

| Path | Responsibility |
|------|----------------|
| `src/CombolistTools.Core/LineFirstCharacterTransformer.cs` | Stateless `TransformLine(string)` — uppercase index `0` only (empty string unchanged). |
| `src/CombolistTools.Core/CapitalizeLineOptions.cs` | Options DTO: input file path, output file path (`required` init pattern like `UserPassFilterOptions`). |
| `src/CombolistTools.Application/CapitalizeLineService.cs` | Orchestrates read → transform → write for one input file. |
| `src/CombolistTools.Application/DependencyInjection.cs` | Registers `CapitalizeLineService` as singleton. |
| `src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs` | Implements `CapitalizeLineAsync` on `JobExecutionService`; extend `IJobExecutionService`. |
| `src/CombolistTools.Presentation.Wpf/ViewModels/CapitalizeLineViewModel.cs` | Input/output paths, browse, run/cancel (mirror `RemoveDuplicateViewModel` without column/delimiter fields). |
| `src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs` | Adds `CapitalizeLine` property and wires constructor. |
| `src/CombolistTools.Presentation.Wpf/MainWindow.xaml` | New `TabItem` bound to `CapitalizeLine`. |
| `tests/CombolistTools.UnitTests/LineFirstCharacterTransformerTests.cs` | Unit tests for `TransformLine`. |
| `tests/CombolistTools.IntegrationTests/EndToEndTests.cs` | One async test: temp input → `CapitalizeLineService` → assert output lines. |

---

## Scope check

Single cohesive feature: one transformation rule (first character only), one streaming pipeline, one UI tab. No CLI scope in this plan (optional follow-up: add a `capitalize-lines` command to `CombolistTools.Presentation.Console/Program.cs` mirroring `RemoveDuplicateViewModel` paths).

---

### Task 1: `LineFirstCharacterTransformer` + unit tests

**Files:**
- Create: `src/CombolistTools.Core/LineFirstCharacterTransformer.cs`
- Create: `tests/CombolistTools.UnitTests/LineFirstCharacterTransformerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CombolistTools.UnitTests/LineFirstCharacterTransformerTests.cs`:

```csharp
using Xunit;

namespace CombolistTools.UnitTests;

public class LineFirstCharacterTransformerTests
{
    [Theory]
    [InlineData("abc", "Abc")]
    [InlineData("a", "A")]
    [InlineData("123x", "123x")]
    [InlineData("aBC", "ABC")]
    [InlineData(" hello", " hello")]
    public void TransformLine_WhenNonEmpty_UppercasesFirstCharacterOnly(string input, string expected)
    {
        var output = CombolistTools.Core.LineFirstCharacterTransformer.TransformLine(input);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void TransformLine_WhenEmpty_ReturnsEmpty()
    {
        Assert.Equal("", CombolistTools.Core.LineFirstCharacterTransformer.TransformLine(""));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (from repository root `CombolistTools/`):

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" --filter LineFirstCharacterTransformerTests -v minimal
```

Expected: FAIL (type `LineFirstCharacterTransformer` missing or method missing).

- [ ] **Step 3: Write minimal implementation**

Create `src/CombolistTools.Core/LineFirstCharacterTransformer.cs`:

```csharp
using System.Globalization;

namespace CombolistTools.Core;

public static class LineFirstCharacterTransformer
{
    public static string TransformLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var upperFirst = char.ToUpper(line[0], CultureInfo.InvariantCulture);
        return line.Length == 1 ? upperFirst.ToString() : string.Concat(upperFirst, line.AsSpan(1));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" --filter LineFirstCharacterTransformerTests -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CombolistTools.Core/LineFirstCharacterTransformer.cs tests/CombolistTools.UnitTests/LineFirstCharacterTransformerTests.cs
git commit -m "feat(core): capitalize first character per line transformer"
```

---

### Task 2: Options DTO + streaming service

**Files:**
- Create: `src/CombolistTools.Core/CapitalizeLineOptions.cs`
- Create: `src/CombolistTools.Application/CapitalizeLineService.cs`
- Modify: `src/CombolistTools.Application/DependencyInjection.cs`

- [ ] **Step 1: Add options type**

Create `src/CombolistTools.Core/CapitalizeLineOptions.cs`:

```csharp
namespace CombolistTools.Core;

public sealed class CapitalizeLineOptions
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
}
```

- [ ] **Step 2: Add application service**

Create `src/CombolistTools.Application/CapitalizeLineService.cs`:

```csharp
using CombolistTools.Core;

namespace CombolistTools.Application;

public sealed class CapitalizeLineService
{
    private readonly IFileReader _reader;
    private readonly IFileWriter _writer;

    public CapitalizeLineService(IFileReader reader, IFileWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public Task ExecuteAsync(CapitalizeLineOptions options, CancellationToken cancellationToken)
    {
        async IAsyncEnumerable<string> Enumerate()
        {
            await foreach (var line in _reader.ReadLinesAsync(options.InputPath, cancellationToken))
            {
                yield return LineFirstCharacterTransformer.TransformLine(line);
            }
        }

        return _writer.WriteLinesAsync(options.OutputPath, Enumerate(), cancellationToken);
    }
}
```

- [ ] **Step 3: Register in DI**

Replace `src/CombolistTools.Application/DependencyInjection.cs` with:

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
        services.AddSingleton<CapitalizeLineService>();
        return services;
    }
}
```

- [ ] **Step 4: Run full unit test suite (sanity)**

Run:

```powershell
dotnet test "tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj" -v minimal
```

Expected: PASS (no test covers service yet; ensures no regressions).

- [ ] **Step 5: Commit**

```bash
git add src/CombolistTools.Core/CapitalizeLineOptions.cs src/CombolistTools.Application/CapitalizeLineService.cs src/CombolistTools.Application/DependencyInjection.cs
git commit -m "feat(application): capitalize-line streaming service"
```

---

### Task 3: WPF job execution + ViewModel + tab

**Files:**
- Modify: `src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs`
- Create: `src/CombolistTools.Presentation.Wpf/ViewModels/CapitalizeLineViewModel.cs`
- Modify: `src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs`
- Modify: `src/CombolistTools.Presentation.Wpf/MainWindow.xaml`

- [ ] **Step 1: Extend job execution interface and implementation**

Replace `src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs` with:

```csharp
using CombolistTools.Application;
using CombolistTools.Core;

namespace CombolistTools.Presentation.Wpf.Services;

public interface IUiLogSink
{
    void Write(string message);
}

public interface IJobExecutionService
{
    Task RemoveDuplicateAsync(DuplicateRemovalOptions options, CancellationToken cancellationToken);
    Task MergeAsync(MergeOptions options, CancellationToken cancellationToken);
    Task SplitAsync(SplitOptions options, CancellationToken cancellationToken);
    Task FilterUserPassAsync(UserPassFilterOptions options, CancellationToken cancellationToken);
    Task CapitalizeLineAsync(CapitalizeLineOptions options, CancellationToken cancellationToken);
}

public sealed class UiLogSink : IUiLogSink
{
    public event Action<string>? OnMessage;
    public void Write(string message) => OnMessage?.Invoke(message);
}

public sealed class JobExecutionService : IJobExecutionService
{
    private readonly DuplicateRemoverService _duplicateRemover;
    private readonly FileMergeService _mergeService;
    private readonly FileSplitService _splitService;
    private readonly FolderUserPassFilterService _folderUserPassFilterService;
    private readonly CapitalizeLineService _capitalizeLineService;
    private readonly IUiLogSink _logSink;

    public JobExecutionService(
        DuplicateRemoverService duplicateRemover,
        FileMergeService mergeService,
        FileSplitService splitService,
        FolderUserPassFilterService folderUserPassFilterService,
        CapitalizeLineService capitalizeLineService,
        IUiLogSink logSink)
    {
        _duplicateRemover = duplicateRemover;
        _mergeService = mergeService;
        _splitService = splitService;
        _folderUserPassFilterService = folderUserPassFilterService;
        _capitalizeLineService = capitalizeLineService;
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

    public async Task CapitalizeLineAsync(CapitalizeLineOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running capitalize first character: {options.InputPath} -> {options.OutputPath}");
        await _capitalizeLineService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("capitalize first character completed.");
    }
}
```

- [ ] **Step 2: Create ViewModel**

Create `src/CombolistTools.Presentation.Wpf/ViewModels/CapitalizeLineViewModel.cs`:

```csharp
using System.Windows.Input;
using CombolistTools.Core;
using CombolistTools.Presentation.Wpf.Commands;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class CapitalizeLineViewModel : ViewModelBase
{
    private readonly IJobExecutionService _jobs;
    private readonly IUiLogSink _log;
    private readonly IPathPickerService _picker;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private string _inputPath = "";
    private string _outputPath = "";

    public string InputPath { get => _inputPath; set { if (SetProperty(ref _inputPath, value)) RaiseCommands(); } }
    public string OutputPath { get => _outputPath; set { if (SetProperty(ref _outputPath, value)) RaiseCommands(); } }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) RaiseCommands(); } }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseInputCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public CapitalizeLineViewModel(IJobExecutionService jobs, IUiLogSink log, IPathPickerService picker)
    {
        _jobs = jobs;
        _log = log;
        _picker = picker;
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning && !string.IsNullOrWhiteSpace(InputPath) && !string.IsNullOrWhiteSpace(OutputPath));
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsRunning);
        BrowseInputCommand = new RelayCommand(BrowseInput);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
    }

    private async Task RunAsync()
    {
        try
        {
            IsRunning = true;
            _cts = new CancellationTokenSource();
            await _jobs.CapitalizeLineAsync(new CapitalizeLineOptions
            {
                InputPath = InputPath,
                OutputPath = OutputPath
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Write("capitalize first character canceled.");
        }
        catch (Exception ex)
        {
            _log.Write($"capitalize first character failed: {ex.Message}");
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

    private void BrowseInput()
    {
        var selected = _picker.PickInputFile(InputPath, "Text/CSV/GZip (*.txt;*.csv;*.gz)|*.txt;*.csv;*.gz|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(selected)) InputPath = selected;
    }

    private void BrowseOutput()
    {
        var selected = _picker.PickOutputFile(OutputPath);
        if (!string.IsNullOrWhiteSpace(selected)) OutputPath = selected;
    }
}
```

- [ ] **Step 3: Wire `MainViewModel`**

Replace `src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs` with:

```csharp
using System.Collections.ObjectModel;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public RemoveDuplicateViewModel RemoveDuplicate { get; }
    public MergeViewModel Merge { get; }
    public SplitViewModel Split { get; }
    public FilterUserPassViewModel FilterUserPass { get; }
    public CapitalizeLineViewModel CapitalizeLine { get; }
    public ObservableCollection<string> Logs { get; } = [];

    public MainViewModel(IJobExecutionService jobs, IUiLogSink logSink, IPathPickerService picker)
    {
        RemoveDuplicate = new RemoveDuplicateViewModel(jobs, logSink, picker);
        Merge = new MergeViewModel(jobs, logSink, picker);
        Split = new SplitViewModel(jobs, logSink, picker);
        FilterUserPass = new FilterUserPassViewModel(jobs, logSink, picker);
        CapitalizeLine = new CapitalizeLineViewModel(jobs, logSink, picker);

        if (logSink is UiLogSink sink)
        {
            sink.OnMessage += message =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Add($"{DateTime.Now:HH:mm:ss}  {message}");
                    if (Logs.Count > 500) Logs.RemoveAt(0);
                });
            };
        }
    }
}
```

- [ ] **Step 4: Add tab to `MainWindow.xaml`**

Insert a new `TabItem` inside the main `TabControl` (place order is cosmetic; e.g. after "Remove Duplicate"):

```xml
<TabItem Header="Capitalize Line" DataContext="{Binding CapitalizeLine}">
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

        <TextBlock Text="Input file" Grid.Row="0" Grid.Column="0" Margin="0,6" VerticalAlignment="Center"/>
        <TextBox Text="{Binding InputPath, UpdateSourceTrigger=PropertyChanged}" Grid.Row="0" Grid.Column="1" Margin="0,6" />
        <Button Grid.Row="0" Grid.Column="2" Content="Choose..." Margin="8,6,0,6" MinWidth="90" Command="{Binding BrowseInputCommand}" />

        <TextBlock Text="Output file" Grid.Row="1" Grid.Column="0" Margin="0,6" VerticalAlignment="Center"/>
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

- [ ] **Step 5: Build WPF project**

Run:

```powershell
dotnet build "src/CombolistTools.Presentation.Wpf/CombolistTools.Presentation.Wpf.csproj" -v minimal
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/CombolistTools.Presentation.Wpf/Services/JobExecutionService.cs src/CombolistTools.Presentation.Wpf/ViewModels/CapitalizeLineViewModel.cs src/CombolistTools.Presentation.Wpf/ViewModels/MainViewModel.cs src/CombolistTools.Presentation.Wpf/MainWindow.xaml
git commit -m "feat(wpf): capitalize first character tab and job"
```

---

### Task 4: Integration test for `CapitalizeLineService`

**Files:**
- Modify: `tests/CombolistTools.IntegrationTests/EndToEndTests.cs`

- [ ] **Step 1: Add failing test**

Append to class `EndToEndTests` in `tests/CombolistTools.IntegrationTests/EndToEndTests.cs`:

```csharp
[Fact]
public async Task CapitalizeLineService_TransformsFirstCharacterPerLine()
{
    var dir = CreateTempDir();
    var input = Path.Combine(dir, "input.txt");
    var output = Path.Combine(dir, "output.txt");
    await File.WriteAllLinesAsync(input, ["abc", "xyz", ""]);

    var svc = new CapitalizeLineService(new AutoDetectFileReader(), new AutoDetectFileWriter());
    await svc.ExecuteAsync(new CapitalizeLineOptions { InputPath = input, OutputPath = output }, CancellationToken.None);

    var lines = await File.ReadAllLinesAsync(output);
    Assert.Equal(["Abc", "Xyz", ""], lines);
}
```

`using CombolistTools.Application;` is already present at the top of `EndToEndTests.cs`; do not duplicate it.

- [ ] **Step 2: Run integration test**

Run:

```powershell
dotnet test "tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj" --filter CapitalizeLineService_TransformsFirstCharacterPerLine -v minimal
```

Expected: PASS once Tasks 1–2 are complete.

- [ ] **Step 3: Commit**

```bash
git add tests/CombolistTools.IntegrationTests/EndToEndTests.cs
git commit -m "test(integration): capitalize line service end-to-end"
```

---

## Self-review

**Spec coverage**

| Requirement | Task |
|-------------|------|
| First character only (`abc` → `Abc`) | Task 1 |
| Stream large files via existing reader/writer | Tasks 2–3 |
| WPF UI entry point | Task 3 |
| Automated verification | Tasks 1, 4 |

**Placeholder scan:** No TBD/TODO/similar-to-other-task shortcuts; each task includes concrete code or exact edits.

**Type consistency:** `CapitalizeLineOptions` (`InputPath`, `OutputPath`), `LineFirstCharacterTransformer.TransformLine`, `CapitalizeLineService.ExecuteAsync(CapitalizeLineOptions, ...)`, `IJobExecutionService.CapitalizeLineAsync(...)` align across layers.

**Known limitation (documented):** Transformation uses `line[0]` as a UTF-16 code unit; supplementary-plane characters represented by surrogate pairs are not treated as a single logical first character (consistent with existing `char`-based code such as `UserPassLineTransformer`).

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-05-capitalize-first-character-per-line.md`. Two execution options:

**1. Subagent-Driven (recommended)** — Dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using superpowers:executing-plans, batch execution with checkpoints.

Which approach?
