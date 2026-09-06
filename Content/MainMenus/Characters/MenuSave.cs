using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>单个角色的菜单持久状态</summary>
    internal sealed class CharacterMenuState
    {
        public int Expression;
        public Vector2 Offset = Vector2.Zero;
        public int ZoomStep = CharacterDockUI.DefaultZoomStep;
        public bool Show;

        public TagCompound ToTag() => new() {
            ["Expression"] = Expression,
            ["Offset"] = Offset,
            ["ZoomStep"] = ZoomStep,
            ["Show"] = Show
        };

        public static CharacterMenuState FromTag(TagCompound tag) {
            CharacterMenuState state = new();
            if (tag.TrySafeGet("Expression", out int expression, nameof(CharacterMenuState))) {
                state.Expression = expression;
            }
            if (tag.TrySafeGet("Offset", out Vector2 offset, nameof(CharacterMenuState))
                && float.IsFinite(offset.X) && float.IsFinite(offset.Y)) {
                state.Offset = offset;
            }
            if (tag.TrySafeGet("ZoomStep", out int zoomStep, nameof(CharacterMenuState))) {
                state.ZoomStep = zoomStep;
            }
            if (tag.TrySafeGet("Show", out bool show, nameof(CharacterMenuState))) {
                state.Show = show;
            }
            state.ZoomStep = Math.Clamp(state.ZoomStep, CharacterDockUI.MinZoomStep, CharacterDockUI.MaxZoomStep);
            return state;
        }
    }

    /// <summary>主菜单立绘存档，按角色 Key 存通用状态</summary>
    internal class MenuSave : SaveMod
    {
        private const int CurrentDataVersion = 2;

        /// <summary>解锁"永恒燃烧的现在"结局立绘</summary>
        public static bool ADV_SupCal_EBN { get; private set; }

        private static readonly Dictionary<string, CharacterMenuState> states = [];

        /// <summary>取角色状态，缺省即建</summary>
        public static CharacterMenuState GetState(string key) {
            if (!states.TryGetValue(key, out CharacterMenuState state)) {
                state = new CharacterMenuState();
                states[key] = state;
            }
            return state;
        }

        /// <summary>立即落盘，防抖由调用方持有</summary>
        public static void SaveNow() => CWRSaveData.Guard(nameof(MenuSave) + ".SaveNow", () => DoSave<MenuSave>());

        //SaveMod 的 DoSave/DoLoad 在 InnoVault 侧不兜底，这里跑在模组加载期：
        //一份坏掉的菜单存档不该让整个模组加载失败，出错就按无存档处理
        public override void SetStaticDefaults() {
            CWRSaveData.Guard(nameof(MenuSave) + ".SetStaticDefaults", () => {
                if (!HasSave) {
                    DoSave<MenuSave>();
                }
                DoLoad<MenuSave>();
            });
        }

        public override void SaveData(TagCompound tag) {
            tag["DataVersion"] = CurrentDataVersion;
            tag["ADV_SupCal_EBN"] = ADV_SupCal_EBN;

            TagCompound characters = [];
            foreach (KeyValuePair<string, CharacterMenuState> pair in states) {
                characters[pair.Key] = pair.Value.ToTag();
            }
            tag["Characters"] = characters;
        }

        public override void LoadData(TagCompound tag) {
            states.Clear();

            if (!tag.TrySafeGet("DataVersion", out int dataVersion, nameof(MenuSave))) {
                dataVersion = 0;//旧档无版本戳
            }

            if (!tag.TrySafeGet("ADV_SupCal_EBN", out bool unlocked, nameof(MenuSave))) {
                unlocked = false;
            }
            ADV_SupCal_EBN = unlocked;

            if (dataVersion < 2) {
                MigrateLegacy(tag);
                return;
            }

            if (tag.TrySafeGet("Characters", out TagCompound characters, nameof(MenuSave)) && characters != null) {
                foreach (KeyValuePair<string, object> pair in characters) {
                    if (pair.Value is TagCompound sub) {
                        states[pair.Key] = CharacterMenuState.FromTag(sub);
                    }
                }
            }
        }

        /// <summary>v1 旧键折算，偏移基于已死布局不迁</summary>
        private static void MigrateLegacy(TagCompound tag) {
            CharacterMenuState supCal = GetState("SupCal");
            if (tag.TrySafeGet("SupCal_Expression", out int expression, nameof(MenuSave))) {
                supCal.Expression = Math.Clamp(expression, 0, 2);
            }
            if (tag.TrySafeGet("SupCal_ShowFullPortrait", out bool show, nameof(MenuSave))) {
                supCal.Show = show;
            }
            //旧值绘制时另乘 1.6，先折回实际观感再取整数档
            if (tag.TrySafeGet("SupCal_LeftPortraitScale", out float scale, nameof(MenuSave)) && float.IsFinite(scale)) {
                supCal.ZoomStep = Math.Clamp((int)Math.Round(scale * 1.6f),
                    CharacterDockUI.MinZoomStep, CharacterDockUI.MaxZoomStep);
            }
        }

        /// <summary>达成结局时解锁主菜单立绘</summary>
        public static void UnlockEternalBlazingNowPortrait(Player player) {
            if (!ADV_SupCal_EBN) {
                ADV_SupCal_EBN = true;
                SaveNow();
            }
        }

        public static bool IsPortraitUnlocked() => ADV_SupCal_EBN;
    }
}
