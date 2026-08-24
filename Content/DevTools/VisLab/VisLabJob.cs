#if DEBUG
using InnoVault.PRT;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DevTools.VisLab
{
    /// <summary>
    /// 游戏内快照 job 模型。与离线沙盒共用 .vissandbox\jobs 目录,
    /// 靠 kind 字段区分(离线 job 有 effect 字段、无 kind)
    /// </summary>
    internal sealed class VisLabJob
    {
        /// <summary>proj / prt / ui</summary>
        public string Kind { get; set; }
        /// <summary>"Mod内部名/类名";裸类名默认 CalamityOverhaul</summary>
        public string Type { get; set; }
        public float[] Ai { get; set; }
        public float[] Velocity { get; set; }
        /// <summary>出生点相对玩家中心的偏移,缺省 [0,-180]</summary>
        public float[] Offset { get; set; }
        public int Damage { get; set; } = 100;
        public float Knockback { get; set; } = 2f;
        /// <summary>prt:粒子数</summary>
        public int Count { get; set; } = 10;
        /// <summary>prt:初速随机散布幅度(px/tick)</summary>
        public float Spread { get; set; } = 2f;
        /// <summary>prt:颜色 RGBA 0~255</summary>
        public float[] Color { get; set; }
        public float Scale { get; set; } = 1f;
        /// <summary>抓几帧</summary>
        public int Frames { get; set; } = 8;
        /// <summary>每几 tick 抓一帧</summary>
        public int Interval { get; set; } = 5;
        /// <summary>spawn 前的场景准备帧数(末帧抓基线)</summary>
        public int Warmup { get; set; } = 15;
        /// <summary>裁剪框相对实体包围盒的外扩像素</summary>
        public int Margin { get; set; } = 240;
        /// <summary>抓帧期隐藏HUD;ui 类 job 强制不隐藏</summary>
        public bool HideUI { get; set; } = true;
        /// <summary>抓帧区每 tick 补满环境光(洞穴/夜晚用)</summary>
        public bool FloodLight { get; set; }
        /// <summary>裁剪框跟随目标弹幕;要测 STATIC 红旗时建议关掉</summary>
        public bool Follow { get; set; } = true;
        public bool LockPlayer { get; set; } = true;
        public bool GodMode { get; set; } = true;
        /// <summary>ui:开启后反射注入的字段/属性(mock 状态)</summary>
        public Dictionary<string, JsonElement> Fields { get; set; }

        private static readonly JsonSerializerOptions options = new() {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static VisLabJob Load(string path) => JsonSerializer.Deserialize<VisLabJob>(File.ReadAllText(path), options);

        public (string modName, string typeName) SplitType() {
            int slash = Type?.IndexOf('/') ?? -1;
            return slash < 0 ? ("CalamityOverhaul", Type) : (Type[..slash], Type[(slash + 1)..]);
        }

        /// <summary>解析弹幕类型 ID,失败返回 -1</summary>
        public int ResolveProjType(out string error) {
            (string modName, string typeName) = SplitType();
            if (ModContent.TryFind(modName, typeName, out ModProjectile mp)) {
                error = null;
                return mp.Type;
            }
            error = $"找不到弹幕 {modName}/{typeName}";
            return -1;
        }

        /// <summary>解析 PRT 粒子 ID,失败返回 -1(按类名匹配,同名多命中时优先指定 mod 的程序集)</summary>
        public int ResolvePrtID(out string error) {
            (string modName, string typeName) = SplitType();
            Type match = null;
            foreach (Type t in PRTLoader.PRT_TypeToID.Keys) {
                if (t.Name != typeName) {
                    continue;
                }
                if (match == null) {
                    match = t;
                    continue;
                }
                //多命中:优先程序集名与 mod 名一致者
                if (t.Assembly.GetName().Name == modName) {
                    match = t;
                }
            }
            if (match == null) {
                error = $"找不到 PRT {typeName}";
                return -1;
            }
            error = null;
            return PRTLoader.GetParticleID(match);
        }

        /// <summary>解析 UIHandle 实例,失败返回 null</summary>
        public UIHandle ResolveUI(out string error) {
            (string modName, string typeName) = SplitType();
            string fullName = modName + "/" + typeName;
            foreach (UIHandle handle in UIHandleLoader.UIHandles) {
                if (handle.FullName == fullName || handle.Name == typeName) {
                    error = null;
                    return handle;
                }
            }
            error = $"找不到 UIHandle {fullName}";
            return null;
        }

        public Vector2 OffsetVec() => Offset is { Length: >= 2 } ? new Vector2(Offset[0], Offset[1]) : new Vector2(0, -180);
        public Vector2 VelocityVec() => Velocity is { Length: >= 2 } ? new Vector2(Velocity[0], Velocity[1]) : Vector2.Zero;
        public Color ColorValue() => Color is { Length: >= 3 }
            ? new Color((int)Color[0], (int)Color[1], (int)Color[2], Color.Length > 3 ? (int)Color[3] : 255)
            : Microsoft.Xna.Framework.Color.White;
    }
}
#endif
