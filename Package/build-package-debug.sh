#! /bin/bash

RED='\033[0;31m'
NC='\033[0m' # No Color

# Test if manifest exists
if [ ! -f "./manifest.json" ]; then
  echo "./manifest.json not foundy"
  exit 1
fi

# Test if icon exists
if [ ! -f "icon.png" ]; then
  echo "./icon.png not found"
  exit 1
fi

# Test if README.md exists
if [ ! -f "../README.md" ]; then
  echo "../README.md not found"
  exit 1
fi

# Test if CHANGELOG.md exists
if [ ! -f "../CHANGELOG.md" ]; then
  echo "../CHANGELOG.md not found"
  exit 1
fi

# Test if LICENSE exists
if [ ! -f "../LICENSE" ]; then
  echo "../LICENSE not found"
  exit 1
fi

# Test if lethalmon unity file exists and ask for confirmation
if [ ! -f "../LethalMon/bin/Debug/netstandard2.1/lethalmon" ]; then
  echo "../LethalMon/bin/Debug/netstandard2.1/lethalmon not found"
  exit 1
fi
echo -e "Unity file ../LethalMon/bin/Debug/netstandard2.1/lethalmon found and has been compiled on ${RED}$(date -r ../LethalMon/bin/Debug/netstandard2.1/lethalmon +"%Y-%m-%dT%H:%M:%S%z")${NC}, press [Enter] once the compilation is okay"
read -rp "" < /dev/tty

# Test if LethalMon.dll file exists and ask for confirmation
if [ ! -f "../LethalMon/bin/Debug/netstandard2.1/LethalMon.dll" ]; then
  echo "../LethalMon/bin/Debug/netstandard2.1/LethalMon.dll not found"
  exit 1
fi
echo -e "../LethalMon/bin/Debug/netstandard2.1/LethalMon.dll found and has been compiled on ${RED}$(date -r ../LethalMon/bin/Debug/netstandard2.1/LethalMon.dll +"%Y-%m-%dT%H:%M:%S%z")${NC}, press [Enter] once the compilation is okay"
read -rp "" < /dev/tty

# Version
version='0.0.0'

# Create build folder and subfolders only if not exists
mkdir -p build
mkdir -p build/plugins
mkdir -p build/plugins/LethalMon

# Create temp manifest with the version
jq --arg version "$version" '.version_number = $version' ./manifest.json > ./build/manifest.json

# Copy files
cp ./icon.png ./build/
cp ../README.md ./build/
cp ../CHANGELOG.md ./build/
cp ../LICENSE ./build/
cp ../LethalMon/bin/Debug/netstandard2.1/lethalmon ./build/plugins/LethalMon
cp ../LethalMon/bin/Debug/netstandard2.1/LethalMon.dll ./build/plugins/LethalMon

# Create zip
7za a -tzip ./build/Feiryn-LethalMon-"$version".zip ./build/CHANGELOG.md ./build/README.md ./build/LICENSE ./build/icon.png ./build/manifest.json ./build/plugins

# Echo end
echo "Zip created in build folder"
