/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Archiving;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using static VerifyNUnit.Verifier;
using static chibiar.ArchiverTestRunner;

namespace chibiar;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class ArchiverTests
{
    private static async Task VerifySymbolTableAsync(ArchiveReader archiveReader)
    {
        Assert.That(
            archiveReader.TryOpenObjectStream(ArchiverUtilities.SymbolTableFileName, true, out var afs),
            Is.True);

        using var tr = StreamUtilities.CreateTextReader(afs);
        var actual = await tr.ReadToEndAsync();

        await Verify(actual);
    }

    [Test]
    public Task ArchiveOne()
    {
        return RunAsync(async (basePath, logger) =>
        {
            logger.Information($"Test runner BasePath={basePath}");

            var archivePath = Path.Combine(basePath, "output.a");

            var archiver = new Archiver(logger);
            var actual = archiver.AddOrUpdate(
                archivePath,
                [ Path.Combine(ArtifactsBasePath, "parse.o"), ],
                true,
                false);

            Assert.That(actual, Is.True);

            var archiveReader = new ArchiveReader(archivePath);

            Assert.That(
                archiveReader.ObjectNames,
                Is.EqualTo(new[] { ArchiverUtilities.SymbolTableFileName, "parse.o", }));

            await VerifySymbolTableAsync(archiveReader);
        });
    }

    [Test]
    public Task ArchiveTwo()
    {
        return RunAsync(async (basePath, logger) =>
        {
            var archivePath = Path.Combine(basePath, "output.a");

            var archiver = new Archiver(logger);
            var actual = archiver.AddOrUpdate(
                archivePath,
                [
                    Path.Combine(ArtifactsBasePath, "parse.o"),
                    Path.Combine(ArtifactsBasePath, "codegen.o"),
                ],
                true,
                false);

            Assert.That(actual, Is.True);

            var archiveReader = new ArchiveReader(archivePath);

            Assert.That(
                archiveReader.ObjectNames,
                Is.EqualTo(new[] { ArchiverUtilities.SymbolTableFileName, "parse.o", "codegen.o" }));

            await VerifySymbolTableAsync(archiveReader);
        });
    }
    
    [Test]
    public Task Update()
    {
        return RunAsync(async (basePath, logger) =>
        {
            var archivePath = Path.Combine(basePath, "output.a");

            var newCodegenPath = Path.Combine(basePath, "codegen.o");

            // It's made for dummy updated codegen.o.
            File.Copy(
                Path.Combine(ArtifactsBasePath, "tokenize.o"),
                newCodegenPath);

            var archiver = new Archiver(logger);
            var actual1 = archiver.AddOrUpdate(
                archivePath,
                [
                    Path.Combine(ArtifactsBasePath, "parse.o"),
                    Path.Combine(ArtifactsBasePath, "codegen.o"),
                ],
                true,
                false);

            Assert.That(actual1, Is.True);

            var actual2 = archiver.AddOrUpdate(
                archivePath,
                [ newCodegenPath, ],
                true,
                false);

            Assert.That(actual2, Is.False);

            var archiveReader = new ArchiveReader(archivePath);

            Assert.That(
                archiveReader.ObjectNames,
                Is.EqualTo(new[] { ArchiverUtilities.SymbolTableFileName, "parse.o", "codegen.o", }));

            await VerifySymbolTableAsync(archiveReader);
        });
    }
}
