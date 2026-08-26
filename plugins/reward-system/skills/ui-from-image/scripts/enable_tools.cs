// Republish Feeder MCP tools that the server is not exposing.
// Run with script-execute, isMethodBody: false, className "EnableMcpTools", methodName "Main".
//
// Symptom this fixes: a tool listed under .claude/skills/ returns FMP_TOOL_NOT_FOUND,
// and tools/list is missing it. IsToolEnabled may already report true — the tool list
// only republishes after UnityMcpPluginEditor.Instance.Save(true), which is what this does.
// After running, re-issue tools/list; the count should go up.
//
// Reflection is used so this compiles without referencing the plugin assembly.

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public class EnableMcpTools
{
    // Everything a UI-from-image build wants available.
    static readonly string[] Want =
    {
        "script-execute",
        "script-read",
        "script-update-or-create",
        "assets-refresh",
        "console-get-logs",
        "console-clear-logs",
        "editor-application-get-state",
        "editor-application-set-state",
        "screenshot-game-view",
        "scene-list-opened",
        "tool-set-enabled-state",
    };

    public static void Main()
    {
        Type plugin = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.Name == "UnityMcpPluginEditor");
        if (plugin == null) { Debug.LogError("UnityMcpPluginEditor not found"); return; }

        object instance = plugin.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (instance == null) { Debug.LogError("plugin Instance is null"); return; }

        object tm = instance.GetType().GetProperty("Tools")?.GetValue(instance);
        if (tm == null) { Debug.LogError("Tools manager is null"); return; }

        MethodInfo isEnabled = tm.GetType().GetMethod("IsToolEnabled");
        MethodInfo setEnabled = tm.GetType().GetMethod("SetToolEnabled");

        StringBuilder sb = new StringBuilder();
        foreach (string n in Want)
        {
            bool before = (bool)isEnabled.Invoke(tm, new object[] { n });
            if (!before) setEnabled.Invoke(tm, new object[] { n, true });
            sb.AppendLine(n + ": " + before + " -> " + (bool)isEnabled.Invoke(tm, new object[] { n }));
        }

        // Save takes (bool captureCurrentToolStates) — this is the call that republishes.
        instance.GetType().GetMethod("Save")?.Invoke(instance, new object[] { true });

        Debug.Log("MCP TOOLS REPUBLISHED\n" + sb);
    }
}
