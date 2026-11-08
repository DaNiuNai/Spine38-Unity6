using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine;
using Spine.Unity;
using Spine.Unity.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpineBakingCheck {
    [Serializable] public class Report {
        public int mecanimClips, bakedClips, prefabs, renderers;
        public bool success;
        public List<string> errors = new List<string>();
    }
    public static string Run() {
        const string folder="Assets/SpineCompatibility/BakingProbe";
        if(AssetDatabase.IsValidFolder(folder))throw new Exception("Probe folder already exists.");
        var original=SceneManager.GetActiveScene();
        var temporary=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporary);
        var report=new Report();
        Application.LogCallback collect=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception)report.errors.Add(message);};
        Application.logMessageReceived+=collect;
        try {
            AssetDatabase.CreateFolder("Assets/SpineCompatibility","BakingProbe");
            AssetDatabase.CreateFolder(folder,"Baked");
            var source=AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>("Assets/SpineCompatibility/Fixtures/Spine3875/Json/hero-pro_SkeletonData.asset");
            var copy=UnityEngine.Object.Instantiate(source);
            copy.controller=null;
            AssetDatabase.CreateAsset(copy,folder+"/Probe_SkeletonData.asset");
            SkeletonBaker.GenerateMecanimAnimationClips(copy);
            report.mecanimClips=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(copy.controller)).OfType<AnimationClip>().Count();
            var data=copy.GetSkeletonData(false);
            SkeletonBaker.BakeToPrefab(copy,data.Skins,folder+"/Baked",true,true);
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{folder+"/Baked"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            report.prefabs=prefabs.Length;
            report.renderers=prefabs.Sum(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Renderer>(true).Length);
            foreach(var path in AssetDatabase.FindAssets("t:AnimatorController",new[]{folder+"/Baked"}).Select(AssetDatabase.GUIDToAssetPath))
                report.bakedClips+=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count();
            report.success=report.errors.Count==0 && report.mecanimClips==data.Animations.Count
                && report.bakedClips==data.Animations.Count && report.prefabs>0 && report.renderers>0;
        } catch(Exception e){report.errors.Add(e.ToString());}
        finally {
            Application.logMessageReceived-=collect;
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(temporary,true);
            if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
        }
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/baking.json",JsonUtility.ToJson(report,true));
        return JsonUtility.ToJson(report);
    }
}
