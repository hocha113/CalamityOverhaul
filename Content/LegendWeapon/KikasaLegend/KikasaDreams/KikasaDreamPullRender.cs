using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦拉入/归返的全屏合成：拷屏→镜面着色（黑犬在镜里替换玩家的镜像）→
    /// 绕屏幕中心旋转写回。几何契约与鬼雨异化翻转一致（θ=π 恒等、覆盖缩放两端恒等）。<br/>
    /// 对旁观者同样合成（缝线投影 clamp 0.3~0.7），但不锁输入不变焦。
    /// 结算闪色温随方向：入梦血红，归返暖白。
    /// </summary>
    internal sealed class KikasaDreamPullRender : RenderHandle
    {
        /// <summary>压在鬼雨异化翻转(2.02)之后一档，两者相位互斥不会同帧合成</summary>
        public override float Weight => 2.03f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || (kdp.Phase != KikasaDomainPhase.DreamPull
                && kdp.Phase != KikasaDomainPhase.DreamReturn)) {
                return;
            }

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch, kdp);
                return;
            }

            Effect fx = EffectLoader.KikasaDream?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawLowQualityFallback(spriteBatch, kdp);
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed
                || Main.screenTargetSwap == null || Main.screenTargetSwap.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFallback(spriteBatch, kdp);
                return;
            }

            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D wolf = TextureAssets.Npc[NPCID.Wolf].Value;

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //1. 拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //2. 镜面合成到第二交换屏
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            SetMirrorParams(fx, kdp, wolf);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            if (wolf != null) {
                graphicsDevice.Textures[2] = wolf;
                graphicsDevice.SamplerStates[2] = SamplerState.LinearClamp;
            }
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechMirror"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //3. 绕屏幕中心旋转写回主屏
            float theta = kdp.DreamRollAngle;
            float dreamSide = DreamSideOf(kdp);
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            //旋转途中的角落垫底：向梦侧压得更黑更红
            graphicsDevice.Clear(Color.Lerp(new Color(16, 6, 8), new Color(20, 4, 5), dreamSide));
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, BuildRollMatrix(theta));
            spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            DrawRotationSmear(spriteBatch, kdp, theta);
            DrawFlashOverlay(spriteBatch, kdp);

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>镜面调色向哪侧靠：拉入随 DreamMix 走向梦，归返反向走回血湖</summary>
        private static float DreamSideOf(KikasaDomainPlayer kdp)
            => kdp.Phase == KikasaDomainPhase.DreamPull ? kdp.DreamMix : 1f - kdp.DreamMix;

        private static void SetMirrorParams(Effect fx, KikasaDomainPlayer kdp, Texture2D wolf) {
            float w = Main.screenWidth;
            float h = Main.screenHeight;
            bool pull = kdp.Phase == KikasaDomainPhase.DreamPull;
            float rollProgress = MathHelper.Clamp(kdp.DreamRollAngle / MathHelper.Pi, 0f, 1f);

            //缝线取湖面线的实际投影，翻转期间收敛到屏幕中线；旁观者靠 clamp 兜底
            float pivotY = MathHelper.Clamp(
                WorldToScreen(new Vector2(Main.screenPosition.X, kdp.LakeWorldY)).Y / h,
                0.3f, 0.7f);
            pivotY = MathHelper.Lerp(pivotY, 0.5f, rollProgress);

            float originU = MathHelper.Clamp(WorldToScreen(kdp.Player.Center).X / w, -0.2f, 1.2f);

            float wobble = 0.0025f + 0.011f * kdp.FoamBoost + 0.014f * kdp.DreamBoil;
            float fadeIn = MathHelper.Clamp(kdp.PhaseTimer / 14f, 0f, 1f);

            fx.Parameters["uTime"]?.SetValue(kdp.EffectTime);
            fx.Parameters["uPivotY"]?.SetValue(pivotY);
            fx.Parameters["uRollProgress"]?.SetValue(rollProgress);
            fx.Parameters["uOriginU"]?.SetValue(originU);
            fx.Parameters["uAspect"]?.SetValue(w / h);
            fx.Parameters["uWaterLevel"]?.SetValue(pivotY);
            fx.Parameters["uWaterWobble"]?.SetValue(wobble);
            fx.Parameters["uFoamBoost"]?.SetValue(kdp.FoamBoost);
            fx.Parameters["uSwallow"]?.SetValue(kdp.DreamSwallow);
            fx.Parameters["uGrade"]?.SetValue(kdp.DreamGrade);
            fx.Parameters["uGlimpse"]?.SetValue(kdp.DreamGlimpse);
            fx.Parameters["uGlimpseRing"]?.SetValue(kdp.DreamGlimpseRing);
            fx.Parameters["uSeamGlow"]?.SetValue(kdp.DreamSeamGlow);
            fx.Parameters["uBoil"]?.SetValue(kdp.DreamBoil);
            fx.Parameters["uDreamSide"]?.SetValue(DreamSideOf(kdp));
            fx.Parameters["uMix"]?.SetValue(fadeIn);

            //镜里抹掉施术者本人，人影不与犬影同镜
            float coverA = kdp.HoundReflection ? fadeIn : 0f;
            Rectangle hit = kdp.Player.Hitbox;
            Vector2 coverTl = WorldToScreen(new Vector2(hit.Left - 18f, hit.Top - 16f)) / new Vector2(w, h);
            Vector2 coverBr = WorldToScreen(new Vector2(hit.Right + 18f, hit.Bottom + 6f)) / new Vector2(w, h);
            fx.Parameters["uCoverRect"]?.SetValue(new Vector4(coverTl.X, coverTl.Y, coverBr.X, coverBr.Y));
            fx.Parameters["uCoverA"]?.SetValue(coverA);

            SetHoundParams(fx, kdp, wolf, pull, fadeIn, w, h);
        }

        /// <summary>
        /// 镜中黑犬的几何：与 <see cref="KikasaHoundReflection"/> 同一套镜像（爪线=脚底映像），
        /// 拉入期镜面接管拷屏后，犬影由本着色器续画，玩家的镜像被它替换
        /// </summary>
        private static void SetHoundParams(Effect fx, KikasaDomainPlayer kdp,
            Texture2D wolf, bool pull, float fadeIn, float w, float h) {

            if (wolf == null) {
                fx.Parameters["uHoundA"]?.SetValue(0f);
                return;
            }

            Player caster = kdp.Player;
            int frameCount = Main.npcFrameCount[NPCID.Wolf];
            int frameH = wolf.Height / frameCount;
            int frame = KikasaHoundReflection.GetFrame(caster.whoAmI);

            float quadW = wolf.Width * KikasaHoundReflection.HoundScale;
            float quadH = (frameH - 2) * KikasaHoundReflection.HoundScale;
            float quadTopY = 2f * kdp.LakeWorldY - caster.Bottom.Y;
            Vector2 topLeft = WorldToScreen(
                new Vector2(caster.Center.X - quadW * 0.5f, quadTopY));
            //视图矩阵含缩放，quad 尺寸同样要过缩放
            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 sizePx = new(quadW * zoom, quadH * zoom);

            //倒影已醒才有影可用；归返镜里它候在湖底，弱半分
            float presence = kdp.HoundReflection ? 1f : 0f;
            float houndA = fadeIn * presence * (pull ? 1f : 0.8f);

            fx.Parameters["uHoundRect"]?.SetValue(new Vector4(
                topLeft.X / w, topLeft.Y / h, sizePx.X / w, sizePx.Y / h));
            fx.Parameters["uHoundUv"]?.SetValue(new Vector4(
                0f, (frame * frameH + 1) / (float)wolf.Height,
                1f, (frameH - 2) / (float)wolf.Height));
            fx.Parameters["uHoundFlipH"]?.SetValue(
                KikasaHoundReflection.GetFacing(caster.whoAmI) > 0 ? 1f : 0f);
            fx.Parameters["uHoundA"]?.SetValue(houndA);
            fx.Parameters["uHoundAspect"]?.SetValue(sizePx.X / MathF.Max(sizePx.Y, 1f));
            fx.Parameters["uEyeUv"]?.SetValue(KikasaHoundReflection.EyeAnchor);
            //驻留段那双眼睛必须燃起来；归返只剩一点余烬
            fx.Parameters["uGaze"]?.SetValue(pull
                ? MathF.Max(0.3f, kdp.DreamGaze) : 0.3f);
        }

        /// <summary>绕屏幕中心的旋转矩阵，覆盖缩放只在旋转中途起效，两端恒等</summary>
        private static Matrix BuildRollMatrix(float theta) {
            if (MathF.Abs(theta) <= 0.0001f) {
                return Matrix.Identity;
            }

            float w = Main.screenWidth;
            float h = Main.screenHeight;
            float c = MathF.Abs(MathF.Cos(theta));
            float s = MathF.Abs(MathF.Sin(theta));
            float cover = MathF.Max((w * c + h * s) / w, (w * s + h * c) / h);
            cover *= 1f + 0.03f * s;

            Vector2 pivot = new(w * 0.5f, h * 0.5f);
            return Matrix.CreateTranslation(-pivot.X, -pivot.Y, 0f)
                * Matrix.CreateRotationZ(theta)
                * Matrix.CreateScale(cover, cover, 1f)
                * Matrix.CreateTranslation(pivot.X, pivot.Y, 0f);
        }

        /// <summary>旋转拖影：转得越快残影越长</summary>
        private static void DrawRotationSmear(SpriteBatch spriteBatch, KikasaDomainPlayer kdp, float theta) {
            float velocity = kdp.DreamRollVelocity;
            if (MathF.Abs(velocity) <= 0.004f) {
                return;
            }
            DrawSmearTap(spriteBatch, theta - velocity * 2.4f, 0.15f);
            DrawSmearTap(spriteBatch, theta - velocity * 4.8f, 0.08f);
        }

        private static void DrawSmearTap(SpriteBatch spriteBatch, float lagTheta, float strength) {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, BuildRollMatrix(lagTheta));
            spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White * strength);
            spriteBatch.End();
        }

        /// <summary>结算闪：入梦是被咬进红黑里的一口血光，归返是吐回真实的暖白</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            float flash = kdp.DreamFlash;
            if (flash <= 0.002f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            bool pull = kdp.Phase == KikasaDomainPhase.DreamPull;
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            Color soft = pull
                ? new Color(0.86f, 0.20f, 0.13f, 0f)
                : new Color(0.95f, 0.86f, 0.84f, 0f);
            Color hardCol = pull
                ? new Color(214, 52, 38)
                : new Color(240, 226, 224);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(white, full, soft * (0.58f * flash));
            spriteBatch.End();

            if (flash > 0.55f) {
                float hard = (flash - 0.55f) / 0.45f;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                spriteBatch.Draw(white, full, hardCol * (0.94f * hard));
                spriteBatch.End();
            }
        }

        /// <summary>RT 不可用的纯色回退：按沸腾/吞没进度压暗 + 结算闪，相位推进不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            float dreamSide = DreamSideOf(kdp);
            float dim = kdp.DreamBoil * 0.22f + kdp.DreamSwallow * 0.22f;
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            if (dim > 0.002f) {
                spriteBatch.Draw(white, full,
                    Color.Lerp(new Color(24, 8, 10), new Color(26, 6, 7), dreamSide) * dim);
            }
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch, kdp);
        }

        private static Vector2 WorldToScreen(Vector2 worldPos)
            => Vector2.Transform(worldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
    }
}
