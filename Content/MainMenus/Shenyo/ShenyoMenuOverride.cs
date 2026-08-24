using CalamityOverhaul.Content.MainMenus.Himayo;
using CalamityOverhaul.Content.MainMenus.Overs;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Shenyo
{
    /// <summary>鬼湖夜雨主菜单接管主体：标题帧（menuMode==0）整帧自绘并跳过原版 DrawMenu，其余 menuMode 放行原版；
    /// 结构镜像 <see cref="HimayoMenuOverride"/>，场景绘制委托 <see cref="ShenyoGhostLakeScene"/>；
    /// 任何异常或反射缺失均 fail-open 回原版菜单</summary>
    internal class ShenyoMenuOverride : MenuOverride
    {
        //帧首锁存的接管决策：DrawMenu 定夺、PostDrawMenu 消费，防 menuMode 帧中变化引起原版按钮闪帧
        private static bool takeoverLatch;
        //本帧对子页面放行了原版 DrawMenu；若 orig 中途把 menuMode 改回 0，帧末会闪出社交/版本号，须在 PostDrawMenu 盖回
        private static bool yieldedToVanilla;
        //入场淡入 0~1
        private static float fade;
        //运行期绘制异常一次即永久停用（fail-open）
        private static bool runtimeFault;

        public static bool ThemeSelected =>
            VaultLoad.LoadenContent && ModContent.GetInstance<ShenyoMenu>()?.IsSelected == true;

        public override void SetStaticDefaults() {
            if (Main.dedServ) {
                return;
            }
            //动作表与夜樱主题共用，重复初始化由内部幂等闸挡下
            HimayoMenuActions.Initialize(CWRMod.Instance);
            ShenyoMenuButtons.Initialize();
        }

        public override bool CanOverride() =>
            !Main.dedServ && Main.gameMenu && !runtimeFault && HimayoMenuActions.Ready && ThemeSelected;

        internal static void OnThemeSelected() {
            fade = 0f;
            ShenyoGhostLakeScene.Reset();
            ShenyoMenuButtons.Reset();
            //起手一记压低的落水声：雨已经在下了（与初见沈幽同一句语汇）
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.55f,
                Volume = 0.35f,
                MaxInstances = 3,
            });
        }

        internal static void OnThemeDeselected() => ShenyoGhostLakeScene.Release();

        public override void MenuLogicUpdate() {
            bool onTitle = Main.menuMode == 0;
            if (onTitle) {
                //接管期间原版 menuMode==0 分支不运行，其常态职责在此复刻
                HimayoMenuActions.TitleHousekeeping();
            }

            ShenyoGhostLakeScene.Tick();
            fade = MathF.Min(fade + 0.020f, 1f);

            //输入优先级：模态面板 > 公告栏/立绘 > 标题按钮
            bool overlayActive = FeedbackUI.Instance.OnActive()
                || AcknowledgmentUI.OnActive()
                || OverhaulSettingsUI.OnActive();
            bool overCwrUI = overlayActive
                || HimayoMenuOverride.MouseOverMenuOverlays(new Point(Main.mouseX, Main.mouseY));

            if (onTitle) {
                ShenyoMenuButtons.Tick(inputFree: !overCwrUI);
            }
        }

        public override bool? DrawMenu(GameTime gameTime) {
            takeoverLatch = false;
            yieldedToVanilla = false;
            //tML 有延迟错误待弹时放行原版，由原版标题分支弹出错误 UI，处理完回标题再恢复接管
            if (HimayoMenuActions.HasPendingErrorMessages) {
                return null;
            }

            try {
                //任意 menuMode 先铺鬼湖场景，盖住 DoDraw 的原版天空；标题帧继续自绘 chrome，子页面放行原版 UI 叠上
                DrawAtmosphereLayer();

                if (Main.menuMode != 0) {
                    yieldedToVanilla = true;
                    return null;
                }

                takeoverLatch = true;
                DrawTitleChrome();
            } catch (Exception ex) {
                //一次异常即永久回退原版，恢复批次保住本帧
                runtimeFault = true;
                takeoverLatch = false;
                yieldedToVanilla = false;
                CWRMod.Instance.Logger.Error($"[ShenyoMenu] 接管绘制异常，永久回退原版菜单: {ex}");
                EnsureMenuBatch(Main.spriteBatch);
            }
            //标题帧即使出错也跳过原版本帧（批次已恢复），下一帧起 CanOverride=false 走原版
            return Main.menuMode == 0 ? false : null;
        }

        public override void PostDrawMenu(GameTime gameTime) {
            //子页面 orig 中途回到标题：同帧盖回场景+chrome，抹掉社交按钮/版本号闪帧
            if (yieldedToVanilla && Main.menuMode == 0
                && !HimayoMenuActions.HasPendingErrorMessages && !runtimeFault) {
                yieldedToVanilla = false;
                try {
                    DrawAtmosphereLayer();
                    DrawTitleChrome();
                    Main.DrawCursor(Main.DrawThickCursor());
                } catch (Exception ex) {
                    runtimeFault = true;
                    CWRMod.Instance.Logger.Error($"[ShenyoMenu] 返回标题盖帧异常，永久回退原版菜单: {ex}");
                    EnsureMenuBatch(Main.spriteBatch);
                }
                return;
            }
            yieldedToVanilla = false;

            if (!takeoverLatch) {
                return;
            }
            takeoverLatch = false;
            //原版光标在 DrawMenu 帧末绘制，跳过后在此补上（框架已开启 UIScale 批次）
            Main.DrawCursor(Main.DrawThickCursor());
            //原版 DrawMenu 帧末的按键释放维护同样需要补齐，否则下帧点击判定失真
            Main.mouseLeftRelease = !Main.mouseLeft;
            Main.mouseRightRelease = !Main.mouseRight;
            Main.mouseMiddleRelease = !Main.mouseMiddle;
            Main.mouseXButton1Release = !Main.mouseXButton1;
            Main.mouseXButton2Release = !Main.mouseXButton2;
        }

        //氛围层：鬼湖场景整幅（含湿屏合成）；入口批次须已开启，返回前交还已开启批次
        private static void DrawAtmosphereLayer() {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            ShenyoGhostLakeScene.Draw(sb, fade);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        //标题 chrome：按钮/公告栏/立绘
        private static void DrawTitleChrome() {
            SpriteBatch sb = Main.spriteBatch;

            //标题簇与按钮列（氛围层已交还开启批次）
            ShenyoMenuButtons.Draw(sb, fade);
            sb.End();

            //公告栏与角色码头等 Mod_MenuLoad 层（内部自管批次与固定步长逻辑）
            HimayoMenuActions.DriveMenuOverlays(sb);

            //交还开启批次，框架随后 End
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        //异常恢复：确保存在已开启批次；若已开启则 Begin 抛出并忽略
        private static void EnsureMenuBatch(SpriteBatch sb) {
            try {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            } catch (InvalidOperationException) {
                //批次本就处于开启状态，无需处理
            }
        }
    }
}
