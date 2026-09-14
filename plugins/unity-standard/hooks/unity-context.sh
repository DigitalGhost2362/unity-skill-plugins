#!/bin/sh
# SessionStart hook: install the skills as user skills, then print the standard
# only when this is a Unity project.
# stdout of a SessionStart hook is added to Claude's context, so anything printed
# here is in effect for the whole session. Printing nothing costs nothing.

# Plugin skills are always named <plugin>:<skill>, and Poracode's slash menu only
# prefix-matches that full name, so /brai never finds unity-standard:brainstorm.
# User skills keep their bare name. The skills live in user-skills/ (not skills/)
# so Claude does not also load a namespaced copy. A copy carries a .nbg-unity
# marker holding the plugin root it came from; a folder without it is never touched.
src="$CLAUDE_PLUGIN_ROOT/user-skills"
dest="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/skills"
command -v cygpath >/dev/null 2>&1 && dest=$(cygpath -u "$dest")

if [ -d "$src" ] && mkdir -p "$dest"; then
  for skill in "$src"/*/; do
    name=$(basename "$skill")
    target="$dest/$name"
    if [ -e "$target" ] && [ ! -f "$target/.nbg-unity" ]; then
      echo "nbg-unity: $target exists and is not managed by this plugin; left untouched" >&2
      continue
    fi
    [ "$(cat "$target/.nbg-unity" 2>/dev/null)" = "$CLAUDE_PLUGIN_ROOT" ] && continue
    rm -rf "$target"
    cp -R "$skill" "$target" && printf '%s' "$CLAUDE_PLUGIN_ROOT" > "$target/.nbg-unity"
  done
  for marker in "$dest"/*/.nbg-unity; do
    [ -f "$marker" ] || continue
    owned=$(dirname "$marker")
    [ -d "$src/$(basename "$owned")" ] || rm -rf "$owned"
  done
fi

dir="${CLAUDE_PROJECT_DIR:-$PWD}"

# ProjectVersion.txt exists from the moment the Editor creates a project, so a
# brand-new project is covered too. No other marker is as reliable: Assets/ and
# .csproj files also appear in non-Unity trees.
[ -f "$dir/ProjectSettings/ProjectVersion.txt" ] || exit 0

cat "$CLAUDE_PLUGIN_ROOT/hooks/unity-always.md"
