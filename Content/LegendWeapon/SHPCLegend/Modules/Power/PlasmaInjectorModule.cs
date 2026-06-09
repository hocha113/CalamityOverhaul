using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 等离子注入器（至尊灾厄）：终局"注入-激活-绽放"循环。
    /// 右键球飞行时向两侧喷射等离子针，命中给敌人注入种子；
    /// 左键/激光命中带种子的敌人或右键引爆时，种子坍缩成持续灼烧的等离子花。
    /// 旧版"聚束伤害"为死属性，已移除。
    /// </summary>
    internal sealed class PlasmaInjectorModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //等离子注入粉紫
        public override Color TintColor => new(255, 100, 220);

        private const int NeedleInterval = 11;
        private const float BloomRange = 480f;

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += 0.6f;
            ctx.ChargeTimeMul += 0.36f;
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int frame = (int)Main.GameUpdateCount + orb.Projectile.whoAmI;
            if (frame % NeedleInterval != 0) return;
            Vector2 fwd = orb.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = fwd.RotatedBy(MathHelper.PiOver2);
            int needleDmg = Math.Max(orb.Projectile.damage / 4, 1);
            for (int s = -1; s <= 1; s += 2) {
                Vector2 vel = (perp * s * Main.rand.NextFloat(5.5f, 7.5f)) + fwd * Main.rand.NextFloat(1f, 3f);
                Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    orb.Projectile.Center, vel,
                    ModContent.ProjectileType<SHPCPlasmaNeedleProj>(),
                    needleDmg, 0f, orb.Projectile.owner);
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            TryBloom(beam.Projectile, target, Math.Max(beam.Projectile.damage / 2, 1));
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            TryBloom(laser.Projectile, target, Math.Max(laser.Projectile.damage, 1));
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            List<NPC> seeded = SHPCNPCEffects.CollectPlasmaSeedTargets(orb.Projectile.owner, orb.Projectile.Center, BloomRange, 6);
            foreach (NPC npc in seeded) {
                if (npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                    eff.PlasmaSeedTime = 0;
                }
                SpawnBloom(orb.Projectile, npc.Center, Math.Max(orb.Projectile.damage / 3, 1));
            }
        }

        private static void TryBloom(Projectile src, NPC target, int dmg) {
            if (src.owner != Main.myPlayer) return;
            if (!target.TryGetGlobalNPC(out SHPCNPCEffects eff)) return;
            if (eff.PlasmaSeedTime <= 0 || eff.PlasmaSeedOwner != src.owner) return;
            eff.PlasmaSeedTime = 0;
            SpawnBloom(src, target.Center, dmg);
        }

        private static void SpawnBloom(Projectile src, Vector2 pos, int dmg) {
            Projectile.NewProjectile(src.GetSource_FromThis(), pos, Vector2.Zero,
                ModContent.ProjectileType<SHPCPlasmaBloomProj>(),
                dmg, 0f, src.owner);
        }
    }

    /// <summary>
    /// 等离子针：从能量球轨迹两侧射出的细针，轻度追踪，命中给目标注入等离子种子。
    /// </summary>
    internal sealed class SHPCPlasmaNeedleProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int SeedTime = 420;
        private static readonly Color Tint = new(255, 110, 225);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 50;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            if (Projectile.localAI[0] > 8f) {
                NPC target = Projectile.Center.FindClosestNPC(360f, false, true);
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
                }
            }
            Projectile.localAI[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Tint.ToVector3() * 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer && target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyPlasmaSeed(SeedTime, Projectile.owner);
            }
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center, vel, Tint, Main.rand.NextFloat(0.5f, 1.0f)).Configure(new Color(180, 30, 160), Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float life = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, Tint * life, new Color(120, 20, 120, 0) * life * 0.4f, 0.36f, 0f, 2);
        }
    }

    /// <summary>
    /// 等离子花：种子坍缩后在原地绽放的短寿命范围灼烧，周期性对范围内敌人造成伤害。
    /// </summary>
    internal sealed class SHPCPlasmaBloomProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 150;
        private const float Radius = 130f;

        public override void SetDefaults() {
            Projectile.width = (int)(Radius * 2f);
            Projectile.height = (int)(Radius * 2f);
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(255, 110, 225, 0), 0.05f).Configure(0.05f, Radius / 320f, 24);
                }
            }
            Projectile.velocity = Vector2.Zero;
            float life = Projectile.timeLeft / (float)Lifetime;
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.4f, 0.85f) * life);
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 4 == 0) {
                Vector2 off = Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.8f);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + off, Main.rand.NextVector2Circular(1.2f, 1.2f), new Color(255, 130, 230), Main.rand.NextFloat(0.6f, 1.4f)).Configure(new Color(170, 20, 150), Main.rand.Next(14, 26));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < Radius;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float dist = Vector2.Distance(Projectile.Center, target.Center);
            float falloff = 1f - (dist / Radius) * 0.4f;
            modifiers.FinalDamage *= MathHelper.Clamp(falloff, 0.6f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float life = Projectile.timeLeft / (float)Lifetime;
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            float scale = (Radius * 2f) / glow.Width;
            spriteBatch.Draw(glow, screen, null, new Color(255, 90, 210, 0) * life * 0.5f * pulse, 0f, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, screen, null, new Color(255, 180, 240, 0) * life * 0.3f * pulse, 0f, origin, scale * 0.55f, SpriteEffects.None, 0f);
        }
    }
}
