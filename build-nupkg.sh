#!/bin/sh

# chibicc-toolchain - The specialized backend toolchain for chibicc-cil
# Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
#
# Licensed under MIT: https://opensource.org/licenses/MIT

echo
echo "==========================================================="
echo "Build chibicc-toolchain"
echo

# git clean -xfd

dotnet restore

dotnet build -p:Configuration=Release -p:Platform="Any CPU"
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -p:PackageOutputPath="../artifacts"
