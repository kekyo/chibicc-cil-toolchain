#!/bin/sh

# chibias-cil - The specialized backend CIL assembler for chibicc-cil
# Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
#
# Licensed under MIT: https://opensource.org/licenses/MIT

echo
echo "==========================================================="
echo "Build chibias"
echo

# git clean -xfd

dotnet restore

dotnet build -p:Configuration=Release -p:Platform="Any CPU"
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -o artifacts
