using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>动能阻尼托，命中蓄动能，过半免击退，受击震地反击</summary>
    internal sealed class KineticDamperModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //阻尼岩金
        public override Color TintColor => new(230, 190, 110);

        private const float ChargeCap = 60f;
        private const float ReleaseThreshold = 20f;

        private float kineticCharge;
        private int prevLife = -1;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.3f;
            ctx.CritAdd += 3;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            kineticCharge = Math.Min(kineticCharge + 2f, ChargeCap);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            kineticCharge = Math.Min(kineticCharge + 0.8f, ChargeCap);
        }

        public override void OnPlayerUpdate(Player player) {
            //储备过半免击退
            if (kineticCharge >= ChargeCap * 0.5f) {
                player.noKnockback = true;
            }

            if (player.whoAmI != Main.myPlayer) {
                return;
            }

            //生命下降瞬间反震
            if (prevLife > 0 && player.statLife < prevLife && !player.dead
                && kineticCharge >= ReleaseThreshold) {
                ReleaseCounter(player);
            }
            prevLife = player.statLife;

            //慢泄压
            kineticCharge = Math.Max(kineticCharge - 0.02f, 0f);
        }

        private void ReleaseCounter(Player player) {
            float chargeRatio = kineticCharge / ChargeCap;
            kineticCharge = 0f;

            //反击伤按持握武器×储备比例
            Item held = player.HeldItem;
            int weaponDmg = held != null && held.type == SHPCOverride.ID
                ? player.GetWeaponDamage(held) : 30;
            int dmg = Math.Max((int)(weaponDmg * (1.2f + chargeRatio * 2.3f)), 1);

            Projectile.NewProjectile(player.GetSource_FromThis(),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCSeismicCounterProj>(),
                dmg, 10f, player.whoAmI,
                ai0: chargeRatio);
        }
    }

    /// <summary>震地反击环，扩张波前伤害+强击退</summary>
    internal sealed class SHPCSeismicCounterProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 25;
        private static readonly Color WaveMain = new(255, 195, 100);
        private static readonly Color WaveEdge = new(150, 95, 40);

        private float ChargeRatio => Projectile.ai[0];
        private float MaxRadius => MathHelper.Lerp(160f, 260f, ChargeRatio);
        private float CurrentRadius {
            get {
                float progress = 1f - Projectile.timeLeft / (float)Lifetime;
                return MaxRadius * MathF.Sqrt(progress); //先快后慢的冲击扩张
            }
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一圈反震每个敌人只震一次
            Projectile.knockBack = 10f;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.6f, Pitch = -0.6f }, Projectile.Center);
                    //岩屑+双层热浪环
                    for (int i = 0; i < 16; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f) - Vector2.UnitY * 2f;
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                            Color.Lerp(WaveMain, WaveEdge, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.6f, 1.3f)).Configure(true, Main.rand.Next(14, 28));
                    }
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        WaveMain with { A = 0 }, 0.05f).Configure(0.05f, MaxRadius / 380f, 20);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        WaveEdge with { A = 0 }, 0.05f).Configure(0.05f, MaxRadius / 300f, 26);
                }
                SHPCNaturalFx.Shake(7f);
            }
            Lighting.AddLight(Projectile.Center, WaveMain.ToVector3() * 0.7f * (Projectile.timeLeft / (float)Lifetime));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //环形波前判定
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            float radius = CurrentRadius;
            return dist >= radius - 48f && dist <= radius + 48f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退远离玩家
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                    .RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center, vel,
                    WaveMain, Main.rand.NextFloat(0.6f, 1.1f)).Configure(WaveEdge, Main.rand.Next(12, 22));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring == null) return;
            float lifeRatio = Projectile.timeLeft / (float)Lifetime;
            float radius = CurrentRadius;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float scale = radius * 2f / ring.Width;
            //扩张环，外岩金内暗褐
            spriteBatch.Draw(ring, drawPos, null, WaveMain * lifeRatio * 0.85f, 0f,
                ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(ring, drawPos, null, WaveEdge * lifeRatio * 0.5f, 0f,
                ring.Size() * 0.5f, scale * 0.82f, SpriteEffects.None, 0f);
        }
    }
}
