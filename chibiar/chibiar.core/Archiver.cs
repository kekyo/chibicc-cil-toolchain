/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibiar.Archiving;
using chibiar.Cli;
using chibicc.toolchain.Archiving;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace chibiar;

public sealed class Archiver
{
    private readonly ILogger logger;

    public Archiver(ILogger logger) =>
        this.logger = logger;

    internal bool AddOrUpdate(
        string archiveFilePath,
        string[] objectFilePaths,
        bool writeSymbolTable,
        bool isDryrun)
    {
        using var scope = this.logger.BeginScope(LogLevels.Debug);

        var outputArchiveFilePath = Path.Combine(
            Path.GetTempPath(),
            $"chibiar_{Path.GetFileNameWithoutExtension(archiveFilePath)}_{Guid.NewGuid():N}{Path.GetExtension(archiveFilePath)}");

        try
        {
            var isExistArchiveFile = File.Exists(archiveFilePath);
            
            var symbolListEntries = ArchiveWriter.GetCombinedSymbolListEntries(
                this.logger,
                archiveFilePath,
                objectFilePaths);

            scope.Debug("Step 1");

            using (var outputArchiveFileStream = isDryrun ?
               new NullStream() :
               StreamUtilities.OpenStream(outputArchiveFilePath, true))
            {
                ArchiveWriter.WriteArchive(
                    this.logger,
                    outputArchiveFileStream,
                    symbolListEntries,
                    archiveFilePath,
                    writeSymbolTable);

                outputArchiveFileStream.Flush();
            }

            scope.Debug("Step 2");

            if (!isDryrun)
            {
                File.Delete(archiveFilePath);
                File.Move(outputArchiveFilePath, archiveFilePath);
            }

            scope.Debug("Step 3");

            return !isExistArchiveFile;
        }
        catch
        {
            if (!isDryrun)
            {
                File.Delete(outputArchiveFilePath);
            }
            throw;
        }
    }

    internal void Extract(
        string archiveFilePath,
        string[] objectNames,
        bool isDryrun)
    {
        using var scope = this.logger.BeginScope(LogLevels.Debug);

        var archiveReader = new ArchiveReader(archiveFilePath, objectNames);
        var read = objectNames.ToDictionary(objectName => objectName, _ => false);

        Parallel.ForEach(
            archiveReader.ObjectNames,
            objectName =>
            {
                if (!archiveReader.TryOpenObjectStream(objectName, false, out var objectStream))
                {
                    throw new ArgumentException(
                        $"Could not extract an object: Path={archiveFilePath}, Name={objectName}");
                }

                using var _ = objectStream;
                using var outputObjectFileStream = isDryrun ?
                    new NullStream() :
                    StreamUtilities.OpenStream(objectName, true);
                objectStream.CopyTo(outputObjectFileStream);
                outputObjectFileStream.Flush();

                lock (read)
                {
                    read[objectName] = true;
                }

                scope.Debug($"Extracted: {objectName}");
            });

        foreach (var entry in read.Where(entry => !entry.Value))
        {
            this.logger.Error($"Object is not found: {entry.Key}");
        }
    }

    internal void List(
        string archiveFilePath,
        string[] objectNames)
    {
        var archiveReader = new ArchiveReader(archiveFilePath, objectNames);

        foreach (var objectItem in archiveReader.ObjectNames)
        {
            Console.WriteLine(objectItem);
        }
    }

    public void Archive(CliOptions options)
    {
        switch (options.Mode)
        {
            case ArchiveModes.AddOrUpdate:
                if (this.AddOrUpdate(
                    options.ArchiveFilePath,
                    options.ObjectNames.ToArray(),
                    options.IsCreateSymbolTable,
                    options.IsDryRun) &&
                    !options.IsSilent)
                {
                    this.logger.Information($"creating {Path.GetFileName(options.ArchiveFilePath)}");
                }
                break;
            case ArchiveModes.Extract:
                this.Extract(
                    options.ArchiveFilePath,
                    options.ObjectNames.ToArray(),
                    options.IsDryRun);
                break;
            case ArchiveModes.List:
                this.List(
                    options.ArchiveFilePath,
                    options.ObjectNames.ToArray());
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
