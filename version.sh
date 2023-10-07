#!/bin/bash

# Check for input
if [ -z "$1" ]; then
  echo "Usage: ./bump_version.sh [major|minor|patch]"
  exit 1
fi

# Determine which version part to bump
case $1 in
  "major")
    index=0
    ;;
  "minor")
    index=1
    ;;
  "patch")
    index=2
    ;;
  *)
    echo "Invalid option. Choose [major|minor|patch]."
    exit 1
    ;;
esac

# Extract the version from the .csproj file
current_version=$(grep "<Version>.*</Version>" TujenMem.csproj | sed -E "s/.*<Version>(.*)<\/Version>/\1/")

# Increment the version
IFS='.' read -ra parts <<< "$current_version"
((parts[$index]++))

# Reset lower indices
for (( i=$index+1; i<${#parts[@]}; i++ )); do
  parts[$i]=0
done

new_version="${parts[0]}.${parts[1]}.${parts[2]}"

# Update the .csproj file with the new version
sed -i "s/<Version>$current_version<\/Version>/<Version>$new_version<\/Version>/g" TujenMem.csproj

# Commit and tag
git add TujenMem.csproj
git commit -m "Release version $new_version"
git tag "v$new_version"
git push origin master --tags
