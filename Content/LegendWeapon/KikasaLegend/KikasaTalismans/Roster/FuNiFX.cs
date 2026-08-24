using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霓的三色板与演出集中处：滴身染色流转、对应色花火、印记标点</summary>
    internal static class FuNiFX
    {
        //三色板：体色压暗保墨感，芯色亮出色相；序 0赤/1青/2紫
        private static readonly Color[] body = [
            new(156, 44, 52), new(36, 110, 124), new(108, 54, 142),
        ];
        private static readonly Color[] deep = [
            new(70, 18, 24), new(14, 48, 58), new(44, 20, 62),
        ];
        private static readonly Color[] core = [
            new(255, 158, 132), new(150, 232, 224), new(216, 160, 255),
        ];

        /// <summary>取色序芯色（花火/标点共用）</summary>
        internal static Color CoreOf(int payload) => core[Math.Clamp(payload, 0, 2)];

        /// <summary>滴身染色：按载荷取色，色相向下一色缓慢流转（绘制线程纯计算）</summary>
        internal static void PaintDrop(Projectile drop, ref KikasaDropDrawParams draw) {
            int payload = Math.Clamp(KikasaTalismanHooks.ReadTagPayload(drop.ai[2]), 0, 2);
            int next = (payload + 1) % 3;
            float flow = 0.5f + 0.5f * MathF.Sin(
                Main.GlobalTimeWrappedHourly * 4f + drop.identity * 0.7f);
            draw.Body = Color.Lerp(body[payload], body[next], flow * 0.35f);
            draw.Deep = Color.Lerp(deep[payload], deep[next], flow * 0.35f);
            draw.Core = Color.Lerp(core[payload], core[next], flow * 0.25f);
        }

        /// <summary>染色滴谢幕的对应色花火：小珠+一粒色芒，各客户端本地</summary>
        internal static void ColorBurst(Vector2 pos, int payload) {
            if (Main.dedServ) {
                return;
            }
            Color tint = CoreOf(payload);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos + Main.rand.NextVector2Circular(5f, 5f),
                    Main.rand.NextVector2Circular(2.4f, 1.8f) - Vector2.UnitY * 1.2f,
                    tint, Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_Sparkle>(pos, -Vector2.UnitY * 0.6f, tint, 0.3f)
                ?.Configure(tint * 0.6f, Main.rand.Next(12, 18), 0.1f, 0.8f);
        }

        /// <summary>赤爆演出：暖红脉冲环+火色珠雾（爆伤弹幕首帧在各端自播，旁观可见）</summary>
        internal static void RedBloomBurst(Vector2 pos, float radius) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(SoundID.Item14, pos, 0.32f, 0.35f, 4);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(pos, Vector2.Zero,
                core[0] * 0.6f, 0.08f)?.Configure(0.08f, radius / 110f, 12);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 0.6f;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos + Main.rand.NextVector2Circular(6f, 6f),
                    vel, Main.rand.NextBool() ? body[0] : core[0],
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_KikasaInkMist>(pos, -Vector2.UnitY * 0.5f,
                body[0] * 0.9f, Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(20, 30));
        }

        /// <summary>
        /// 印记标点：紫印在身侧浮两粒紫芒、青印在腿侧挂两粒垂滴，
        /// 随叠层计时渐隐（叠层已广播，旁观端同样画）
        /// </summary>
        internal static void DrawColorMarks(SpriteBatch sb, NPC npc, int stacks,
            int timerFrames, Vector2 screenPos) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float alpha = MathHelper.Clamp(timerFrames / 30f, 0f, 1f) * 0.55f;
            if (alpha <= 0.02f) {
                return;
            }
            float t = Main.GlobalTimeWrappedHourly;
            if ((stacks & FuNi.BitVuln) != 0) {
                for (int i = 0; i < 2; i++) {
                    float sway = MathF.Sin(t * 2.4f + npc.whoAmI * 1.3f + i * MathHelper.Pi);
                    Vector2 pos = npc.Center - screenPos
                        + new Vector2((i == 0 ? -1f : 1f) * (npc.width * 0.42f + 3f * sway),
                            -npc.height * 0.24f + 2f * sway);
                    sb.Draw(glow, pos, null, (core[2] with { A = 0 }) * alpha, 0f,
                        glow.Size() * 0.5f, 7f / glow.Width, SpriteEffects.None, 0f);
                }
            }
            if ((stacks & FuNi.BitSlow) != 0) {
                for (int i = 0; i < 2; i++) {
                    float drip = (t * 0.8f + i * 0.5f + npc.whoAmI * 0.21f) % 1f;
                    Vector2 pos = npc.Center - screenPos
                        + new Vector2((i == 0 ? -1f : 1f) * npc.width * 0.22f,
                            npc.height * (0.1f + 0.3f * drip));
                    sb.Draw(glow, pos, null, (core[1] with { A = 0 }) * (alpha * (1f - drip * 0.6f)),
                        0f, glow.Size() * 0.5f, 5.5f / glow.Width, SpriteEffects.None, 0f);
                }
            }
        }
    }

    /// <summary>
    /// 霓·赤滴小爆：不可见的一瞬 AoE 判定（ai[0]=判定半径 px），
    /// 首帧在各端自播赤色花火，伤害随生成包自含
    /// </summary>
    internal class FuNiBloomProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>判定半径（px），生成包 ai[0]</summary>
        private ref float Radius => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                float radius = MathHelper.Clamp(Radius <= 0f ? 66f : Radius, 30f, 140f);
                Projectile.Resize((int)(radius * 2f), (int)(radius * 2f));
                FuNiFX.RedBloomBurst(Projectile.Center, radius);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
