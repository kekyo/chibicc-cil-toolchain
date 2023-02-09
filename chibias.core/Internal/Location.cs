/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil.Cil;

namespace chibias.Internal;

internal sealed class FileDescriptor
{
    public readonly string? BasePath;
    public readonly string RelativePath;
    public readonly DocumentLanguage? Language;

    public FileDescriptor(
        string? basePath,
        string relativePath,
        DocumentLanguage? language)
    {
        this.BasePath = basePath;
        this.RelativePath = relativePath;
        this.Language = language;
    }
}

internal sealed class Location
{
    public readonly FileDescriptor File;
    public readonly uint StartLine;
    public readonly uint StartColumn;
    public readonly uint EndLine;
    public readonly uint EndColumn;

    public Location(
        FileDescriptor file,
        uint startLine,
        uint startColumn,
        uint endLine,
        uint endColumn)
    {
        this.File = file;
        this.StartLine = startLine;
        this.StartColumn = startColumn;
        this.EndLine = endLine;
        this.EndColumn = endColumn;
    }
}
