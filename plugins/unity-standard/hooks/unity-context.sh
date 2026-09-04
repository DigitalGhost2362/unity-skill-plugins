#!/bin/sh
# SessionStart hook: print the standard only when this is a Unity project.
# stdout of a SessionStart hook is added to Claude's context, so anything printed
# here is in effect for the whole session. Printing nothing costs nothing.

dir="${CLAUDE_PROJECT_DIR:-$PWD}"

# ProjectVersion.txt exists from the moment the Editor creates a project, so a
# brand-new project is covered too. No other marker is as reliable: Assets/ and
# .csproj files also appear in non-Unity trees.
[ -f "$dir/ProjectSettings/ProjectVersion.txt" ] || exit 0

cat "$CLAUDE_PLUGIN_ROOT/hooks/unity-always.md"
