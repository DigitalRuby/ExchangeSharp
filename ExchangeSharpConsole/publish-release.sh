#!/bin/sh

set -xe

for RID in "linux-x64" "win-x64" "osx-x64"
do
    DIST="${PWD}/dist/${RID}/"
    rm -rf "${DIST}"
    mkdir -p "${DIST}"
    dotnet publish --force -r ${RID} -f netcoreapp30 -o "${DIST}" -c Release /NoLogo
done
