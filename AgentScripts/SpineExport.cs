using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public static class SpineExport {
    public static async Task<string> Run() {
        const string output = "Artifacts/SpineCompatibility/Packages";
        Directory.CreateDirectory(output);
        var files = new List<string>();
        var sdk = output + "/spine-unity-3.8-unity6000.3.unitypackage";
        AssetDatabase.ExportPackage(new[] { "Assets/Spine", "Assets/Spine Examples" }, sdk, ExportPackageOptions.Recurse);
        files.Add(Path.GetFullPath(sdk));
        foreach (var package in new[] { "com.esotericsoftware.spine.urp-shaders", "com.esotericsoftware.spine.timeline" }) {
            var request = Client.Pack(Path.GetFullPath("SpineExtensions/" + package), Path.GetFullPath(output));
            var started = DateTime.UtcNow;
            while (!request.IsCompleted) {
                if ((DateTime.UtcNow - started).TotalSeconds > 60) throw new TimeoutException("UPM pack timed out: " + package);
                await Task.Delay(50);
            }
            if (request.Status != StatusCode.Success) throw new Exception(request.Error.message);
            files.Add(request.Result.tarballPath);
        }
        return string.Join("\n", files);
    }
}
