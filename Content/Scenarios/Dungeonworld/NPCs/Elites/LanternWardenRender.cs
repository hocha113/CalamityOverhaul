using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 提灯巡守灯锥渲染（LanternWardenCone.fx 消费端，Weight=1.671，C1 频段 1.670–1.679）。
    /// 画在实体层下方（物块后、NPC 前）：玩家与怪物剪影压在光域上，光锥读作空间里的体积光
    /// 而不是盖在人身上的贴片。逐巡守 Immediate+Additive 全参数上载（共享参数化纪律）。
    /// 着色器缺编时走 SoftGlow 三段楔形 CPU 回退（灯锥是探测判定的视觉承诺，禁无形）。
    /// 门禁自然自闭：Enabled=false 时 LanternWarden 不加载，场上不可能有该型 NPC。
    /// </summary>
    internal class LanternWardenRender : RenderHandle
    {
        /// <summary>与实体树同门禁：整树未验收不进游戏，渲染层也不注册</summary>
        public override bool CanLoad() => DungeonworldEliteGate.Enabled;

        public override float Weight => 1.671f;

        //==================== 画布契约（与 LanternWardenCone.fx 头注同源）====================

        /// <summary>quad 长（世界px）：判定半径 340 + 45 撕散余量；shader 端 uReach 钳 0.93，
        /// 满 reach 时亮体前沿 ≈358px ≥ 判定 340px（公平：光照到哪判到哪）</summary>
        internal const float ConeQuadLen = LanternWarden.ConeRange + 45f;
        /// <summary>quad 全宽（世界px）：2×(端半宽 385×tan27°+根6px)×1.17 摆动余量 ≈ 470</summary>
        internal const float ConeQuadWide = 470f;
        /// <summary>锥半角正切 tan27°（与 LanternWarden.ConeHalfCos=0.891 同一半角）</summary>
        internal const float ConeSpreadTan = 0.5095f;

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.dedServ) {
                return;
            }
            int type = ModContent.NPCType<LanternWarden>();
            if (type <= 0) {
                return;
            }
            bool any = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type
                    && npc.ModNPC is LanternWarden w && w.ConeVisible) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect fx = EffectLoader.LanternWardenCone?.Value;
            Texture2D px = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || px == null || noise == null || px.IsDisposed) {
                DrawFallbackCones(spriteBatch, type);
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            float time = (float)Main.timeForVisualEffects / 60f;
            //origin 取左中：uv.x=0 在灯口，quad 沿锥轴旋转铺出
            Vector2 origin = new(0f, px.Height * 0.5f);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type
                    || npc.ModNPC is not LanternWarden warden || !warden.ConeVisible) {
                    continue;
                }
                //共享参数化 shader：每次调用全参数重设
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(npc.whoAmI * 0.7391f);
                fx.Parameters["uLevel"]?.SetValue(warden.RenderFlameLevel);
                fx.Parameters["uAlert"]?.SetValue(warden.RenderAlert01);
                fx.Parameters["uReach"]?.SetValue(warden.RenderConeReach01);
                fx.Parameters["uQuadLen"]?.SetValue(ConeQuadLen);
                fx.Parameters["uQuadWide"]?.SetValue(ConeQuadWide);
                fx.Parameters["uSpread"]?.SetValue(ConeSpreadTan);
                fx.CurrentTechnique.Passes[0].Apply();

                spriteBatch.Draw(px, warden.RenderLanternPos - Main.screenPosition, null,
                    Color.White * npc.Opacity, warden.RenderConeAxis.ToRotation(), origin,
                    new Vector2(ConeQuadLen / px.Width, ConeQuadWide / px.Height),
                    SpriteEffects.None, 0f);
            }
            spriteBatch.End();
            //设备槽位归还（帧内邻居泄漏由各绘制点自守，Janitor 只兜跨帧）
            graphicsDevice.Textures[1] = null;
        }

        /// <summary>
        /// CPU 回退：SoftGlow 三段渐宽渐淡楔形（旧实现降格保命）。
        /// 视觉末端 ~345px ≥ 判定 340px，公平合同在回退层同样成立
        /// </summary>
        private static void DrawFallbackCones(SpriteBatch spriteBatch, int type) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;
            float[] dist = [65f, 170f, 285f];
            float[] wide = [26f, 62f, 104f];
            float[] lum = [0.30f, 0.20f, 0.11f];

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type
                    || npc.ModNPC is not LanternWarden warden || !warden.ConeVisible) {
                    continue;
                }
                Vector2 axis = warden.RenderConeAxis;
                float rot = axis.ToRotation();
                Vector2 src = warden.RenderLanternPos;
                float strength = warden.RenderFlameLevel * npc.Opacity;
                Color coneCol = Color.Lerp(LanternWarden.LampWarm, Color.White,
                    warden.RenderAlert01 * 0.5f);
                float reach = warden.RenderConeReach01 * ConeQuadLen;
                for (int seg = 0; seg < 3; seg++) {
                    if (dist[seg] > reach) {
                        continue;
                    }
                    Vector2 p = src + axis * dist[seg];
                    spriteBatch.Draw(glow, p - Main.screenPosition, null,
                        coneCol * (lum[seg] * strength), rot, gOrigin,
                        new Vector2(120f * 2f / glow.Width, wide[seg] * 2f / glow.Height),
                        SpriteEffects.None, 0f);
                }
            }
            spriteBatch.End();
        }
    }
}
