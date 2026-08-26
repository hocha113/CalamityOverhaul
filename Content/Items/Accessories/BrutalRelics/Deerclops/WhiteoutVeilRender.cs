using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Deerclops
{
    /// <summary>白视风暴全屏后效，screenTarget ping-pong 单pass</summary>
    internal class WhiteoutVeilRender : RenderHandle
    {
        /// <summary>权重1.77(残酷遗物认领槽)，巨鹿本体暴雪视界在1.07，互不相扰</summary>
        public override float Weight => 1.77f;

        /// <summary>清明圈半径(px，与shader uClearRadius同源)</summary>
        internal const float ClearRadiusPx = 360f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            WhiteoutVeilFX.Update();

            if (!WhiteoutVeilFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.BRelicWhiteoutVeil?.IsLoaded != true) {
                return;
            }
            Effect shader = EffectLoader.BRelicWhiteoutVeil.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || noise == null) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uStorm"]?.SetValue(WhiteoutVeilFX.Veil);
            shader.Parameters["uPunch"]?.SetValue(WhiteoutVeilFX.Punch);
            shader.Parameters["uCenterUV"]?.SetValue(WorldToScreenUV(WhiteoutVeilFX.CenterWorld));
            shader.Parameters["uClearRadius"]?.SetValue(PixelsToHeightNorm(ClearRadiusPx));
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图，
            //参数式贴图绑定实机失效（合同同 DeerclopsVeilRender）
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            PingPong(sb, gd, screenSwap, shader);
        }

        /// <summary>拷屏再 shader 回写</summary>
        private static void PingPong(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap, Effect shader) {
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        /// <summary>世界→归一化uv(含Zoom)</summary>
        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }

        /// <summary>像素→屏高归一化</summary>
        private static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }

    /// <summary>
    /// 白视风暴全屏FX状态。客户端静态、纯表现层：每帧扫描带风暴buff的玩家
    /// (buff经原版同步)重建目标值，无任何玩法状态，不走网络
    /// </summary>
    internal static class WhiteoutVeilFX
    {
        private static float veil;
        private static float punch;
        private static Vector2 centerWorld;
        private static bool hadStorm;

        internal static float Veil => veil;
        internal static float Punch => punch;
        internal static Vector2 CenterWorld => centerWorld;

        public static bool HasAny => veil > 0.02f || punch > 0.02f;

        /// <summary>每帧平滑(渲染句柄驱动，仅客户端)</summary>
        public static void Update() {
            //取衰减后最强的风暴主人：自己恒满权，旁人按距离衰减
            int buffType = ModContent.BuffType<WhiteoutStormBuff>();
            float best = 0f;
            Vector2 bestPos = centerWorld;
            bool any = false;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.Alives() || !player.HasBuff(buffType)) {
                    continue;
                }
                float atten = player.whoAmI == Main.myPlayer
                    ? 1f
                    : MathHelper.Clamp(1.45f - player.Distance(Main.LocalPlayer.Center) / 1900f, 0f, 1f);
                if (atten > best) {
                    best = atten;
                    bestPos = player.Center;
                    any = true;
                }
            }

            if (any) {
                //新风暴：圈心直落+触发白闪；持续中圈心快速跟随
                if (!hadStorm) {
                    centerWorld = bestPos;
                    punch = best;
                }
                else {
                    centerWorld = Vector2.Lerp(centerWorld, bestPos, 0.5f);
                }
            }
            hadStorm = any;

            //风暴浓度上限0.85：屏缘吞没、中央保读(视野保持)
            veil = MathHelper.Lerp(veil, best * 0.85f, any ? 0.12f : 0.05f);
            punch = Math.Max(punch - 0.05f, 0f);
        }

        /// <summary>风暴随行演出(buff驱动，各端对每个风暴主人自播)</summary>
        public static void EmitStormAmbient(Player player) {
            //环绕寒雾
            if (Main.GameUpdateCount % 5 == 0) {
                PRTLoader.NewParticle<PRT_DefCryoMist>(player.Center, Vector2.Zero,
                    DeerclopsMotion.ColdWhite * 0.45f, Main.rand.NextFloat(0.7f, 1.1f))
                    .Configure(Main.rand.Next(20, 32), player.Center, Main.rand.NextFloat(46f, 84f));
            }
            //圈内飘雪
            if (Main.rand.NextBool(2)) {
                Dust snow = Dust.NewDustPerfect(
                    player.Center + new Vector2(Main.rand.NextFloat(-150f, 150f), -Main.rand.NextFloat(60f, 130f)),
                    DustID.Snow, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(1f, 2.4f)),
                    100, default, Main.rand.NextFloat(0.8f, 1.4f));
                snow.noGravity = true;
            }
            //偶发晶闪
            if (Main.rand.NextBool(16)) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    player.Center + Main.rand.NextVector2Circular(70f, 60f),
                    Main.rand.NextVector2Circular(1f, 1f),
                    DeerclopsMotion.ColdWhite, Main.rand.NextFloat(1.8f, 3f))
                    .Configure(Main.rand.Next(14, 24));
            }
        }

        /// <summary>冷却转好提示：所有者本地一圈霜晶(客户端)</summary>
        public static void EmitReadyCue(Player player) {
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    player.Center + angle.ToRotationVector2() * 30f,
                    angle.ToRotationVector2() * 0.8f,
                    DeerclopsMotion.IceBlue, Main.rand.NextFloat(1.8f, 2.6f))
                    .Configure(Main.rand.Next(16, 26));
            }
        }
    }
}
