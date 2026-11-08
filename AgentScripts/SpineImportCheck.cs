using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

public static class SpineImportCheck {
    [Serializable] public class Report {
        public bool success;
        public string[] generatedSkeletons;
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
    }
    public static async Task<string> Run() {
        const string folder = "Assets/SpineCompatibility/ImportProbe";
        if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Import probe folder already exists; refusing to overwrite it.");
        var report = new Report();
        Application.LogCallback collect = (message, stack, type) => {
            if (type == LogType.Warning) report.warnings.Add(message);
            if (type == LogType.Error || type == LogType.Exception) report.errors.Add(message);
        };
        Application.logMessageReceived += collect;
        try {
            AssetDatabase.CreateFolder("Assets/SpineCompatibility", "ImportProbe");
            var source = "Assets/Spine Examples/Spine Skeletons/Eyes";
            var paths = AssetDatabase.FindAssets("", new[] { source }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".json") || p.EndsWith(".atlas.txt") || p.EndsWith(".png")).ToArray();
            if (paths.Length < 3) throw new Exception("Expected JSON, atlas and texture fixture");
            AssetDatabase.StartAssetEditing();
            try {
                foreach (var path in paths)
                    if (!AssetDatabase.CopyAsset(path, folder + "/" + Path.GetFileName(path))) throw new Exception("Copy failed: " + path);
            } finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            await Task.Delay(500);
            report.generatedSkeletons = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            report.success = report.generatedSkeletons.Length > 0 && report.errors.Count == 0;
            foreach (var path in report.generatedSkeletons) {
                var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path).GetSkeletonData(false);
                if (data == null || data.Bones.Count == 0) report.success = false;
            }
        } catch (Exception e) { report.errors.Add(e.ToString()); report.success = false; }
        finally {
            Application.logMessageReceived -= collect;
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/import.json", JsonUtility.ToJson(report, true));
        return JsonUtility.ToJson(report);
    }
}
