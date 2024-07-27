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
if [ ! -f "../LethalMon/bin/Release/netstandard2.1/LethalMon.dll" ]; then
  echo "../LethalMon/bin/Release/netstandard2.1/LethalMon.dll not found"
  exit 1
fi
echo -e "../LethalMon/bin/Release/netstandard2.1/LethalMon.dll found and has been compiled on ${RED}$(date -r ../LethalMon/bin/Release/netstandard2.1/LethalMon.dll +"%Y-%m-%dT%H:%M:%S%z")${NC}, press [Enter] once the compilation is okay"
read -rp "" < /dev/tty

# Ask for version
version=''
while [[ ! $version =~ ^[0-9]{1,2}\.[0-9]{1,2}\.[0-9]{1,2}$ ]]; do
  echo "Please enter the version of the package (format xx.xx.xx)"
  read -r version
  echo
done

# Check if version exists in the CHANGELOG.md file
if ! grep -q "# $version" ../CHANGELOG.md; then
  echo "Version not found in ../CHANGELOG.md"
    exit 1
fi
read -rp "Version found in ../CHANGELOG.md, press [Enter] if all changes has been added correctly" < /dev/tty

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
cp ../LethalMon/bin/Release/netstandard2.1/LethalMon.dll ./build/plugins/LethalMon

# Create zip
7za a -tzip ./build/Feiryn-LethalMon-"$version".zip ./build/CHANGELOG.md ./build/README.md ./build/LICENSE ./build/icon.png ./build/manifest.json ./build/plugins

# Echo end
echo "Zip created in build folder"
