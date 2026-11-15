#!/usr/bin/env bash
# Monta o .deb do agente em artifacts/ (A19):
#   ./packaging/build-deb.sh          # amd64
#   ./packaging/build-deb.sh arm64
# Só precisa do SDK do .NET e do dpkg-deb; não pede root.
set -euo pipefail

arch="${1:-amd64}"
case "$arch" in
	amd64) rid=linux-x64 ;;
	arm64) rid=linux-arm64 ;;
	*) echo "Arquitetura \"$arch\" desconhecida: use amd64 ou arm64." >&2; exit 1 ;;
esac

repo="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo/SentinelAgente/SentinelAgente.csproj"
version="$(dotnet msbuild "$project" -getProperty:Version)"
maintainer="SentinelAgente <sentinel-agente@example.com>"
package="$repo/artifacts/sentinel-agent_${version}_${arch}.deb"

root="$(mktemp -d)"
trap 'rm -rf "$root"' EXIT

# Depende do runtime da distribuição (dotnet-runtime-10.0): quem corrige o .NET
# é a atualização de segurança do Ubuntu, não um pacote novo do agente.
dotnet publish "$project" -c Release -r "$rid" --self-contained false \
	-p:DebugType=none -o "$root/usr/lib/sentinel-agent"

install -D -m 644 "$repo/packaging/sentinel-agent.service" "$root/usr/lib/systemd/system/sentinel-agent.service"
install -D -m 644 "$repo/packaging/deb/copyright" "$root/usr/share/doc/sentinel-agent/copyright"

# O changelog que a política pede, com a data do último commit: o mesmo commit
# dá o mesmo pacote.
date="$(git -C "$repo" log -1 --format=%cD)"
printf 'sentinel-agent (%s) unstable; urgency=medium\n\n  * Veja o git log do SentinelAgente.\n\n -- %s  %s\n' \
	"$version" "$maintainer" "$date" | gzip -9n > "$root/usr/share/doc/sentinel-agent/changelog.gz"

find "$root" -type d -exec chmod 755 {} +
find "$root" -type f -exec chmod 644 {} +
chmod 755 "$root/usr/lib/sentinel-agent/sentinel-agent"

install -d "$root/DEBIAN"
size="$(du -sk "$root" | cut -f1)"
sed -e "s|@VERSION@|$version|" -e "s|@ARCH@|$arch|" -e "s|@MAINTAINER@|$maintainer|" -e "s|@SIZE@|$size|" \
	"$repo/packaging/deb/control" > "$root/DEBIAN/control"
for script in postinst prerm postrm; do
	install -m 755 "$repo/packaging/deb/$script" "$root/DEBIAN/$script"
done
(cd "$root" && find usr -type f -print0 | sort -z | xargs -0 md5sum) > "$root/DEBIAN/md5sums"

mkdir -p "$repo/artifacts"
SOURCE_DATE_EPOCH="$(git -C "$repo" log -1 --format=%ct)" dpkg-deb --root-owner-group --build "$root" "$package"
echo "$package"
