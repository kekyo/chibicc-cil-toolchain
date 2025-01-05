/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.IO;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace chibicc.toolchain.Archiving;

public sealed class ArchiveReader
{
    private readonly string archiveFilePath;
    private readonly Dictionary<string, ArchivedObjectItemDescriptor> aods;

    public ArchiveReader(
        string archiveFilePath)
    {
        this.archiveFilePath = archiveFilePath;
        
        var descriptors = ArchiverUtilities.LoadArchivedObjectItemDescriptors(
            this.archiveFilePath, aod => aod);
        this.ObjectNames = descriptors.
            Select(d => d.ObjectName).
            ToArray();
        this.aods = descriptors.
            ToDictionary(d => d.ObjectName, d => (ArchivedObjectItemDescriptor)d);
    }

    public ArchiveReader(
        string archiveFilePath,
        IEnumerable<string> objectNames)
    {
        this.archiveFilePath = archiveFilePath;

        var hashedObjectNames = new HashSet<string>(objectNames);
        var descriptors = ArchiverUtilities.LoadArchivedObjectItemDescriptors(
            this.archiveFilePath, aod => hashedObjectNames.Contains(aod.ObjectName) ? aod : null);
        this.ObjectNames = descriptors.
            Select(d => d.ObjectName).
            ToArray();
        this.aods = descriptors.
            ToDictionary(d => d.ObjectName, d => (ArchivedObjectItemDescriptor)d);
    }

    public IReadOnlyList<string> ObjectNames { get; }

    public bool TryOpenObjectStream(
        string objectName,
        bool decodeBody,
        out Stream stream)
    {
        if (!this.aods.TryGetValue(objectName, out var aod))
        {
            stream = null!;
            return false;
        }

        var archiveFileStream = StreamUtilities.OpenStream(
            this.archiveFilePath,
            false);
        archiveFileStream.Position = aod.Position;
        var rangedStream = new RangedStream(
            archiveFileStream,
            aod.Length,
            false);
        stream = decodeBody ?
            new GZipStream(rangedStream, CompressionMode.Decompress) :
            rangedStream;
        return true;
    }
}
