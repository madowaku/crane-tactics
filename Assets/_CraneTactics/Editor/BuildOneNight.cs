using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace CraneTactics.Editor
{
    public static class BuildOneNight
    {
        [MenuItem("Crane Tactics/Create OneNight Scene")]
        public static void CreateScene()
        {
            PrepareArt();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("OneNight",typeof(OneNightGame));
            System.IO.Directory.CreateDirectory("Assets/_CraneTactics/Scenes");
            EditorSceneManager.SaveScene(scene,"Assets/_CraneTactics/Scenes/OneNight.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/_CraneTactics/Scenes/OneNight.unity",true)};
            AssetDatabase.SaveAssets();
            Debug.Log("CRANE_TACTICS_SCENE_CREATED");
        }
        public static void PrepareArt()
        {
            const string routePath="Assets/_CraneTactics/Resources/RouteGuide.mat";
            var routeMaterial=AssetDatabase.LoadAssetAtPath<Material>(routePath);
            if(routeMaterial==null) {routeMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(routeMaterial,routePath);}
            routeMaterial.color=new Color(.2f,.8f,.75f); EditorUtility.SetDirty(routeMaterial);
            string art="Assets/_CraneTactics/Art/";
            var probe=AssetDatabase.LoadAssetAtPath<GameObject>(art+"ScaleProbe.fbx");
            if(probe==null) return;
            var instance=Object.Instantiate(probe); var renderers=instance.GetComponentsInChildren<Renderer>(); var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Object.DestroyImmediate(instance);
            if(Vector3.Distance(bounds.size,Vector3.one)>.02f) throw new System.Exception("FBX meter probe failed: "+bounds.size);
            Debug.Log("CRANE_TACTICS_METER_PROBE_PASS "+bounds.size);
            var axis=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(art+"AxisProbe.fbx"));
            var positiveX=axis.GetComponentsInChildren<Transform>().First(t=>t.name=="PositiveX");
            var front=axis.GetComponentsInChildren<Transform>().First(t=>t.name=="FrontMinusY");
            Debug.Log("CRANE_TACTICS_AXIS_PROBE "+positiveX.position+" / "+front.position);
            if(Vector3.Distance(positiveX.position,Vector3.right)>.02f || Vector3.Distance(front.position,Vector3.back)>.02f) throw new System.Exception("FBX orientation contract failed");
            Object.DestroyImmediate(axis);
            System.IO.Directory.CreateDirectory("Assets/_CraneTactics/Resources/Toys");
            foreach(var name in new[]{"Knight","Heavy","Slimy","Scout","Guard","Cabinet","Claw"})
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(art+name+".fbx"); if(model==null) continue;
                var toy=Object.Instantiate(model); toy.name=name;
                foreach(var renderer in toy.GetComponentsInChildren<Renderer>())
                {
                    var originals=renderer.sharedMaterials; var converted=new Material[originals.Length];
                    for(int i=0;i<originals.Length;i++)
                    {
                        var source=originals[i]; string path="Assets/_CraneTactics/Resources/Toys/"+source.name+".mat";
                        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path);}
                        material.color=source.color; material.SetFloat("_Smoothness",.3f); if(source.name=="brass") material.SetFloat("_Metallic",.65f); EditorUtility.SetDirty(material);
                        converted[i]=material;
                    }
                    renderer.sharedMaterials=converted;
                }
                PrefabUtility.SaveAsPrefabAsset(toy,"Assets/_CraneTactics/Resources/Toys/"+name+".prefab"); Object.DestroyImmediate(toy);
            }
        }
        public static void BuildWindows()
        {
            CreateScene();
            PlayerSettings.companyName="madowaku";
            PlayerSettings.productName="CRANE TACTICS";
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/Windows/CraneTactics.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception(report.summary.result.ToString());
        }
    }
}
