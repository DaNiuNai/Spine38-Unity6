using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class SpineValidationSetup {
    public static string Create2D() {
        EnsureFolder("Assets", "SpineCompatibility");
        EnsureFolder("Assets/SpineCompatibility", "Validation");
        var rendererPath = "Assets/SpineCompatibility/Validation/URP2DRenderer.asset";
        var pipelinePath = "Assets/SpineCompatibility/Validation/URP2D.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
        if (renderer == null) {
            renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null) {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        AssetDatabase.SaveAssets();
        return AssetDatabase.GetAssetPath(pipeline);
    }
    static void EnsureFolder(string parent, string name) {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
