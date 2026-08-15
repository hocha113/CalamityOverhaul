using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering
{
    /// <summary>主题色+通用视觉生成助手，全部客户端</summary>
    internal static class PlanteraRenderHelper
    {
        /// <summary>一阶段荧光查特绿</summary>
        internal static Color GlowGreen => new(150, 255, 100);
        /// <summary>二阶段荧光品红</summary>
        internal static Color GlowMagenta => new(255, 100, 180);
        /// <summary>一阶段花瓣粉</summary>
        internal static Color PetalPink => new(240, 120, 160);
        /// <summary>二阶段肉色深红</summary>
        internal static Color FleshCrimson => new(190, 60, 110);
        /// <summary>孢子毒绿</summary>
        internal static Color SporeGreen => new(140, 230, 60);

        internal static Color GlowByPhase(bool phase2) => phase2 ? GlowMagenta : GlowGreen;
        internal static Color PetalByPhase(bool phase2) => phase2 ? FleshCrimson : PetalPink;

        /// <summary>是否屏内(含边距)，服务器恒否</summary>
        internal static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            if (VaultUtils.isServer) {
                return false;
            }
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>本体后侧蓄力特效：吸入荧光尘+呼吸辉团</summary>
        internal static void DrawChargeEffect(SpriteBatch spriteBatch, PlanteraStateContext ctx) {
            if (!ctx.IsCharging || ctx.ChargeProgress <= 0.02f) {
                return;
            }

            NPC npc = ctx.Npc;
            float progress = ctx.ChargeProgress;
            Color glow = GlowByPhase(ctx.IsPhase2);
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Vector2 pos = npc.Center - Main.screenPosition;

            //蓄力辉团(衬底层，本体覆盖其上)
            float breath = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
            float scale = (0.8f + progress * 1.6f) * breath;
            spriteBatch.Draw(soft, pos, null, glow with { A = 0 } * (0.5f * progress),
                0f, soft.Size() / 2f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(soft, pos, null, Color.White with { A = 0 } * (0.25f * progress * progress),
                0f, soft.Size() / 2f, scale * 0.45f, SpriteEffects.None, 0f);
        }

        /// <summary>蓄力吸入粒子，72%后静默(临爆收声)；状态每帧调</summary>
        internal static void SpawnChargeIntake(PlanteraStateContext ctx, float progress) {
            if (VaultUtils.isServer || progress > 0.72f) {
                return;
            }
            NPC npc = ctx.Npc;
            if (!Main.rand.NextBool(2)) {
                return;
            }

            //径向吸入+切向卷旋两族
            Vector2 spawnPos = npc.Center + Main.rand.NextVector2CircularEdge(160f, 150f) * Main.rand.NextFloat(0.7f, 1.3f);
            Vector2 inward = (npc.Center - spawnPos) * 0.055f;
            if (Main.rand.NextBool(3)) {
                inward = inward.RotatedBy(MathHelper.PiOver2 * 0.8f);
            }
            PRTLoader.NewParticle<PRT_PlanteraSporeMote>(spawnPos, inward,
                GlowByPhase(ctx.IsPhase2), Main.rand.NextFloat(0.8f, 1.5f))
                ?.Converge(npc.Center).SetLife(50);
        }

        /// <summary>花瓣爆发(蜕壳/绽放/受创)</summary>
        internal static void SpawnPetalBurst(Vector2 pos, int count, float speed, bool phase2) {
            if (VaultUtils.isServer) {
                return;
            }
            Color petal = PetalByPhase(phase2);
            Color glow = GlowByPhase(phase2);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.35f, 1f) * speed;
                PRTLoader.NewParticle<PRT_PlanteraPetal>(pos + Main.rand.NextVector2Circular(26f, 26f), vel,
                    Color.Lerp(petal, Color.White, Main.rand.NextFloat(0.25f)), Main.rand.NextFloat(0.8f, 1.5f))
                    ?.Configure(Main.rand.Next(55, 100), Main.rand.NextFloat(0.7f, 1.2f), glow);
            }
        }

        /// <summary>孢子雾轻爆(荧光尘+毒绿光+草屑)</summary>
        internal static void SpawnSporePuff(Vector2 pos, float scale) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < (int)(7 * scale); i++) {
                PRTLoader.NewParticle<PRT_PlanteraSporeMote>(pos + Main.rand.NextVector2Circular(18f, 18f),
                    Main.rand.NextVector2Circular(2.2f, 2f) * scale, SporeGreen,
                    Main.rand.NextFloat(0.9f, 1.8f) * scale)?.SetLife(Main.rand.Next(45, 90));
            }
            for (int i = 0; i < (int)(5 * scale); i++) {
                Dust dust = Dust.NewDustDirect(pos - new Vector2(12f, 12f), 24, 24,
                    DustID.Plantera_Green, 0f, 0f, 120, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(3f, 3f) * scale;
            }
            Lighting.AddLight(pos, SporeGreen.ToVector3() * 0.5f * scale);
        }

        /// <summary>钩爪嵌入尘爆(碎土+草屑)</summary>
        internal static void SpawnAnchorImpact(Vector2 pos, Vector2 dir) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustDirect(pos - new Vector2(8f, 8f), 16, 16,
                    Main.rand.NextBool() ? DustID.Dirt : DustID.JungleGrass, 0f, 0f, 90, default, Main.rand.NextFloat(1f, 1.8f));
                dust.velocity = (-dir).RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(1.5f, 5f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>环境孢子微光(主控闲时装点)</summary>
        internal static void SpawnAmbientMote(Vector2 pos, bool phase2) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_PlanteraSporeMote>(pos,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.55f, -0.2f)),
                GlowByPhase(phase2) * 0.75f, Main.rand.NextFloat(0.5f, 1.1f))
                ?.SetLife(Main.rand.Next(80, 150));
        }
    }
}
