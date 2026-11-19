#!/usr/bin/env bash
set -euo pipefail
umask 022

root=/henry
app=user-management-api
app_dir="$root/$app"
incoming="$root/release"
config="$root/config/appsettings.Production.json"
service="$app.service"
health_url=http://127.0.0.1:5000/health

fail() {
	printf 'Error: %s\n' "$*" >&2
	exit 1
}

[[ -f "$config" && -s "$config" ]] || fail "Set up $config before deploying."
exec 9>"$root/.$app.deploy.lock"
flock -n 9 || fail "A deployment for $app is already running."
if [[ -L "$app_dir" || ( -e "$app_dir" && ! -d "$app_dir" ) ]]; then
	fail "$app_dir must be a regular directory or absent."
fi
if [[ -e "$app_dir/current" || -L "$app_dir/current" || -e "$app_dir/releases" || -L "$app_dir/releases" ]]; then
	fail "Remove the legacy current and releases entries from $app_dir before the first direct deployment."
fi

# CD passes an exact archive; manual runs without arguments select the newest file.
(( $# <= 1 )) || fail "Usage: $0 [absolute-archive-path]"
latest=${1:-}
if [[ -z "$latest" ]]; then
	for archive in "$incoming/$app-"*.tar.gz; do
		[[ -f "$archive" && ! -L "$archive" ]] || continue
		if [[ -z "$latest" || "$archive" -nt "$latest" ]]; then latest=$archive; fi
	done
fi
[[ -n "$latest" && "$latest" == /* && -f "$latest" && ! -L "$latest" ]] || fail "Published archive not found."
[[ -s "$latest.sha256" ]] || fail "SHA-256 file is required alongside the archive."
python3 - "$latest" <<'PYTHON'
import hashlib, sys, tarfile
from pathlib import Path, PurePosixPath
archive = Path(sys.argv[1])
digest, filename = Path(str(archive) + '.sha256').read_text().strip().split(maxsplit=1)
with archive.open('rb') as stream:
    actual = hashlib.file_digest(stream, 'sha256').hexdigest()
if digest != actual or filename != archive.name:
    raise SystemExit('Archive checksum validation failed.')
with tarfile.open(archive, 'r:gz') as package:
    for member in package.getmembers():
        path = PurePosixPath(member.name)
        if path.is_absolute() or '..' in path.parts or not (member.isfile() or member.isdir()):
            raise SystemExit('Archive contains an unsafe entry.')
PYTHON
gzip -t -- "$latest" || fail "The archive is incomplete or corrupt."
[[ $(systemctl show --property=WorkingDirectory --value "$service") == "$app_dir" ]] ||
	fail "Configure $service with WorkingDirectory=$app_dir before deploying."
printf 'Deploying %s\n' "${latest##*/}"

scratch=$(mktemp -d "$root/.$app.XXXXXX")
was_active=false
stopped=false
created=false
start_attempted=false

finish() {
	local status=$?
	if (( status != 0 )) && [[ "$stopped" == true ]]; then
		if [[ "$start_attempted" == true ]] && ! sudo -n systemctl stop "$service"; then
			printf 'Could not stop the failed release. Recovery files are in %s.\n' "$scratch" >&2
			return
		fi
		if [[ "$created" == true ]] && ! mv -T -- "$app_dir" "$scratch/failed"; then
			printf 'Could not clear the failed release. Recovery files are in %s.\n' "$scratch" >&2
			return
		fi
		if [[ -d "$scratch/previous" ]]; then
			if ! mv -T -- "$scratch/previous" "$app_dir"; then
				printf 'Restore the previous files from %s/previous.\n' "$scratch" >&2
				return
			fi
			printf 'Restored the previous API files.\n' >&2
		fi
		if [[ "$was_active" == true ]] && ! sudo -n systemctl start "$service"; then
			printf 'Could not restart the previous API. Check journalctl -u %s.\n' "$service" >&2
		fi
	fi
	rm -rf -- "$scratch"
}
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if systemctl is-active --quiet "$service"; then
	was_active=true
fi
sudo -n systemctl stop "$service"
stopped=true

# Clear the live path and keep the old files until the new API is healthy.
if [[ -d "$app_dir" ]]; then
	mv -T -- "$app_dir" "$scratch/previous"
fi
mkdir -- "$app_dir"
created=true
cp -- "$latest" "$scratch/api.tar.gz"
tar -xzf "$scratch/api.tar.gz" --no-same-owner --no-same-permissions -C "$app_dir"
[[ -f "$app_dir/UserManagement.Web.dll" && -f "$app_dir/UserManagement.Web.runtimeconfig.json" ]] ||
	fail "Archive does not contain the published API."
install -m 600 -- "$config" "$app_dir/appsettings.Production.json"

start_attempted=true
sudo -n systemctl start "$service"
for attempt in {1..10}; do
	if curl --fail --silent --max-time 2 "$health_url" > /dev/null; then
		printf 'API deployed to %s and passed its health check.\n' "$app_dir"
		exit 0
	fi
	sleep 1
done
fail "API did not pass its health check. Check journalctl -u $service."
