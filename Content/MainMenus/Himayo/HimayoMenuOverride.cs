using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MainMenus.Characters;
using CalamityOverhaul.Content.MainMenus.Overs;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Diagnostics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>夜樱主菜单接管主体：标题帧（menuMode==0）整帧自绘并跳过原版 DrawMenu，其余 menuMode 放行原版；
    /// 任何异常或反射缺失均 fail-open 回原版菜单</summary>
    internal class HimayoMenuOverride : MenuOverride
    {
        [VaultLoaden(CWRConstant.ADV + "Himayo/HimayoEquirectangular")]
        internal static Asset<Texture2D> PanoramaTex = null;

        //帧首锁存的接管决策：DrawMenu 定夺、PostDrawMenu 消费，防 menuMode 帧中变化引起原版按钮闪帧
        private static bool takeoverLatch;
        //本帧对子页面放行了原版 DrawMenu；若 orig 中途把 menuMode 改回 0，帧末会闪出社交/版本号，须在 PostDrawMenu 盖回
        private static bool yieldedToVanilla;
        //入场淡入 0~1
        private static float fade;
        //运行期绘制异常一次即永久停用（fail-open）
        private static bool runtimeFault;
        //绘制插值时钟：MenuLogicUpdate 每 tick 打点，Draw 用经过时间求部分帧
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static double lastTickSeconds;
        private static Vector2 prevMouse;
        private static Vector2 mouseVel;

        public static bool ThemeSelected =>
            VaultLoad.LoadenContent && ModContent.GetInstance<HimayoMenu>()?.IsSelected == true;

        public override void SetStaticDefaults() {
            if (Main.dedServ) {
                return;
            }
            HimayoMenuActions.Initialize(CWRMod.Instance);
            HimayoMenuButtons.Initialize();
        }

        public override bool CanOverride() =>
            !Main.dedServ && Main.gameMenu && !runtimeFault && HimayoMenuActions.Ready && ThemeSelected;

        internal static void OnThemeSelected() {
            fade = 0f;
            HimayoMenuCamera.Reset();
            HimayoPetalField.Reset();
            HimayoMenuButtons.Reset();
        }

        internal static void OnThemeDeselected() => HimayoPetalField.ReleaseCaught();

        public override void MenuLogicUpdate() {
            lastTickSeconds = clock.Elapsed.TotalSeconds;

            Vector2 mouse = new(Main.mouseX, Main.mouseY);
            mouseVel = mouse - prevMouse;
            prevMouse = mouse;

            bool onTitle = Main.menuMode == 0;
            if (onTitle) {
                //接管期间原版 menuMode==0 分支不运行，其常态职责在此复刻
                HimayoMenuActions.TitleHousekeeping();
            }

            HimayoMenuCamera.Tick();
            fade = MathF.Min(fade + 0.025f, 1f);

            //输入优先级：模态面板 > 公告栏/立绘 > 夜樱按钮 > 近景花瓣
            bool overlayActive = FeedbackUI.Instance.OnActive()
                || AcknowledgmentUI.OnActive()
                || OverhaulSettingsUI.OnActive();
            bool overCwrUI = overlayActive || MouseOverMenuOverlays(mouse.ToPoint());

            bool buttonsHover = false;
            if (onTitle) {
                buttonsHover = HimayoMenuButtons.Tick(inputFree: !overCwrUI);
            }

            bool interactive = onTitle && !overCwrUI && !buttonsHover;
            bool catching = interactive && Main.mouseLeft && Main.hasFocus;
            HimayoPetalField.Tick(interactive, mouse, mouseVel, catching);
        }

        //公告栏主热区、词条与立绘头像占用判断
        private static bool MouseOverMenuOverlays(Point pt) {
            if (!VaultLoad.LoadenContent) {
                return false;
            }
            BulletinBoardUI board = BulletinBoardUI.Instance;
            if (board != null && board.UIHitBox.Contains(pt)) {
                return true;
            }
            foreach (BulletinBoardElement element in BulletinBoardUI.bulletinBoardElements) {
                if (element.UIHitBox.Contains(pt)) {
                    return true;
                }
            }
            if (SupCalPortraitUI.Instance?.CapturesMenuInput(pt) == true) {
                return true;
            }
            if (HelenPortraitUI.Instance?.CapturesMenuInput(pt) == true) {
                return true;
            }
            return false;
        }

        public override bool? DrawMenu(GameTime gameTime) {
            takeoverLatch = false;
            yieldedToVanilla = false;
            if (PanoramaTex?.Value == null) {
                return null;
            }
            //tML 有延迟错误待弹时放行原版，由原版标题分支弹出错误 UI，处理完回标题再恢复接管
            if (HimayoMenuActions.HasPendingErrorMessages) {
                return null;
            }

            try {
                //任意 menuMode 先铺氛围层，盖住 DoDraw 的原版天空；标题帧继续自绘 chrome，子页面放行原版 UI 叠上
                DrawAtmosphereLayer();

                if (Main.menuMode != 0) {
                    //放行 orig：若本帧点「返回」把 menuMode 改成 0，原版帧末会画社交/版本号——PostDrawMenu 用 yielded 标记盖回
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
                CWRMod.Instance.Logger.Error($"[HimayoMenu] 接管绘制异常，永久回退原版菜单: {ex}");
                EnsureMenuBatch(Main.spriteBatch);
            }
            //标题帧即使出错也跳过原版本帧（批次已恢复），下一帧起 CanOverride=false 走原版
            return Main.menuMode == 0 ? false : null;
        }

        public override void PostDrawMenu(GameTime gameTime) {
            //子页面 orig 中途回到标题：同帧盖回氛围+chrome，抹掉社交按钮/版本号闪帧
            if (yieldedToVanilla && Main.menuMode == 0 && PanoramaTex?.Value != null
                && !HimayoMenuActions.HasPendingErrorMessages && !runtimeFault) {
                yieldedToVanilla = false;
                try {
                    //框架已开启 UIScale 批次；氛围层会 End→绘制→Begin，chrome 接续
                    DrawAtmosphereLayer();
                    DrawTitleChrome();
                    Main.DrawCursor(Main.DrawThickCursor());
                } catch (Exception ex) {
                    runtimeFault = true;
                    CWRMod.Instance.Logger.Error($"[HimayoMenu] 返回标题盖帧异常，永久回退原版菜单: {ex}");
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

        //部分帧插值：距最近一次逻辑 tick 的经过时间，60Hz 归一
        private static float DrawAlpha() {
            float alpha = (float)((clock.Elapsed.TotalSeconds - lastTickSeconds) * 60.0);
            return MathHelper.Clamp(alpha, 0f, 1f);
        }

        //氛围层：全景 + 三层花瓣；入口批次须已开启，返回前交还已开启批次
        private static void DrawAtmosphereLayer() {
            SpriteBatch sb = Main.spriteBatch;
            float alpha = DrawAlpha();
            sb.End();
            DrawPanoramaCore(sb, alpha);
            HimayoPetalField.DrawBack(sb, alpha, fade);
            HimayoPetalField.DrawFront(sb, alpha, fade);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        //标题 chrome：按钮/公告栏/立绘；近景花瓣再盖顶（接瓣交互只在标题）
        private static void DrawTitleChrome() {
            SpriteBatch sb = Main.spriteBatch;
            float alpha = DrawAlpha();

            //标题簇与按钮列（氛围层已交还开启批次）
            HimayoMenuButtons.Draw(sb, fade);
            sb.End();

            //公告栏等 Mod_MenuLoad 层（内部自管批次与固定步长逻辑）
            HimayoMenuActions.DriveMenuOverlays(sb);

            //立绘：HoverHook 同款直调，需处于已开启的 UIScale 批次
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            DrivePortraits(sb);
            sb.End();

            //近景花瓣盖在 chrome 之上（氛围层已画过一轮；此处再画一次保证压住按钮/立绘）
            HimayoPetalField.DrawFront(sb, alpha, fade);

            //交还开启批次，框架随后 End
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        private static void DrivePortraits(SpriteBatch sb) {
            if (!VaultLoad.LoadenContent) {
                return;
            }
            if (SupCalPortraitUI.Instance?.Active == true) {
                SupCalPortraitUI.Instance.Update();
                SupCalPortraitUI.Instance.Draw(sb);
            }
            if (HelenPortraitUI.Instance?.Active == true) {
                HelenPortraitUI.Instance.Update();
                HelenPortraitUI.Instance.Draw(sb);
            }
        }

        //全景绘制核心：着色器路径按视线方向采样 equirect；着色器或噪声缺席时退化为静态 cover 铺满
        private static void DrawPanoramaCore(SpriteBatch sb, float alpha) {
            Texture2D tex = PanoramaTex.Value;
            Effect pano = EffectLoader.HimayoPanorama?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width, vpH = gd.Viewport.Height;
            if (vpW <= 0 || vpH <= 0) {
                return;
            }

            if (pano == null || noise == null) {
                //静态回退：cover 等比铺满，无全景交互但画面完整
                sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                float sc = MathF.Max(vpW / (float)tex.Width, vpH / (float)tex.Height);
                Vector2 pos = new((vpW - tex.Width * sc) * 0.5f, (vpH - tex.Height * sc) * 0.5f);
                sb.Draw(tex, pos, null, Color.White * fade, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
                sb.End();
                return;
            }

            HimayoMenuCamera.GetBasis(alpha, out Vector3 forward, out Vector3 right, out Vector3 up);
            pano.Parameters["uForward"]?.SetValue(forward);
            pano.Parameters["uRight"]?.SetValue(right);
            pano.Parameters["uUp"]?.SetValue(up);
            pano.Parameters["uTanHalfFov"]?.SetValue(HimayoMenuCamera.TanHalfFov);
            pano.Parameters["uAspect"]?.SetValue(vpW / (float)vpH);
            pano.Parameters["uFade"]?.SetValue(fade);
            pano.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            //Catmull-Rom 采样需要底图像素尺寸
            pano.Parameters["uTexSize"]?.SetValue(new Vector2(tex.Width, tex.Height));

            //s0 需 U 向 Wrap 以硬件处理经度缝；全屏 quad 用背板像素空间
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            pano.CurrentTechnique.Passes[0].Apply();
            sb.Draw(tex, new Rectangle(0, 0, vpW, vpH), Color.White);
            sb.End();
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
