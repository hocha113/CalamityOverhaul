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
    /// <summary>霏的演出集中处：雾滴飞行薄雾、消散柔化与漂雾团伴生弹幕</summary>
    internal static class FuFeiFX
    {
        /// <summary>灰青身份色，定义与演出同源取此</summary>
        internal static readonly Color Accent = new(136, 158, 160);

        /// <summary>霏雾滴飞行拖薄雾：逐帧低频补一口，端本地纯表现</summary>
        internal static void DropHaze(Projectile drop) {
            if (!Main.rand.NextBool(5)) {
                return;
            }
            PRTLoader.NewParticle<PRT_KikasaInkMist>(
                drop.Center - drop.velocity * 0.4f + Main.rand.NextVector2Circular(5f, 5f),
                drop.velocity * 0.06f, Accent * 0.55f,
                Main.rand.NextFloat(0.5f, 0.7f))?.Configure(Main.rand.Next(16, 24));
        }

        /// <summary>雾滴消散柔化：一小团灰青雾盖在常规溅裂上，读作"化开"（端本地）</summary>
        internal static void SoftenDeath(Vector2 pos) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(pos + Main.rand.NextVector2Circular(10f, 8f),
                    Main.rand.NextVector2Circular(0.7f, 0.5f) - Vector2.UnitY * 0.2f,
                    Accent * 0.6f, Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(24, 34));
            }
            KikasaInk.Play(KikasaInk.InkSplash, pos, 0.24f, 0.35f, 3);
        }
    }

    /// <summary>
    /// 霏·漂雾团：雾滴消散处滞留 2 秒的一团慢雾。
    /// 判定盒即雾身（约 30 帧咬一口）；缓行在 NPC 权威端做（单机/服务器），
    /// 雾身与穿雾者的雾尾各端本地
    /// </summary>
    internal class FuFeiMistCloud : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 120;

        private float life;

        /// <summary>确定性相位：雾身漂移与叶片错相各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 116;
            Projectile.height = 84;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            life++;
            //慢雾漂移：轻微上浮加左右摇曳
            Projectile.velocity = new Vector2(
                MathF.Sin(life * 0.045f + Seed) * 0.10f, -0.12f);

            //缓行：NPC 运动权威端回拉一成位移（客户端写别人的 NPC 会拉扯回弹）
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Rectangle box = Projectile.Hitbox;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc?.active != true || npc.friendly || npc.dontTakeDamage
                        || !box.Intersects(npc.Hitbox)) {
                        continue;
                    }
                    npc.position -= npc.velocity * FuFei.CloudSlowFraction;
                }
            }

            if (Main.dedServ) {
                return;
            }
            //雾内浮粒：低频补一口，维持"活雾"的读感
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(46f, 30f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.1f, 0.4f),
                    FuFeiFX.Accent * 0.5f, Main.rand.NextFloat(0.6f, 0.9f))
                    ?.Configure(Main.rand.Next(20, 30));
            }
            //穿雾者拖雾尾（端本地）
            if (Main.rand.NextBool(6)) {
                Rectangle box = Projectile.Hitbox;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc?.active != true || npc.friendly
                        || npc.velocity.LengthSquared() < 1f || !box.Intersects(npc.Hitbox)) {
                        continue;
                    }
                    PRTLoader.NewParticle<PRT_KikasaInkMist>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.UnitX) * npc.width * 0.4f,
                        npc.velocity * 0.15f, FuFeiFX.Accent * 0.45f, 0.55f)
                        ?.Configure(Main.rand.Next(14, 20));
                }
            }
        }

        /// <summary>柔边雾身：三团错相慢旋的灰青雾瓣，呼吸涨落，入场浮现末段散尽</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float alpha = MathHelper.Clamp(life / 14f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 28f, 0f, 1f);
            if (alpha <= 0.02f) {
                return false;
            }
            float breath = 1f + 0.07f * MathF.Sin(life * 0.06f + Seed * 3f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            for (int k = 0; k < 3; k++) {
                float ang = Seed * 2f + k * 2.1f + life * 0.012f * (k % 2 == 0 ? 1f : -1.4f);
                Vector2 off = ang.ToRotationVector2() * (10f + k * 9f);
                float size = (74f + k * 16f) * breath;
                Color tone = k switch {
                    0 => new Color(52, 66, 72) * (alpha * 0.50f),
                    1 => new Color(108, 128, 134) * (alpha * 0.42f),
                    _ => new Color(168, 186, 188) * (alpha * 0.20f),
                };
                Main.EntitySpriteDraw(tex, pos + off, null, tone, ang * 0.3f, origin,
                    new Vector2(size * 1.25f, size * 0.85f) / tex.Width, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
