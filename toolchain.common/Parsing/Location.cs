/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

namespace chibicc.toolchain.Parsing;

public enum Language
{
    Other,
    C,
    Cpp,
    CSharp,
    Basic,
    Java,
    Cobol,
    Pascal,
    Cil,
    JScript,
    Smc,
    MCpp,
    FSharp,
}

public sealed class FileDescriptor
{
    public readonly string? BasePath;
    public readonly string RelativePath;
    public readonly Language Language;
    public readonly bool IsVisible;

    public FileDescriptor(
        string? basePath,
        string relativePath,
        Language language,
        bool isVisible)
    {
        this.BasePath = basePath;
        this.RelativePath = relativePath;
        this.Language = language;
        this.IsVisible = isVisible;
    }

    public void Deconstruct(
        out string? basePath,
        out string relativePath,
        out Language language,
        out bool isVisible)
    {
        basePath = this.BasePath;
        relativePath = this.RelativePath;
        language = this.Language;
        isVisible = this.IsVisible;
    }

    public override string ToString() =>
        $"{this.RelativePath}: [{this.Language}{(this.IsVisible ? "" : ",Hidden")}]";
}

public sealed class Location
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

    public override string ToString() =>
        $"{this.File.RelativePath}({this.StartLine},{this.StartColumn}): [{this.File.Language}{(this.File.IsVisible ? "" : ",Hidden")}]";
}
