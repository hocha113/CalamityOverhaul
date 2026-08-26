using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Renders
{
    /// <summary>
    /// 回收官旗舰渲染（OldNetWarden.fx 消费端，Weight=1.43 为协调者分配槽位）：
    /// 实体层下方（物块后、NPC 前）绘制纹章环 + 核心独目两张 shader 画布，
    /// 位于 OldNetGradeRender(1.45) 拷屏之前，随世界一起吃调色降解；
    /// 实体层结束后（EndEntityDraw）补终末协议全屏红沿脉冲与死亡 impact-frame 白闪
    /// （两者为 CPU quad，不依赖 shader）。
    /// shader 缺编时本渲染静默跳过，战斗可读性由本体 PreDraw 的 CPU 全保真层承担。
    /// 共享参数化纪律：每次调用全参数重设
    /// </summary>
    internal class OldNetWardenRender : RenderHandle
    {
        public override float Weight => 1.43f;

        //画布：纹章环 140px、核心独目 96px（随入场 RenderScale 缩放）
        private const float RingCanvas = 140f;
        private const float EyeCanvas = 96f;

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.dedServ || !OldNetWorld.Active) {
                return;
            }
            Effect fx = EffectLoader.OldNetWarden?.Value;
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (fx == null || px == null || px.IsDisposed) {
                return;
            }
            int type = ModContent.NPCType<OldNetWardenICE>();
            bool any = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == type) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            float time = (float)Main.timeForVisualEffects / 60f;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type || npc.ModNPC is not OldNetWardenICE warden) {
                    continue;
                }
                Vector2 center = npc.Center - Main.screenPosition;
                float seed = npc.whoAmI * 0.477f;

                //纹章环（底层）：共享参数化 shader 每次调用全参数重设
                fx.CurrentTechnique = fx.Techniques["TechSealRing"];
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(seed);
                fx.Parameters["uDecay"]?.SetValue(warden.RenderDecay);
                fx.Parameters["uCharge"]?.SetValue(warden.RenderCharge);
                fx.Parameters["uSpin"]?.SetValue(warden.RingSpin);
                fx.Parameters["uAlpha"]?.SetValue(warden.RenderAlpha);
                fx.CurrentTechnique.Passes[0].Apply();
                float ringSize = RingCanvas * warden.RenderScale;
                spriteBatch.Draw(px, center, null, Color.White, 0f, origin,
                    new Vector2(ringSize / px.Width, ringSize / px.Height),
                    SpriteEffects.None, 0f);

                //核心独目（环内）
                fx.CurrentTechnique = fx.Techniques["TechCoreEye"];
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(seed);
                fx.Parameters["uDecay"]?.SetValue(warden.RenderDecay);
                fx.Parameters["uCharge"]?.SetValue(warden.RenderCharge);
                fx.Parameters["uSpin"]?.SetValue(warden.RingSpin);
                fx.Parameters["uAlpha"]?.SetValue(warden.RenderAlpha);
                fx.CurrentTechnique.Passes[0].Apply();
                float eyeSize = EyeCanvas * warden.RenderScale;
                spriteBatch.Draw(px, center, null, Color.White, 0f, origin,
                    new Vector2(eyeSize / px.Width, eyeSize / px.Height),
                    SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        //实体层之后：终末协议红沿脉冲 + 死亡 impact-frame 白闪（CPU quad，无 shader 依赖）
        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.dedServ || !OldNetWorld.Active) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            //聚合全部在场回收官的通道值（正常只有一台）
            int type = ModContent.NPCType<OldNetWardenICE>();
            float edgePulse = 0f;
            float whiteFlash = 0f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type && npc.ModNPC is OldNetWardenICE warden) {
                    edgePulse = MathF.Max(edgePulse, warden.EdgePulse);
                    whiteFlash = MathF.Max(whiteFlash, warden.WhiteFlash);
                }
            }
            if (edgePulse <= 0.01f && whiteFlash <= 0.01f) {
                return;
            }

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            //红沿脉冲：四缘三层内收条带拟渐变（最狠的招给最醒目的读秒）
            if (edgePulse > 0.01f) {
                Color ember = new(235, 64, 44);
                float wobble = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);
                for (int layer = 0; layer < 3; layer++) {
                    int thick = 26 - layer * 7;
                    float alpha = edgePulse * wobble * (0.16f - layer * 0.04f);
                    Color col = ember * alpha;
                    spriteBatch.Draw(px, new Rectangle(0, 0, w, thick), col);
                    spriteBatch.Draw(px, new Rectangle(0, h - thick, w, thick), col);
                    spriteBatch.Draw(px, new Rectangle(0, 0, thick, h), col);
                    spriteBatch.Draw(px, new Rectangle(w - thick, 0, thick, h), col);
                }
            }
            //死亡 impact-frame：全屏白闪（一场戏只有一次）
            if (whiteFlash > 0.01f) {
                spriteBatch.Draw(px, new Rectangle(0, 0, w, h),
                    Color.White * (whiteFlash * 0.85f));
            }
            spriteBatch.End();
        }
    }
}
