#!/usr/bin/env bash
set -euo pipefail

version="${1:?Usage: verify_package_consumer.sh VERSION PACKAGE_DIR local|public}"
package_dir="${2:?Missing PACKAGE_DIR}"
source_mode="${3:?Missing local|public source mode}"
if [[ "$source_mode" != "local" && "$source_mode" != "public" ]]; then
  echo "Source mode must be local or public" >&2
  exit 2
fi

consumer_dir="$(mktemp -d)"
trap 'rm -rf "$consumer_dir"' EXIT
cp .github/verification/PackageConsumer.csproj "$consumer_dir/PackageConsumer.csproj"
cp .github/verification/Program.cs "$consumer_dir/Program.cs"
sed -i "s/__GORM_VERSION__/$version/g" "$consumer_dir/PackageConsumer.csproj"

if [[ "$source_mode" == "local" ]]; then
  test -f "$package_dir/GORM.$version.nupkg"
  cat > "$consumer_dir/NuGet.Config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$package_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="GORM" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
CONFIG
else
  # Fresh package cache means local CI packages cannot disguise a broken public release.
  export NUGET_PACKAGES="$consumer_dir/fresh-packages"
  cat > "$consumer_dir/NuGet.Config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
CONFIG
fi

dotnet restore "$consumer_dir/PackageConsumer.csproj" --configfile "$consumer_dir/NuGet.Config" --force --no-cache
dotnet run --project "$consumer_dir/PackageConsumer.csproj" -c Release --no-restore