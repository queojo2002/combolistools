using CombolistTools.Core;

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
                await foreach (var line in _reader.ReadLinesAsync(file, cancellationToken))
                {
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

