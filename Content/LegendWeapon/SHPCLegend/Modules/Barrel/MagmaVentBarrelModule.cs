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
    /// 岩浆枪管：命中与消亡处留下熔岩喷口，周期喷发形成区域封锁。
    /// </summary>
    internal sealed class MagmaVentBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 105, 30);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.05f;
            ctx.HomingMul += -0.28f;
            ctx.BeamSpeedMul += -0.08f;
            ctx.ManaCostMul += 0.3f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnVent(beam, target.Bottom, Math.Max(damageDone / 4, 1));
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived) return;
            SpawnVent(beam, beam.Projectile.Center, Math.Max(beam.Projectile.damage / 4, 1));
        }

        private static void SpawnVent(CyberTraceBeamProj beam, Vector2 center, int damage) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<SHPCMagmaVentProj>(),
                damage, 0f, beam.Projectile.owner);
        }
    }

    /// <summary>
    /// 熔岩喷口：底部 DiffusionCircle 脉冲、火柱用 Fire/Fog 双层 additive，喷发瞬间触发屏幕震动与火焰粒子
    /// </summary>
    internal sealed class SHPCMagmaVentProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 150;
        private const int PulseInterval = 30;

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] > 0f) {
                Projectile.localAI[0]--;
            }
            int age = Lifetime - Projectile.timeLeft;
            Tile tile = Framing.GetTileSafely(Projectile.Center.ToTileCoordinates());
            bool nearLava = Projectile.Center.Y / 16f > Main.UnderworldLayer
                || (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava);
            int interval = nearLava ? 20 : PulseInterval;
            if (age % interval == 0 && age > 0) {
                EruptionPulse(nearLava);
            }
            //岩浆环境下持续吐烟与火星，强化"喷口活着"的视觉
            if (nearLava && Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 6 == 0) {
                PRT_LavaFire ember = new PRT_LavaFire {
                    Position = Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-4f, 6f)),
                    Velocity = new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-3.2f, -1.2f)),
                    Scale = Main.rand.NextFloat(0.6f, 1.0f),
                    minLifeTime = 28,
                    maxLifeTime = 50,
                };
                PRTLoader.AddParticle(ember);
            }
            float pulse = Projectile.localAI[0] / 9f;
            Lighting.AddLight(Projectile.Center, new Vector3(1.5f, 0.5f, 0.1f) * MathHelper.Clamp(0.4f + pulse * 0.6f, 0f, 1.5f));
            Lighting.AddLight(Projectile.Center - Vector2.UnitY * 80f, new Vector3(1.0f, 0.35f, 0.08f) * pulse);
        }

        private void EruptionPulse(bool nearLava) {
            Projectile.localAI[0] = 9f;
            if (Main.netMode == NetmodeID.Server) return;
            //熔岩 / 地狱火粒子混合
            for (int i = 0; i < 6; i++) {
                PRT_LavaFire lf = new PRT_LavaFire {
                    Position = Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-2f, 8f)),
                    Velocity = new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), Main.rand.NextFloat(-9f, -4f)),
                    Scale = Main.rand.NextFloat(0.9f, 1.6f),
                    minLifeTime = 40,
                    maxLifeTime = 90,
                };
                PRTLoader.AddParticle(lf);
            }
            for (int i = 0; i < 6; i++) {
                PRT_HellFlame hf = new PRT_HellFlame {
                    Position = Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-12f, 4f)),
                    Velocity = new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-7f, -3f)),
                    Scale = Main.rand.NextFloat(0.7f, 1.2f),
                };
                PRTLoader.AddParticle(hf);
            }
            //地面冲击环：用 PRT_StarPulseRing 横向压扁形成热浪环
            PRTLoader.AddParticle(new PRT_StarPulseRing(
                Projectile.Center + Vector2.UnitY * 4f, Vector2.Zero,
                new Color(255, 140, 50, 0), 0.05f, 0.55f, 22));
            //方块碎屑火星
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-9f, -2f));
                PRTLoader.AddParticle(new PRT_Spark(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), 0f), vel, true,
                    Main.rand.Next(20, 38), Main.rand.NextFloat(0.5f, 1.2f),
                    Color.Lerp(new Color(255, 220, 130), new Color(255, 90, 25), Main.rand.NextFloat())));
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = -0.2f }, Projectile.Center);
            if (nearLava) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
            }
            SHPCNaturalFx.Shake(3.5f);
        }

        public override bool? CanDamage() => Projectile.localAI[0] > 0f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float width = 28f + Projectile.localAI[0] * 2.5f;
            float point = 0f;
            Vector2 top = Projectile.Center - Vector2.UnitY * 190f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, top, width, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.SoftGlow?.Value;
            if (ring == null) return false;
            float pulse = MathHelper.Clamp(Projectile.localAI[0] / 9f, 0f, 1f);
            //地面口部脉冲圆环
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float ringScale = 1.32f + pulse * 0.45f;
            Color ringInner = new Color(255, 150, 60, 0) * (0.55f + pulse * 0.45f);
            Color ringOuter = new Color(120, 30, 10, 0) * (0.4f + pulse * 0.4f);
            for (int i = 0; i < 6; i++) {
                SHPCNaturalFx.GlowLayered(Main.spriteBatch, ring, baseScreen - new Vector2(0, i * 10), ringInner, ringOuter, ringScale, 0f, 3);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float pulse = MathHelper.Clamp(Projectile.localAI[0] / 9f, 0f, 1f);
            float life = MathHelper.Clamp(Projectile.timeLeft / 60f, 0f, 1f);
            float intensity = 0.4f + pulse * 0.6f;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;

            //火柱本体：Fire 拉伸 + Fog 拖尾
            Texture2D fire = CWRAsset.Fire?.Value;
            if (fire != null) {
                //Fire 是序列帧 4x4，取一格
                int frameW = fire.Width / 4;
                int frameH = fire.Height / 4;
                int idx = (int)(Main.GameUpdateCount / 4) % 16;
                Rectangle frame = new(frameW * (idx % 4), frameH * (idx / 4), frameW, frameH);
                Vector2 origin = new(frameW * 0.5f, frameH);
                Color fireCol = new Color(255, 140, 50, 0) * intensity * life;
                spriteBatch.Draw(fire, baseScreen, frame, fireCol, 0f, origin,
                    new Vector2(0.7f, 6f), SpriteEffects.None, 0f);
                spriteBatch.Draw(fire, baseScreen, frame, new Color(255, 230, 160, 0) * intensity * life * 0.7f,
                    0f, origin, new Vector2(0.45f, 4.4f), SpriteEffects.None, 0f);
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 origin = fog.Size() * 0.5f;
                for (int i = 0; i < 5; i++) {
                    float t = i / 4f;
                    Color smokeCol = Color.Lerp(new Color(200, 90, 30, 0), new Color(60, 30, 30, 0), t)
                        * intensity * life * 0.7f;
                    Vector2 offset = new(MathF.Sin((float)Main.timeForVisualEffects * 0.05f + t * 6f) * 12f,
                        -t * 110f - 20f);
                    spriteBatch.Draw(fog, baseScreen + offset, null, smokeCol, t * 1.2f, origin,
                        0.5f + t * 0.7f, SpriteEffects.None, 0f);
                }
            }

            //底部多层 SoftGlow，颜色 (255,115,25)→(130,30,10)
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color inner = new Color(255, 200, 110, 0) * intensity * life;
                Color outer = new Color(130, 30, 10, 0) * intensity * life * 0.5f;
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 1.6f, 0f, 4);
            }
        }
    }
}
