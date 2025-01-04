/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibiar.Cli;
using chibicc.toolchain.Archiving;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;
using System;
using System.Collections.Generic;
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
        var outputArchiveFilePath = Path.Combine(
            Path.GetTempPath(),
            $"chibiar_{Path.GetFileNameWithoutExtension(archiveFilePath)}_{Guid.NewGuid():N}{Path.GetExtension(archiveFilePath)}");

        try
        {
            var isExistArchiveFile = File.Exists(archiveFilePath);
            
            var symbolListEntries = ArchiverUtilities.GetCombinedSymbolListEntries(
                archiveFilePath,
                objectFilePaths);

            using (var outputArchiveFileStream = isDryrun ?
               new NullStream() :
               StreamUtilities.OpenStream(outputArchiveFilePath, true))
            {
                ArchiverUtilities.WriteArchive(
                    outputArchiveFileStream,
                    symbolListEntries,
                    archiveFilePath,
                    writeSymbolTable);
            }

            if (!isDryrun)
            {
                File.Delete(archiveFilePath);
                File.Move(outputArchiveFilePath, archiveFilePath);
            }

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
        var hashedObjectNames = new HashSet<string>(objectNames);
        var descriptors = ArchiverUtilities.LoadArchivedObjectItemDescriptors(
            archiveFilePath, aod => hashedObjectNames.Contains(aod.ObjectName) ? aod : null).
            ToArray();

        Parallel.ForEach(descriptors,
            descriptor =>
            {
                var aod = (ArchivedObjectItemDescriptor)descriptor;
                
                using var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false);
                
                archiveFileStream.Position = aod.Position;
                var objectStream = new RangedStream(
                    archiveFileStream, aod.Length, false);
                
                using var outputObjectFileStream = isDryrun ?
                    new NullStream() :
                    StreamUtilities.OpenStream(aod.ObjectName, true);
                objectStream.CopyTo(outputObjectFileStream);
                outputObjectFileStream.Flush();
            });

        foreach (var exceptName in descriptors.
            Select(aod => aod!.ObjectName).
            Except(objectNames))
        {
            this.logger.Error($"Object is not found: {exceptName}");
        }
    }

    internal void List(
        string archiveFilePath,
        string[] objectNames)
    {
        var hashedObjectNames = new HashSet<string>(objectNames);
        var descriptors = ArchiverUtilities.LoadArchivedObjectItemDescriptors(
            archiveFilePath, aod => hashedObjectNames.Contains(aod.ObjectName) ? aod : null).
            ToArray();

        foreach (var descriptor in descriptors)
        {
            Console.WriteLine(descriptor.ObjectName);
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
