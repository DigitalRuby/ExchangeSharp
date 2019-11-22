#!/bin/sh

set -e

if [ -z $INSTALL_DIR ]; then
  INSTALL_DIR=/usr/local/bin/
else
  INSTALL_DIR="$(realpath -s $INSTALL_DIR)"
fi

OS=
case "$(uname -s)" in
  Darwin)
    OS=osx
    ;;
  Linux)
    OS=linux
    ;;
  CYGWIN*|MINGW32*|MSYS*)
    OS=win
    ;;
  *)
    echo -e 'Failed to identify the OS.'
    return 1
    ;;
esac

TAG=$(curl -Ssf https://api.github.com/repos/jjxtra/ExchangeSharp/releases/latest | jq -r .name)
echo "Downloading version: '${TAG}'..."

curl -SfL# -o /tmp/exchangesharp.zip "https://github.com/jjxtra/ExchangeSharp/releases/download/${TAG}/${OS}-x64.zip"

unzip -qq -o /tmp/exchangesharp.zip -d /tmp/exchangesharp/

sh -c "set -ex; sudo mv /tmp/exchangesharp/exchange-sharp ${INSTALL_DIR}exchange-sharp"

rm -rf /tmp/exchangesharp.zip /tmp/exchangesharp/

echo "🎉 Installed in ${INSTALL_DIR}exchange-sharp"
