using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SpineBuiltinValidation {
    const string StatePath = ".tmp/spine-builtin-restore.json";
    [Serializable] public class MaterialState { public string path, shader; public int queue; public string[] keywords; }
    [Serializable] public class State {
        public string graphics;
        public string[] quality;
        public int level;
        public ColorSpace colorSpace;
        public MaterialState[] materials;
    }
    public static string Enable() {
        if (File.Exists(StatePath)) throw new InvalidOperationException("Restore the existing pipeline snapshot first.");
        var state = new State {
            graphics = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline),
            level = QualitySettings.GetQualityLevel(), colorSpace = PlayerSettings.colorSpace,
            quality = new string[QualitySettings.names.Length],
            materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/SpineCompatibility/Fixtures/Spine3875" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => { var m=AssetDatabase.LoadAssetAtPath<Material>(p); var rawQueue=new SerializedObject(m).FindProperty("m_CustomRenderQueue"); return new MaterialState {path=p,shader=m.shader.name,queue=rawQueue==null?m.renderQueue:rawQueue.intValue,keywords=m.shaderKeywords}; }).ToArray()
        };
        for (int i = 0; i < state.quality.Length; ++i) {
            QualitySettings.SetQualityLevel(i, false);
            state.quality[i] = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
        }
        QualitySettings.SetQualityLevel(state.level, false);
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
        File.WriteAllText(StatePath, Newtonsoft.Json.JsonConvert.SerializeObject(state, Newtonsoft.Json.Formatting.Indented));
        for (int i = 0; i < state.quality.Length; ++i) {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = null;
        }
        QualitySettings.SetQualityLevel(state.level, false);
        GraphicsSettings.defaultRenderPipeline = null;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        foreach (var entry in state.materials) {
            var material = AssetDatabase.LoadAssetAtPath<Material>(entry.path);
            material.shader = Shader.Find("Spine/Skeleton");
            material.shaderKeywords = entry.keywords;
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        return "Built-in/Gamma enabled for original PMA example validation; snapshot saved.";
    }
    public static string Restore() {
        var state = Newtonsoft.Json.JsonConvert.DeserializeObject<State>(File.ReadAllText(StatePath));
        if (state.materials == null) throw new InvalidOperationException("Pipeline snapshot has no material records.");
        for(int i=0;i<state.quality.Length;++i) {
            QualitySettings.SetQualityLevel(i,false);
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(state.quality[i]);
        }
        QualitySettings.SetQualityLevel(state.level,false);
        GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(state.graphics);
        PlayerSettings.colorSpace = state.colorSpace;
        foreach(var entry in state.materials) {
            var material = AssetDatabase.LoadAssetAtPath<Material>(entry.path);
            material.shader=Shader.Find(entry.shader); material.renderQueue=entry.queue; material.shaderKeywords=entry.keywords;
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        File.Delete(StatePath);
        return "Original Graphics/Quality/color-space/material settings restored.";
    }
}
