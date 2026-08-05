using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.States;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>标题按钮动作表与反射缓存集中处；任一核心项缺失即 fail-open：放弃整帧接管，原版菜单与现有菜单 UI 不受影响</summary>
    internal static class HimayoMenuActions
    {
        /// <summary>核心反射是否齐备，决定 <see cref="HimayoMenuOverride.CanOverride"/></summary>
        public static bool Ready { get; private set; }

        /// <summary>主题切换落地反射是否可用；失败时切换钮降级，不影响标题接管</summary>
        public static bool ThemeSwitchReady { get; private set; }

        //InnoVault UIHandleLoader.MenuLoadDraw：驱动 Mod_MenuLoad 层（公告栏/反馈等），内部自管批次与 60tick 逻辑
        private static Action<SpriteBatch> driveMenuLoadUIs;
        //MenuLoader.OffsetModMenu：主题循环切换
        private static Action<int> offsetModMenu;
        //Main.PrepareLoadedModsAndConfigsForSingleplayer：单人入口的配置准备
        private static Action prepareSingleplayer;
        //Main.ClearVisualPostProcessEffects：标题帧屏幕后效清理，非核心，缺失则跳过
        private static Action clearVisualPostProcess;
        //Interface.pendingErrorMessages：tML 延迟错误弹窗队列（存档失败等），非核心
        private static FieldInfo pendingErrorMessagesField;
        //标题接管跳过 UpdateAndDrawModMenu，须自行落地 switchToMenu → currentMenu
        private static FieldInfo switchToMenuField;
        private static FieldInfo currentMenuField;
        private static FieldInfo lastSelectedModMenuField;
        private static FieldInfo menuLoadingField;

        private static bool initialized;
        private static bool themeSwitchFaultLogged;

        public static void Initialize(Mod mod) {
            if (initialized) {
                return;
            }
            initialized = true;
            try {
                driveMenuLoadUIs = typeof(UIHandleLoader)
                    .GetMethod("MenuLoadDraw", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.CreateDelegate<Action<SpriteBatch>>();
                offsetModMenu = typeof(MenuLoader)
                    .GetMethod("OffsetModMenu", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.CreateDelegate<Action<int>>();
                prepareSingleplayer = typeof(Main)
                    .GetMethod("PrepareLoadedModsAndConfigsForSingleplayer", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.CreateDelegate<Action>();
                clearVisualPostProcess = typeof(Main)
                    .GetMethod("ClearVisualPostProcessEffects", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.CreateDelegate<Action>();
                pendingErrorMessagesField = typeof(Main).Assembly
                    .GetType("Terraria.ModLoader.UI.Interface")
                    ?.GetField("pendingErrorMessages", BindingFlags.NonPublic | BindingFlags.Static);

                BindingFlags menuFlags = BindingFlags.NonPublic | BindingFlags.Static;
                switchToMenuField = typeof(MenuLoader).GetField("switchToMenu", menuFlags);
                currentMenuField = typeof(MenuLoader).GetField("currentMenu", menuFlags);
                lastSelectedModMenuField = typeof(MenuLoader).GetField("LastSelectedModMenu",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                menuLoadingField = typeof(MenuLoader).GetField("loading", menuFlags);
            } catch (Exception ex) {
                mod.Logger.Warn($"[HimayoMenu] 反射解析异常，夜樱主菜单退回原版: {ex}");
            }
            Ready = driveMenuLoadUIs != null && offsetModMenu != null && prepareSingleplayer != null;
            ThemeSwitchReady = offsetModMenu != null && switchToMenuField != null && currentMenuField != null;
            if (!Ready) {
                mod.Logger.Warn("[HimayoMenu] 核心反射项缺失，夜樱主菜单接管停用，原版菜单可正常使用");
            }
            else if (!ThemeSwitchReady) {
                mod.Logger.Warn("[HimayoMenu] 主题切换落地反射缺失，切换钮不可用，标题接管仍可用");
            }
        }

        /// <summary>tML 是否有待弹的延迟错误（存档失败等）；有则当帧放行原版，让原版弹出错误 UI</summary>
        public static bool HasPendingErrorMessages {
            get {
                if (pendingErrorMessagesField?.GetValue(null) is System.Collections.ICollection stack) {
                    return stack.Count > 0;
                }
                return false;
            }
        }

        /// <summary>接管标题帧时复刻原版 menuMode==0 分支的常态职责：联机与事件旗标复位、
        /// 待决选人回调清空、幽灵 UI 状态清空、屏幕后效清理</summary>
        public static void TitleHousekeeping() {
            Main.menuMultiplayer = false;
            Main.menuServer = false;
            Main.netMode = NetmodeID.SinglePlayer;
            Main.ServerSideCharacter = false;
            Terraria.GameContent.Events.DD2Event.Ongoing = false;
            Main.eclipse = false;
            Main.pumpkinMoon = false;
            Main.snowMoon = false;
            Main.ClearPendingPlayerSelectCallbacks();
            if (Main.MenuUI.CurrentState != null) {
                Main.MenuUI.SetState(null);
            }
            clearVisualPostProcess?.Invoke();
        }

        /// <summary>驱动 Mod_MenuLoad 层 UI（跳过原版 DrawMenu 后其 IL 注入点失效，由此补驱动）</summary>
        public static void DriveMenuOverlays(SpriteBatch spriteBatch) => driveMenuLoadUIs?.Invoke(spriteBatch);

        //====== 以下动作逐条复刻原版标题按钮（Main.DrawMenu menuMode==0 分支） ======

        public static void OpenSinglePlayer() {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.ClearPendingPlayerSelectCallbacks();
            //原版下一帧在 menuMode==1 分支调 OpenCharacterSelectUI 进入角色选择
            Main.menuMode = 1;
            prepareSingleplayer?.Invoke();
        }

        public static void OpenMultiplayer() {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = 12;
        }

        public static void OpenAchievements() {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = 888;
            Main.MenuUI.SetState(Main.AchievementsMenu);
        }

        public static void OpenWorkshop() {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = 888;
            UIWorkshopHub hub = new(null);
            hub.EnterHub();
            Main.MenuUI.SetState(hub);
        }

        public static void OpenSettings() {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = 11;
        }

        public static void OpenCredits() {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = 3000;
            SkyManager.Instance.Activate("CreditsRoll");
        }

        public static void ExitGame() {
            //公开包装：仅设置退出标志，等价原版退出按钮
            Main.WeGameRequireExitGame();
        }

        /// <summary>循环切换菜单主题，dir=±1；OffsetModMenu 只排队，须立刻落地（标题帧跳过了 UpdateAndDrawModMenu）</summary>
        public static void SwitchTheme(int dir) {
            if (!ThemeSwitchReady) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick);
            offsetModMenu.Invoke(dir);
            ApplyPendingMenuSwitch();
        }

        /// <summary>复刻 MenuLoader.UpdateAndDrawModMenuInner 的切换段：交换 currentMenu、触发 OnSelected、持久化</summary>
        private static void ApplyPendingMenuSwitch() {
            try {
                ModMenu pending = switchToMenuField.GetValue(null) as ModMenu;
                ModMenu current = currentMenuField.GetValue(null) as ModMenu;
                if (pending == null || pending == current) {
                    return;
                }

                current?.OnDeselected();
                currentMenuField.SetValue(null, pending);
                pending.OnSelected();
                switchToMenuField.SetValue(null, null);

                bool loading = menuLoadingField?.GetValue(null) as bool? ?? false;
                if (!loading && lastSelectedModMenuField != null && pending.FullName != (string)lastSelectedModMenuField.GetValue(null)) {
                    lastSelectedModMenuField.SetValue(null, pending.FullName);
                    Main.SaveSettings();
                }
            } catch (Exception ex) {
                ThemeSwitchReady = false;
                if (!themeSwitchFaultLogged) {
                    themeSwitchFaultLogged = true;
                    CWRMod.Instance?.Logger.Warn($"[HimayoMenu] 主题切换落地失败，切换钮停用: {ex}");
                }
            }
        }
    }
}
