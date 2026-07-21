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
    /// <summary>砂暴枪管，光束卷砂幕，磨蚀敌与削弱敌弹</summary>
    internal sealed class SandstormBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(220, 180, 90);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.06f;
            ctx.DamageMul += -0.08f;
            ctx.BeamExtraPierce += 1;
            ctx.ManaCostMul += 0.48f;
        }

        //同主砂幕上限(寿命90≈最多3个)
        private const int MaxConcurrentCurtains = 3;
        //同点160px内已有则跳过
        private const float MinSpacing = 160f;
        //单束生成间隔帧
        private const int SpawnInterval = 42;

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % SpawnInterval != 0) return;
            int curtainType = ModContent.ProjectileType<SHPCSandCurtainProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, curtainType) >= MaxConcurrentCurtains) return;
            Vector2 pos = beam.Projectile.Center + beam.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 42f;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, curtainType, pos, MinSpacing)) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                pos, beam.Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.5f,
                curtainType, Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    /// <summary>砂幕，旋涡+流场+Fog，磨蚀</summary>
    internal sealed class SHPCSandCurtainProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
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

        //命中扫描，每3帧错峰
        private const int ScanInterval = 3;

        public override void AI() {
            Projectile.velocity *= 0.92f;
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ScanInterval == 0) {
                float radiusSq = radius * radius;
                bool damageTick = Main.GameUpdateCount % 20 == 0;
                int dmg = Math.Max(Projectile.damage / 8, 1);
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.boss) continue;
                    if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radiusSq) continue;
                    npc.velocity *= 0.92f;
                    if (damageTick) {
                        npc.SimpleStrikeNPC(dmg, 0, false, 0f, DamageClass.Magic, false, 0f, true);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile hostile = Main.projectile[i];
                    if (!hostile.active || !hostile.hostile || hostile.friendly) continue;
                    if (Vector2.DistanceSquared(hostile.Center, Projectile.Center) > radiusSq) continue;
                    hostile.velocity *= 0.96f;
                    if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 18 == 0 && Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_GammaIonize>(hostile.Center, Vector2.Zero, new Color(255, 200, 110), 0.6f).Configure(16, Main.rand.NextFloat());
                    }
                }
            }
            //身在砂幕，每3帧轻震
            if (frame % 3 == 0) {
                Player local = Main.LocalPlayer;
                if (local != null && local.active && Vector2.DistanceSquared(local.Center, Projectile.Center) < radius * radius) {
                    SHPCNaturalFx.Shake(0.4f);
                }
            }
            //砂烟+火星，6/12帧节流
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.GameUpdateCount % 6 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(radius * 0.8f, radius * 0.6f), Main.rand.NextVector2Circular(2.5f, 1.2f), new Color(225, 190, 110), Main.rand.NextFloat(0.5f, 0.95f)).Configure(Main.rand.Next(28, 50), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }
            if (Main.GameUpdateCount % 12 == 0) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(radius * 0.5f, radius * 0.5f), vel, Color.Lerp(new Color(255, 220, 130), new Color(160, 90, 30), Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, Main.rand.Next(12, 22));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //Cyclone+Airflow 暖沙主体
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float life = MathHelper.Clamp(Projectile.timeLeft / 90f, 0f, 1f);
            float fadeIn = MathHelper.Clamp((90 - Projectile.timeLeft) / 12f, 0f, 1f);
            float alpha = MathHelper.Clamp(fadeIn * life, 0f, 1f);
            float t = (float)Main.timeForVisualEffects * 0.04f;

            //底部大旋涡
            Texture2D cyclone = CWRAsset.Cyclone?.Value;
            if (cyclone != null) {
                Vector2 origin = cyclone.Size() * 0.5f;
                Color c = new Color(220, 185, 110, 0) * alpha * 0.55f;
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c, t * 1.4f, origin, radius / cyclone.Width * 2.4f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c * 0.6f, -t * 0.7f, origin, radius / cyclone.Width * 1.7f, SpriteEffects.None, 0f);
            }
            //4 张 Airflow
            Texture2D airflow = CWRAsset.Airflow?.Value;
            if (airflow != null) {
                Vector2 origin = airflow.Size() * 0.5f;
                const int airflowCount = 4;
                for (int i = 0; i < airflowCount; i++) {
                    float a = i * (MathHelper.TwoPi / airflowCount) + t * (i % 2 == 0 ? 1f : -0.6f);
                    Vector2 offset = a.ToRotationVector2() * radius * 0.5f;
                    Color c = new Color(225, 190, 110, 0) * alpha * 0.4f;
                    Main.spriteBatch.Draw(airflow, baseScreen + offset, null, c,
                        a + t * 0.6f, origin, radius / airflow.Width * 1.5f, SpriteEffects.None, 0f);
                }
            }
            //6 张 Fog，同种子稳帧
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 origin = fog.Size() * 0.5f;
                int seed = Projectile.whoAmI * 7919;
                const int fogCount = 6;
                for (int i = 0; i < fogCount; i++) {
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
            //暖色发光中心
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
