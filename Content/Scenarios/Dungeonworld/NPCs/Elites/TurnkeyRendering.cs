using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 沉波狱吏水面层渲染（TurnkeyRipple.fx 消费端，Weight=1.690，C3 频段 1.690–1.699）。
    /// 画在 EndEntityDraw（实体画完之后）：白沫尾流/中心隆起/水下暗影透镜/暴起沸腾柱
    /// 叠在水体渲染之上，读作"水面被搅动"而不是怪身上的贴片。
    /// 世界锚定：噪声以 uWorldX0 世界坐标采样，quad 跟身走而水纹不跟（水是水、它是它）。
    /// AlphaBlend 批（暗影透镜要真遮挡，加色批画不出暗）。
    /// 着色器缺编走 SoftGlow 双层横拉白沫回退（涟漪是"水下有它"的视觉承诺，禁无形）。
    /// 门禁自然自闭：Enabled=false 时 DrownedTurnkey 不加载，场上不可能有该型 NPC。
    /// </summary>
    internal class TurnkeyWaterlineRender : RenderHandle
    {
        public override float Weight => 1.690f;

        //==================== 画布契约（与 TurnkeyRipple.fx 头注同源）====================

        /// <summary>quad 世界像素宽：暴起触发半径 180×2 + 尾流余量 200</summary>
        internal const float QuadW = 560f;
        /// <summary>quad 世界像素高：水上白沫带 70 + 水下暗影/气泡柱 130</summary>
        internal const float QuadH = 200f;
        /// <summary>水面在 quad 内的 v（上 70px 留给隆起与飞沫）</summary>
        internal const float WaterV = 70f / QuadH;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || Main.dedServ) {
                return;
            }
            int type = ModContent.NPCType<DrownedTurnkey>();
            if (type <= 0) {
                return;
            }
            bool any = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type
                    && npc.ModNPC is DrownedTurnkey dt
                    && dt.RenderRippleRow > 0 && dt.RenderEnv > 0.02f) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect fx = EffectLoader.TurnkeyRipple?.Value;
            Texture2D px = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || px == null || noise == null || px.IsDisposed) {
                DrawFallbackRipples(spriteBatch, type);
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            float time = (float)Main.timeForVisualEffects / 60f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type
                    || npc.ModNPC is not DrownedTurnkey dt
                    || dt.RenderRippleRow <= 0 || dt.RenderEnv <= 0.02f) {
                    continue;
                }
                float waterY = dt.RenderRippleRow * 16f;
                float centerX = dt.RenderCenterX;
                Vector2 quadTopLeft = new(centerX - QuadW * 0.5f, waterY - WaterV * QuadH);

                //共享参数化 shader：每次调用全参数重设
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uQuadSize"]?.SetValue(new Vector2(QuadW, QuadH));
                fx.Parameters["uWorldX0"]?.SetValue(quadTopLeft.X);
                fx.Parameters["uCenterX"]?.SetValue(centerX);
                fx.Parameters["uWaterV"]?.SetValue(WaterV);
                fx.Parameters["uSpeed"]?.SetValue(npc.velocity.X);
                fx.Parameters["uEnv"]?.SetValue(dt.RenderEnv);
                fx.Parameters["uThreat"]?.SetValue(dt.RenderThreat);
                fx.Parameters["uBoil"]?.SetValue(dt.RenderBoil);
                fx.Parameters["uQuiet"]?.SetValue(dt.RenderQuiet);
                fx.Parameters["uSeed"]?.SetValue(dt.RenderSeed);
                fx.CurrentTechnique.Passes[0].Apply();

                spriteBatch.Draw(px, quadTopLeft - Main.screenPosition, null, Color.White, 0f,
                    Vector2.Zero, new Vector2(QuadW / px.Width, QuadH / px.Height),
                    SpriteEffects.None, 0f);
            }
            spriteBatch.End();
            //设备槽位归还（帧内邻居泄漏由各绘制点自守）
            gd.Textures[1] = null;
        }

        /// <summary>CPU 回退：SoftGlow 双层横拉白沫（旧实现降格保命），随速各向异性拉长</summary>
        private static void DrawFallbackRipples(SpriteBatch spriteBatch, int type) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;
            Color foam = new(200, 228, 222);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type
                    || npc.ModNPC is not DrownedTurnkey dt
                    || dt.RenderRippleRow <= 0 || dt.RenderEnv <= 0.02f) {
                    continue;
                }
                Vector2 pos = new(dt.RenderCenterX, dt.RenderRippleRow * 16f + 4f);
                float stretch = (40f + Math.Abs(npc.velocity.X) * 10f) * (1f + dt.RenderBoil * 0.6f);
                float wobble = 0.85f + 0.15f * MathF.Sin(dt.RenderSeed + (float)Main.timeForVisualEffects * 0.0033f);
                float a = dt.RenderEnv * (0.6f + dt.RenderThreat * 0.4f);
                spriteBatch.Draw(glow, pos - Main.screenPosition, null, foam * (0.30f * wobble * a), 0f,
                    gOrigin, new Vector2(stretch * 2f / glow.Width, 5f * 2f / glow.Height), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, pos - Main.screenPosition, null, foam * (0.18f * wobble * a), 0f,
                    gOrigin, new Vector2(stretch * 1.4f * 2f / glow.Width, 3f * 2f / glow.Height), SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
