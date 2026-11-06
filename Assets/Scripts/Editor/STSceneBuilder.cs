// 星轨防线 · 场景与工程设置生成器
//
// 通过批处理调用一次即可生成：
//   Assets/Scenes/Main.unity      只含一个 App 根节点（相机与 UI 全部运行时构建）
//   ProjectSettings               竖屏 540×960、公司/产品名、启动场景
//
// 之所以用代码建场景：所有视图都是运行时按数据构建的（与 Godot 版一致），
// 场景里只保留入口节点，避免出现手拖引用导致的断链。

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StarLane.EditorTools
{
    public static class STSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void BuildEverything()
        {
            ApplyProjectSettings();
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[STSceneBuilder] 完成");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = "StarLane Studio";
            PlayerSettings.productName = "星轨防线 STARLANE BULWARK";
            PlayerSettings.bundleVersion = "1.0.0";

            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            // 只允许竖屏
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // 图标：使用 API Mart 生成的应用图标
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/Backgrounds/icon-256.png");
            if (icon != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            }
        }

        public static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var app = new GameObject("App");
            app.AddComponent<STMain>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }
    }
}
