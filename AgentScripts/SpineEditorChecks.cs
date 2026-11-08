using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpineEditorChecks {
    [Serializable] public class Report { public int initialized; public List<string> errors=new List<string>(); }
    public static string Run() {
        var report=new Report();
        Application.LogCallback collect=(message,stack,type)=>{ if(type==LogType.Error||type==LogType.Exception)report.errors.Add(message); };
        Application.logMessageReceived+=collect;
        try {
            foreach(var query in new[]{"t:SkeletonDataAsset","t:SpineAtlasAsset","t:AnimationReferenceAsset"}) {
                foreach(var path in AssetDatabase.FindAssets(query).Select(AssetDatabase.GUIDToAssetPath)) {
                    Editor editor=null;
                    try {
                        editor=Editor.CreateEditor(AssetDatabase.LoadMainAssetAtPath(path));
                        if(editor==null)throw new Exception("Editor not created");
                        editor.HasPreviewGUI();
                        ++report.initialized;
                    } catch(Exception e){report.errors.Add(path+": "+e);}
                    finally{if(editor!=null)UnityEngine.Object.DestroyImmediate(editor);}
                }
            }
        } finally{Application.logMessageReceived-=collect;}
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/editor-inspectors.json",JsonUtility.ToJson(report,true));
        return JsonUtility.ToJson(report);
    }
}
