// 关卡 4 实机预览：进入新关卡跑一小段，截战场图。
// 运行：ST.exe -shotlevel 3

using System.Collections;
using System.IO;
using UnityEngine;

namespace StarLane
{
    public class STLevelShot : MonoBehaviour
    {
        private int _level = -1;

        private void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-shotlevel" && int.TryParse(args[i + 1], out var v)) _level = v;
            }
        }

        private IEnumerator Start()
        {
            if (_level < 0) yield break;
            yield return new WaitForSecondsRealtime(0.6f);

            var main = FindAnyObjectByType<STMain>();
            if (main == null) yield break;

            main.StartLevel(_level);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot($"L{_level + 1}-start");

            for (int i = 0; i < 5; i++)
            {
                main.Run.Gold += 80f;
                main.Hud.UpdateHud(main.Run, main.Speed);
                main.OnSummon();
                yield return new WaitForSecondsRealtime(0.1f);
            }
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"L{_level + 1}-summon");

            main.Bot(45f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot($"L{_level + 1}-mid");

            Debug.Log($"LEVELSHOT done level={_level} waves={main.Run.WaveIndex} " +
                      $"enemies={main.Run.Enemies.Count} towers={main.Run.Towers.Count}");
            Application.Quit();
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var dir = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "..", "Shots"));
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }
}
