#if DEBUG
using InnoVault.GameSystem;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DevTools.SoundProbe
{
    /// <summary>
    /// 音效探针（仅调试构建）：钩住 <see cref="SoundEngine.PlaySound(in SoundStyle, Vector2?, SoundUpdateCallback)"/>，
    /// 在进世界后的一段窗口里把每一次播放的音效路径、参数与调用栈写进 client.log。
    /// 专治「进世界莫名其妙响一声、翻源码翻不出来」这类问题：调用栈会直接点名是哪个模组哪一行。
    /// 同一「音效路径 + 调用点」在一个窗口内只记一次，循环音不会刷屏
    /// </summary>
    internal class SoundProbe : ICWRLoader
    {
        private delegate SlotId Orig_PlaySound(ref SoundStyle style, Vector2? position,
            SoundUpdateCallback callback);
        private delegate SlotId Hook_PlaySound(Orig_PlaySound orig, ref SoundStyle style,
            Vector2? position, SoundUpdateCallback callback);

        /// <summary>进世界自动监听的窗口帧数（3 秒）</summary>
        internal const int DefaultWindow = 180;

        /// <summary>调用栈里最多记几层有效帧</summary>
        private const int MaxStackDepth = 12;

        /// <summary>剩余监听帧，大于 0 才记录</summary>
        internal static int Remaining { get; private set; }

        /// <summary>窗口内已过帧数，用来标注「进世界第几帧响的」</summary>
        internal static int Elapsed { get; private set; }

        //同窗口内的去重键集合：音效路径 + 调用点
        private static readonly HashSet<string> logged = [];

        //这些命名空间只是转发层，写进日志纯属噪音
        private static readonly string[] noiseNamespaces = [
            "MonoMod", "System.", "ReLogic.", "Terraria.Audio.SoundEngine",
        ];

        void ICWRLoader.SetupData() {
            MethodInfo method = typeof(SoundEngine).GetMethod(nameof(SoundEngine.PlaySound),
                BindingFlags.Public | BindingFlags.Static, null,
                [typeof(SoundStyle).MakeByRefType(), typeof(Vector2?), typeof(SoundUpdateCallback)],
                null);
            if (method == null) {
                CWRMod.Instance.Logger.Warn("[SoundProbe] 未找到 SoundEngine.PlaySound 目标重载，探针未挂载");
                return;
            }
            VaultHook.Add(method, (Hook_PlaySound)OnPlaySound);
            CWRMod.Instance.Logger.Info("[SoundProbe] 探针已挂载");
        }

        void ICWRLoader.UnLoadData() => Disarm();

        /// <summary>开一段监听窗口</summary>
        internal static void Arm(int frames) {
            Remaining = Math.Max(1, frames);
            Elapsed = 0;
            logged.Clear();
            CWRMod.Instance.Logger.Info($"[SoundProbe] 开始监听 {Remaining} 帧");
        }

        internal static void Disarm() {
            Remaining = 0;
            Elapsed = 0;
            logged.Clear();
        }

        /// <summary>窗口计时，由 <see cref="SoundProbeSystem"/> 逐帧推进</summary>
        internal static void Tick() {
            if (Remaining <= 0) {
                return;
            }
            Remaining--;
            Elapsed++;
            if (Remaining == 0) {
                CWRMod.Instance.Logger.Info($"[SoundProbe] 监听结束，共记录 {logged.Count} 条不重复来源");
            }
        }

        private static SlotId OnPlaySound(Orig_PlaySound orig, ref SoundStyle style,
            Vector2? position, SoundUpdateCallback callback) {
            if (Remaining > 0 && !Main.dedServ) {
                try {
                    Record(in style, position);
                } catch (Exception ex) {
                    CWRMod.Instance.Logger.Warn($"[SoundProbe] 记录失败：{ex.Message}");
                }
            }
            return orig(ref style, position, callback);
        }

        private static void Record(in SoundStyle style, Vector2? position) {
            string stack = ResolveStack();
            string key = style.SoundPath + "|" + stack;
            if (!logged.Add(key)) {
                return;
            }
            CWRMod.Instance.Logger.Info(
                $"[SoundProbe] f{Elapsed} {style.SoundPath} vol={style.Volume:0.00} pitch={style.Pitch:0.00}"
                + $" loop={style.IsLooped} pos={(position.HasValue ? position.Value.ToString() : "null")}"
                + Environment.NewLine + stack);
        }

        /// <summary>抓调用栈并剔掉转发层，只留能定位到源码的那几层</summary>
        private static string ResolveStack() {
            StackTrace trace = new(2, false);
            StringBuilder builder = new();
            int depth = 0;
            for (int i = 0; i < trace.FrameCount && depth < MaxStackDepth; i++) {
                MethodBase frameMethod = trace.GetFrame(i)?.GetMethod();
                Type declaring = frameMethod?.DeclaringType;
                if (frameMethod == null || declaring == null) {
                    continue;
                }
                string full = declaring.FullName + "." + frameMethod.Name;
                bool noise = false;
                foreach (string prefix in noiseNamespaces) {
                    if (full.StartsWith(prefix, StringComparison.Ordinal)) {
                        noise = true;
                        break;
                    }
                }
                if (noise) {
                    continue;
                }
                builder.Append("    at ").AppendLine(full);
                depth++;
            }
            return builder.Length > 0 ? builder.ToString().TrimEnd() : "    (调用栈为空)";
        }
    }

    /// <summary>进世界自动开窗，并逐帧推进窗口计时</summary>
    internal class SoundProbeSystem : ModSystem
    {
        public override void OnWorldLoad() {
            if (!Main.dedServ) {
                SoundProbe.Arm(SoundProbe.DefaultWindow);
            }
        }

        public override void OnWorldUnload() => SoundProbe.Disarm();

        public override void PostUpdateEverything() => SoundProbe.Tick();
    }
}
#endif
