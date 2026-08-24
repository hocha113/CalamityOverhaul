using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria.Localization;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>角色在码头上的场景快照，供环境粒子钩子读取</summary>
    internal struct MenuCharacterScene
    {
        public Vector2 ChipCenter;
        public Vector2 ChipSize;
        public float ChipAlpha;
        public Rectangle PortraitRect;
        public float PortraitAlpha;
        public bool PortraitVisible;
    }

    /// <summary>主菜单角色定义，子类由 <see cref="MenuCharacterRegistry"/> 反射自动注册</summary>
    internal abstract class MenuCharacter
    {
        /// <summary>存档键与本地化键</summary>
        public abstract string Key { get; }

        /// <summary>码头排序，小者在左</summary>
        public virtual int SortOrder => 0;

        /// <summary>解锁谓词，false 时芯片不出现</summary>
        public abstract bool Unlocked { get; }

        /// <summary>芯片帧组，多帧时经 <see cref="GetChipFrame"/> 播动画</summary>
        public abstract IList<Texture2D> ChipFrames { get; }

        /// <summary>芯片缩放，取干净除数保像素清晰</summary>
        public virtual float ChipScale => 0.5f;

        /// <summary>立绘表情组，空或 null 表示暂无立绘</summary>
        public abstract IList<Texture2D> Expressions { get; }

        //主题色，暗端/亮端/近黑底
        public abstract Color AccentDark { get; }
        public abstract Color AccentBright { get; }
        public abstract Color BaseShade { get; }

        /// <summary>本地化名缺省文本</summary>
        public abstract string FallbackName { get; }

        /// <summary>由 <see cref="CharacterDockUI.SetStaticDefaults"/> 绑定</summary>
        public LocalizedText DisplayName { get; internal set; }

        public bool ChipReady {
            get {
                IList<Texture2D> frames = ChipFrames;
                return frames is { Count: > 0 } && frames[0] != null && !frames[0].IsDisposed;
            }
        }

        public bool HasPortrait {
            get {
                IList<Texture2D> list = Expressions;
                return list is { Count: > 0 } && list[0] != null && !list[0].IsDisposed;
            }
        }

        /// <summary>当前芯片帧序号，timeSeconds 为墙钟累计秒</summary>
        public virtual int GetChipFrame(float timeSeconds) => 0;

        /// <summary>固定 60tick 推进环境粒子，仅客户端菜单</summary>
        public virtual void UpdateAmbient(in MenuCharacterScene scene) { }

        /// <summary>芯片与立绘底下的环境层绘制</summary>
        public virtual void DrawAmbient(SpriteBatch sb, in MenuCharacterScene scene) { }

        /// <summary>卸载时清空运行期状态</summary>
        public virtual void ClearRuntime() { }
    }

    /// <summary>角色定义目录，Mod.Load 反射注册，键冲突注册期报错</summary>
    internal sealed class MenuCharacterRegistry : ICWRLoader
    {
        private static readonly List<MenuCharacter> all = [];
        private static readonly Dictionary<string, MenuCharacter> byKey = [];

        /// <summary>全部定义，SortOrder 再 Key</summary>
        public static IReadOnlyList<MenuCharacter> All => all;

        public static bool HasAny => all.Count > 0;

        public static bool TryGet(string key, out MenuCharacter definition) {
            if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out definition)) {
                return true;
            }
            definition = null;
            return false;
        }

        void ICWRLoader.LoadData() {
            List<MenuCharacter> found = VaultUtils.GetDerivedInstances<MenuCharacter>();
            found.Sort((a, b) => {
                int order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (MenuCharacter definition in found) {
                if (string.IsNullOrWhiteSpace(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[MenuCharacterRegistry] {definition.GetType().FullName} has an empty Key, skipped");
                    continue;
                }
                if (byKey.ContainsKey(definition.Key)) {
                    CWRMod.Instance.Logger.Error($"[MenuCharacterRegistry] duplicate Key '{definition.Key}' from {definition.GetType().FullName}, skipped");
                    continue;
                }
                all.Add(definition);
                byKey[definition.Key] = definition;
            }
        }

        void ICWRLoader.UnLoadData() {
            foreach (MenuCharacter definition in all) {
                definition.ClearRuntime();
                definition.DisplayName = null;
            }
            all.Clear();
            byKey.Clear();
        }
    }
}
