// 星轨防线 · 批处理入口（对应 AGENTS.md 里的 Unity CLI 命令）
//
// 提供三个 executeMethod 入口：
//   StarLane.EditorTools.STSetup.RunSimTests    逻辑自检
//   StarLane.EditorTools.STBuild.VerifyResources 资源完整性校验
//   StarLane.EditorTools.STBuild.BuildPlayer     打包 Windows 播放器

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StarLane.EditorTools
{
    public static class STSetup
    {
        /// <summary>逻辑自检：写 tests.log，失败退出码 1。</summary>
        public static void RunSimTests()
        {
            int code = 1;
            try
            {
                code = STSimTests.Run();
            }
            catch (Exception e)
            {
                Debug.LogError("[STSimTests] 异常: " + e);
            }
            WriteLog("tests.log", STSimTests.Lines);
            Finish(code);
        }

        internal static void WriteLog(string fileName, IEnumerable<string> lines)
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, fileName);
            try
            {
                File.WriteAllLines(path, lines);
                Debug.Log("[ST] 已写入 " + path);
            }
            catch (Exception e)
            {
                Debug.LogError("[ST] 写日志失败: " + e.Message);
            }
        }

        internal static void Finish(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
