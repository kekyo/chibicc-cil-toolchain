/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using chibicc.toolchain.Archiving;

using static VerifyNUnit.Verifier;
using static chibiar.ArchiverTestRunner;

namespace chibiar;

[TestFixture]
public sealed class ArchiverTests
{
    private static async Task VerifySymbolTableAsync(ZipArchive zip)
    {
       var symTableEntry = zip.GetEntry(ArchiverUtilities.SymbolTableFileName)!;
        using var afs = symTableEntry.Open();

        var tr = new StreamReader(afs, Encoding.UTF8, true);
        var actual = await tr.ReadToEndAsync();

        await Verify(actual);
    }
    
    [Test]
    public Task ArchiveOne()
    {
        return RunAsync(async basePath =>
        {
            var archivePath = Path.Combine(basePath, "output.a");
            
            var archiver = new Archiver();
            var actual = archiver.Add(
                archivePath,
                SymbolTableModes.Auto,
                new[]
                {
                    Path.Combine(ArtifactsBasePath, "parse.o"),
                },
                false);
            
            Assert.That(actual, Is.EqualTo(AddResults.Created));

            using var zip = ZipFile.OpenRead(archivePath);
            
            Assert.That(
                zip.Entries.Select(e => e.Name),
                Is.EqualTo(new[] { "parse.o", ArchiverUtilities.SymbolTableFileName }));

            await VerifySymbolTableAsync(zip);
        });
    }
    
    [Test]
    public Task ArchiveTwo()
    {
        return RunAsync(async basePath =>
        {
            var archivePath = Path.Combine(basePath, "output.a");
            
            var archiver = new Archiver();
            var actual = archiver.Add(
                archivePath,
                SymbolTableModes.Auto,
                new[]
                {
                    Path.Combine(ArtifactsBasePath, "parse.o"),
                    Path.Combine(ArtifactsBasePath, "codegen.o"),
                },
                false);
            
            Assert.That(actual, Is.EqualTo(AddResults.Created));

            using var zip = ZipFile.OpenRead(archivePath);
            
            Assert.That(
                zip.Entries.Select(e => e.Name),
                Is.EqualTo(new[] { "parse.o", "codegen.o", ArchiverUtilities.SymbolTableFileName }));

            await VerifySymbolTableAsync(zip);
        });
    }
}
