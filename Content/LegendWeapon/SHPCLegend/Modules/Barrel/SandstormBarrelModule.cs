using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 砂暴枪管：连续光束在前方卷起砂幕，磨蚀敌人并削弱穿过的敌对弹幕。
    /// </summary>
    internal sealed class SandstormBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(220, 180, 90);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.08f;
            ctx.DamageMul += -0.06f;
            ctx.BeamExtraPierce += 1;
            ctx.ManaCostMul += 0.4f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % 24 != 0) return;
            Vector2 pos = beam.Projectile.Center + beam.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 42f;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                pos, beam.Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.5f,
                ModContent.ProjectileType<SHPCSandCurtainProj>(),
                Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    /// <summary>
    /// 砂暴幕：Cyclone 旋涡 + Airflow 流场 + Fog 体积，磨蚀敌方弹幕时投出 GammaIonize 标记，玩家在内时附微震
    /// </summary>
    internal sealed class SHPCSandCurtainProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const float Radius = 120f;
        private float radius => 60f;

        public override void SetDefaults() {
            Projectile.width = (int)(radius * 2);
            Projectile.height = (int)(radius * 2);
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            Projectile.velocity *= 0.92f;
            float radiusSq = radius * radius;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.boss) continue;
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radiusSq) continue;
                npc.velocity *= 0.92f;
                if (Main.GameUpdateCount % 20 == 0) {
                    npc.SimpleStrikeNPC(Math.Max(Projectile.damage / 8, 1), 0, false, 0f, DamageClass.Magic, false, 0f, true);
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || !hostile.hostile || hostile.friendly) continue;
                if (Vector2.DistanceSquared(hostile.Center, Projectile.Center) > radiusSq) continue;
                hostile.velocity *= 0.96f;
                //偶发标记：让玩家看到敌方弹幕被削弱
                if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 12 == 0 && Main.rand.NextBool(2)) {
                    PRTLoader.AddParticle(new PRT_GammaIonize(
                        hostile.Center, Vector2.Zero,
                        new Color(255, 200, 110), 0.6f, 16, Main.rand.NextFloat()));
                }
            }
            //玩家身处砂幕：每帧低强度震动
            Player local = Main.LocalPlayer;
            if (local != null && local.active && Vector2.DistanceSquared(local.Center, Projectile.Center) < radiusSq) {
                SHPCNaturalFx.Shake(0.4f);
            }
            //粒子发射：砂色 PRT_Smoke + 偶发火星
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.GameUpdateCount % 3 == 0) {
                PRTLoader.AddParticle(new PRT_Smoke(
                    Projectile.Center + Main.rand.NextVector2Circular(radius * 0.8f, radius * 0.6f),
                    Main.rand.NextVector2Circular(2.5f, 1.2f),
                    new Color(225, 190, 110), Main.rand.Next(28, 50),
                    Main.rand.NextFloat(0.5f, 0.95f), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f), false));
            }
            if (Main.GameUpdateCount % 6 == 0) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                PRTLoader.AddParticle(new PRT_Spark(
                    Projectile.Center + Main.rand.NextVector2Circular(radius * 0.5f, radius * 0.5f), vel, false,
                    Main.rand.Next(12, 22), Main.rand.NextFloat(0.4f, 0.8f),
                    Color.Lerp(new Color(255, 220, 130), new Color(160, 90, 30), Main.rand.NextFloat())));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //Cyclone 与 Airflow 旋转飘移堆叠成砂幕主体（AlphaBlend pass，颜色低饱和暖沙）
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float life = MathHelper.Clamp(Projectile.timeLeft / 90f, 0f, 1f);
            float fadeIn = MathHelper.Clamp((90 - Projectile.timeLeft) / 12f, 0f, 1f);
            float alpha = MathHelper.Clamp(fadeIn * life, 0f, 1f);
            float t = (float)Main.timeForVisualEffects * 0.04f;

            //Cyclone：底部大旋涡
            Texture2D cyclone = CWRAsset.Cyclone?.Value;
            if (cyclone != null) {
                Vector2 origin = cyclone.Size() * 0.5f;
                Color c = new Color(220, 185, 110, 0) * alpha * 0.55f;
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c, t * 1.4f, origin, radius / cyclone.Width * 2.4f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c * 0.6f, -t * 0.7f, origin, radius / cyclone.Width * 1.7f, SpriteEffects.None, 0f);
            }
            //6 张 Airflow 旋转飘移
            Texture2D airflow = CWRAsset.Airflow?.Value;
            if (airflow != null) {
                Vector2 origin = airflow.Size() * 0.5f;
                for (int i = 0; i < 6; i++) {
                    float a = i * (MathHelper.TwoPi / 6f) + t * (i % 2 == 0 ? 1f : -0.6f);
                    Vector2 offset = a.ToRotationVector2() * radius * 0.5f;
                    Color c = new Color(225, 190, 110, 0) * alpha * 0.4f;
                    Main.spriteBatch.Draw(airflow, baseScreen + offset, null, c,
                        a + t * 0.6f, origin, radius / airflow.Width * 1.5f, SpriteEffects.None, 0f);
                }
            }
            //12 张 Fog 体积，多帧不同种子
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 origin = fog.Size() * 0.5f;
                int seed = Projectile.whoAmI * 7919;
                for (int i = 0; i < 12; i++) {
                    float fa = (seed + i * 173) % 360 * MathHelper.Pi / 180f + t * 0.4f;
                    float fr = ((seed + i * 211) % 100) / 100f;
                    Vector2 offset = fa.ToRotationVector2() * radius * (0.3f + fr * 0.7f);
                    Color c = new Color(225, 195, 130, 0) * alpha * 0.35f;
                    Main.spriteBatch.Draw(fog, baseScreen + offset, null, c,
                        fa + t * 0.3f * (i % 3 - 1), origin, 0.5f + fr * 0.7f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //Additive：暖色发光中心，强化"沙磨阳光"质感
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float life = MathHelper.Clamp(Projectile.timeLeft / 90f, 0f, 1f);
            Color inner = new Color(255, 200, 130, 0) * life * 0.5f;
            Color outer = new Color(160, 90, 30, 0) * life * 0.25f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, radius / 32f * 0.8f, 0f, 3);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = -0.4f }, Projectile.Center);
        }
    }
}
