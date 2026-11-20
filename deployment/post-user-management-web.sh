#!/usr/bin/env bash
set -euo pipefail
umask 022

root=/henry
app=user-management-web
app_dir="$root/$app"
incoming="$root/release"
config="$root/config/web.env"

fail() {
	printf 'Error: %s\n' "$*" >&2
	exit 1
}

[[ -f "$config" && -s "$config" ]] || fail "Set up $config before deploying."

mkdir -p -- "$app_dir/releases"
exec 9>"$app_dir/.deploy.lock"
flock -n 9 || fail "A deployment for $app is already running."
if [[ -e "$app_dir/current" && ! -L "$app_dir/current" ]]; then
	fail "$app_dir/current already exists and is not a symlink."
fi

latest=''
for archive in "$incoming/$app-"*.tar.gz; do
	[[ -f "$archive" && ! -L "$archive" ]] || continue
	if [[ -z "$latest" || "$archive" -nt "$latest" ]]; then
		latest=$archive
	fi
done
[[ -n "$latest" ]] || fail "No $app archive found in $incoming."
printf 'Deploying %s\n' "${latest##*/}"

archive_name=${latest##*/}
release_dir=$(mktemp -d "$app_dir/releases/${archive_name%.tar.gz}.XXXXXX")
release_target="releases/${release_dir##*/}"
cleanup() {
	if [[ $(readlink "$app_dir/current") != "$release_target" ]]; then
		rm -r -- "$release_dir"
	fi
}
trap cleanup EXIT

cp -- "$latest" "$release_dir/.upload.tar.gz"
tar -xzf "$release_dir/.upload.tar.gz" --no-same-owner --no-same-permissions -C "$release_dir"
rm -- "$release_dir/.upload.tar.gz"
[[ -f "$release_dir/server.js" && -d "$release_dir/.next/static" ]] || fail "Archive does not contain the standalone Next.js server and static files."
# The web systemd unit reads web.env directly on startup.

# Replace the current symlink only after the new release is ready.
ln -s -- "$release_target" "$release_dir/.current"
mv -Tf -- "$release_dir/.current" "$app_dir/current"
printf 'Active files: %s/current\n' "$app_dir"
