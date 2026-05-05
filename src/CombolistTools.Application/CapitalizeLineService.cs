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
