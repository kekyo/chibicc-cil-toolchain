@echo off

rem chibias-cil - The specialized backend CIL assembler for chibicc-cil
rem Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
rem
rem Licensed under MIT: https://opensource.org/licenses/MIT

echo.
echo "==========================================================="
echo "Build chibias"
echo.

rem git clean -xfd

dotnet restore

dotnet build -p:Configuration=Release -p:Platform="Any CPU"
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -p:PackageOutputPath="..\artifacts"
