using System.Threading.Tasks;
using CombolistTools.Application;
using CombolistTools.Core;
using CombolistTools.Infrastructure;
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
            "zanduc:asd123", // kept => zanduc:Asd123
            "zanduc:Asd123", // skipped (first letter in pass is uppercase)
            "hello"          // not user:pass => skipped
        });

        await File.WriteAllLinesAsync(input2, new[]
        {
            "zanduc:123asd", // kept => zanduc:123Asd
            "zanduc:1Asd2"    // skipped
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

