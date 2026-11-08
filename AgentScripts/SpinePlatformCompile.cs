using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

public static class SpinePlatformCompile {
    [Serializable] public class Report {
        public string target, unityVersion;
        public bool success;
        public string[] spineAssemblies;
        public List<string> errors = new List<string>();
    }

    public static string Compile(string targetName) {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Cannot compile during Play Mode.");
        var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetName);
        var report = new Report { target = targetName, unityVersion = Application.unityVersion };
        var output = "Artifacts/SpineCompatibility/PlayerScripts/" + targetName;
        Directory.CreateDirectory(output);
        Application.LogCallback collect = (message, stack, type) => {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message);
        };
        Application.logMessageReceived += collect;
        try {
            var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings {
                target = target, group = BuildPipeline.GetBuildTargetGroup(target),
                options = ScriptCompilationOptions.None
            }, output);
            report.spineAssemblies = result.assemblies == null ? new string[0]
                : result.assemblies.Where(x => Path.GetFileName(x).StartsWith("spine", StringComparison.OrdinalIgnoreCase)).ToArray();
            report.success = report.errors.Count == 0 && report.spineAssemblies.Any(x => Path.GetFileName(x) == "spine-unity.dll");
        } catch (Exception e) { report.errors.Add(e.ToString()); }
        finally { Application.logMessageReceived -= collect; }
        File.WriteAllText("Artifacts/SpineCompatibility/compile-" + targetName + ".json", JsonUtility.ToJson(report, true));
        return JsonUtility.ToJson(report);
    }
}
