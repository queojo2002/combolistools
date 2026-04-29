# CombolistTools Large File Processor

Console and WPF app for processing very large CSV/TXT and `.gz` files with streaming-first architecture.

## Commands

- `dotnet run --project src/CombolistTools.Presentation.Console -- remove-duplicate input.csv output.csv`
- `dotnet run --project src/CombolistTools.Presentation.Console -- remove-duplicate input.csv output.csv --columns=0,2`
- `dotnet run --project src/CombolistTools.Presentation.Console -- merge ./data merged.csv --pattern=*.csv`
- `dotnet run --project src/CombolistTools.Presentation.Console -- split input.csv ./parts 100000 --mode=lines`
- `dotnet run --project src/CombolistTools.Presentation.Console -- split input.csv ./parts 52428800 --mode=size`

## Notes

- All processing is stream-based (`StreamReader`/`StreamWriter`).
- Duplicate detection uses Bloom Filter + HashSet confirmation.
- `.gz` files are auto-detected by file extension.
- Parallel chunk processing is configurable through `appsettings.json`.

## WPF UI

- Run UI: `dotnet run --project src/CombolistTools.Presentation.Wpf`
- UI has 3 tabs: Remove Duplicate, Merge Files, Split File.
- Each tab supports Run/Cancel and shared execution logs.
