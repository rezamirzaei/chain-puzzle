#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/src/ChainPuzzle.Desktop/ChainPuzzle.Desktop.csproj"
ARTIFACT_DIR="$ROOT_DIR/artifacts"

resolve_default_rid() {
  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"

  case "$os-$arch" in
    Darwin-arm64) echo "osx-arm64" ;;
    Darwin-x86_64) echo "osx-x64" ;;
    Linux-x86_64) echo "linux-x64" ;;
    Linux-aarch64) echo "linux-arm64" ;;
    *)
      echo "Unsupported host '$os-$arch'. Pass an explicit RID, for example win-x64 or linux-x64." >&2
      exit 1
      ;;
  esac
}

RID="${1:-$(resolve_default_rid)}"
PUBLISH_DIR="$ARTIFACT_DIR/publish/$RID"
ZIP_PATH="$ARTIFACT_DIR/ChainChapters-$RID.zip"

rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"
mkdir -p "$ARTIFACT_DIR"

dotnet publish "$PROJECT" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained false \
  -p:PublishSingleFile=false \
  -o "$PUBLISH_DIR"

rm -f "$ZIP_PATH"
if command -v ditto >/dev/null 2>&1; then
  ditto -c -k --keepParent "$PUBLISH_DIR" "$ZIP_PATH"
elif command -v zip >/dev/null 2>&1; then
  (
    cd "$ARTIFACT_DIR/publish"
    zip -rq "$ZIP_PATH" "$RID"
  )
else
  echo "No zip tool found; leaving publish folder at $PUBLISH_DIR" >&2
  exit 0
fi

echo "Published folder: $PUBLISH_DIR"
echo "Packaged zip: $ZIP_PATH"
