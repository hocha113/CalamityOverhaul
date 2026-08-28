using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace CalamityOverhaul.Content.GameModes.UI
{
    /// <summary>
    /// 世界菜单的模式呈现。两个面：<br/>
    /// · 创建界面难度行扩为六格（旅程/经典/专家/大师/残酷世界/修罗地狱），
    ///   残酷与修罗是大师为底的预设，选中即记待生效旗标，世界生成完毕由
    ///   <see cref="GameModeSystem.PostWorldGen"/> 落地；<br/>
    /// · 世界选择列表按 .twld 头部旗标把难度文字换成模式文艺名与专属色板，
    ///   修罗在 FTW/天顶世界呈毁灭脸（与局内 <see cref="GameModeSystem.FaceOf"/> 同规）。<br/>
    /// 原版难度枚举是私有嵌套类型，用越界值 4/5 构造同型按钮混入同组，
    /// 互斥选中/悬停说明/手柄导航全部复用原版逻辑；
    /// 反射面（枚举与两个私有字段）任一缺位则全部钩子静默走原版，上游改名不炸
    /// </summary>
    internal sealed class GameModeWorldMenu : ModSystem
    {
        /// <summary>残酷在原版难度枚举里的越界值（0-3 为原版四难度）</summary>
        private const int BrutalOption = 4;
        /// <summary>修罗在原版难度枚举里的越界值</summary>
        private const int AsuraOption = 5;
        /// <summary>六格统一文字缩放：容一行六格里最长的「残酷世界/Cruel World」不溢出</summary>
        private const float TitleSize = 0.9f;

        private static Type difficultyEnumType;
        private static FieldInfo optionDifficultyField;
        private static FieldInfo difficultyButtonsField;

        /// <summary>反射面完好，创建界面钩子才接管</summary>
        private static bool Ready => difficultyEnumType != null
            && optionDifficultyField != null && difficultyButtonsField != null;

        public override void Load() {
            if (Main.dedServ) {
                return;   //纯菜单表现，服务端无 UI
            }
            difficultyEnumType = typeof(UIWorldCreation)
                .GetNestedType("WorldDifficultyId", BindingFlags.NonPublic);
            optionDifficultyField = typeof(UIWorldCreation)
                .GetField("_optionDifficulty", BindingFlags.NonPublic | BindingFlags.Instance);
            difficultyButtonsField = typeof(UIWorldCreation)
                .GetField("_difficultyButtons", BindingFlags.NonPublic | BindingFlags.Instance);

            On_UIWorldCreation.AddWorldDifficultyOptions += HookAddDifficultyOptions;
            On_UIWorldCreation.FinishCreatingWorld += HookFinishCreatingWorld;
            On_UIWorldCreationPreview.UpdateOption += HookPreviewUpdateOption;
            On_AWorldListItem.GetDifficulty += HookGetDifficulty;
        }

        //On_ 钩子由 tML 随模组卸载自动摘除，只清反射缓存
        public override void Unload() {
            difficultyEnumType = null;
            optionDifficultyField = null;
            difficultyButtonsField = null;
        }

        /// <summary>
        /// 难度行重建：先跑原版拿到四格，摘除后按原版构造参数重排六格。
        /// 六格等宽装不下图标加文字，整行统一去图标、标题居中微缩，
        /// 原版四格文案与颜色照抄原版，模式两格用文艺名与模式色板
        /// </summary>
        private static void HookAddDifficultyOptions(
            On_UIWorldCreation.orig_AddWorldDifficultyOptions orig, UIWorldCreation self,
            UIElement container, float accumualtedHeight, UIElement.MouseEvent clickEvent,
            string tagGroup, float usableWidthPercent) {
            orig(self, container, accumualtedHeight, clickEvent, tagGroup, usableWidthPercent);
            if (!Ready || difficultyButtonsField.GetValue(self) is not Array vanillaButtons) {
                return;
            }
            foreach (object button in vanillaButtons) {
                (button as UIElement)?.Remove();
            }

            //显示顺序与原版一致（旅程在首），后接两个模式档；枚举值经反射装箱
            object[] options = [
                Enum.ToObject(difficultyEnumType, 3),
                Enum.ToObject(difficultyEnumType, 0),
                Enum.ToObject(difficultyEnumType, 1),
                Enum.ToObject(difficultyEnumType, 2),
                Enum.ToObject(difficultyEnumType, BrutalOption),
                Enum.ToObject(difficultyEnumType, AsuraOption),
            ];
            LocalizedText[] titles = [
                Language.GetText("UI.Creative"),
                Language.GetText("UI.Normal"),
                Language.GetText("UI.Expert"),
                Language.GetText("UI.Master"),
                GameModeText.BrutalName,
                GameModeText.AsuraName,
            ];
            LocalizedText[] descriptions = [
                Language.GetText("UI.WorldDescriptionCreative"),
                Language.GetText("UI.WorldDescriptionNormal"),
                Language.GetText("UI.WorldDescriptionExpert"),
                Language.GetText("UI.WorldDescriptionMaster"),
                GameModeText.BrutalCreationDesc,
                GameModeText.AsuraCreationDesc,
            ];
            Color[] colors = [
                Main.creativeModeColor,
                Color.White,
                Main.mcColor,
                Main.hcColor,
                GameModeTheme.BrutalAccent,
                GameModeTheme.AsuraAccent,
            ];

            Type buttonType = typeof(GroupOptionButton<>).MakeGenericType(difficultyEnumType);
            Array buttons = Array.CreateInstance(buttonType, options.Length);
            for (int i = 0; i < options.Length; i++) {
                object button = Activator.CreateInstance(buttonType,
                    [options[i], titles[i], descriptions[i], colors[i], null, TitleSize, 0.5f, 10f]);
                UIElement element = (UIElement)button;
                element.Width = StyleDimension.FromPixelsAndPercent(
                    -1 * (options.Length - 1), 1f / options.Length * usableWidthPercent);
                element.Left = StyleDimension.FromPercent(1f - usableWidthPercent);
                element.HAlign = (float)i / (options.Length - 1);
                element.Top.Set(accumualtedHeight, 0f);
                //接回原版点击委托：互斥选中与 _optionDifficulty 写入全走原版逻辑
                element.OnLeftMouseDown += clickEvent;
                element.OnMouseOver += self.ShowOptionDescription;
                element.OnMouseOut += self.ClearOptionDescription;
                element.SetSnapPoint(tagGroup, i);
                container.Append(element);
                buttons.SetValue(button, i);
            }
            difficultyButtonsField.SetValue(self, buttons);
        }

        /// <summary>
        /// 创建落地：残酷/修罗是越界值，原版 switch 不会改写 <see cref="Main.GameMode"/>，
        /// 这里预设大师作底层难度并记待生效旗标。每次创建先清后判，
        /// 生成中断残留的旗标不得跨档泄漏
        /// </summary>
        private static void HookFinishCreatingWorld(
            On_UIWorldCreation.orig_FinishCreatingWorld orig, UIWorldCreation self) {
            GameModeSystem.PendingBrutal = false;
            GameModeSystem.PendingAsura = false;
            if (Ready) {
                int option = Convert.ToInt32(optionDifficultyField.GetValue(self));
                if (option is BrutalOption or AsuraOption) {
                    Main.GameMode = GameModeID.Master;
                    GameModeSystem.PendingBrutal = true;
                    GameModeSystem.PendingAsura = option == AsuraOption;
                }
            }
            orig(self);
        }

        /// <summary>预览小窗对越界难度整块不画背景，钳制到大师景观</summary>
        private static void HookPreviewUpdateOption(
            On_UIWorldCreationPreview.orig_UpdateOption orig, UIWorldCreationPreview self,
            byte difficulty, byte evil, byte size) {
            if (difficulty > GameModeID.Creative) {
                difficulty = (byte)GameModeID.Master;
            }
            orig(self, difficulty, evil, size);
        }

        /// <summary>
        /// 世界选择列表的难度文字：带模式头部旗标的世界换成模式文艺名与专属色板。
        /// 神匠不占难度位（内容向模式，非难度语义）
        /// </summary>
        private static void HookGetDifficulty(
            On_AWorldListItem.orig_GetDifficulty orig, AWorldListItem self,
            out string expertText, out Color gameModeColor) {
            orig(self, out expertText, out gameModeColor);
            WorldFileData data = self.Data;
            if (data == null || !data.TryGetHeaderData<GameModeSystem>(out TagCompound tag)) {
                return;
            }
            if (!(tag.TryGet(nameof(GameModeSystem.BrutalActive), out bool brutal) && brutal)) {
                return;
            }
            bool asura = tag.TryGet(nameof(GameModeSystem.AsuraActive), out bool value) && value;
            //菜单阶段没有 Main.getGoodWorld，换脸判定读世界档案的种子旗标
            GameModeFace face = !asura ? GameModeFace.Brutal
                : data.ForTheWorthy || data.ZenithWorld ? GameModeFace.Annihilation
                : GameModeFace.Asura;
            expertText = GameModeText.Name(face).Value;
            gameModeColor = GameModeTheme.Accent(face);
        }
    }
}
