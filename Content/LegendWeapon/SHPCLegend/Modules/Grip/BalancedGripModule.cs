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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 平衡握把（史莱姆之神）：红/蓝双核极性。左键命中偏蓝、右键引爆偏红，
    /// 极性接近中点时进入"合相"，下一击召出一对红蓝绕射火花夹击目标。
    /// 长时间只用一种攻击会推向极端从而难以合相，鼓励左右交替。
    /// </summary>
    internal sealed class BalancedGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //平衡青铜
        public override Color TintColor => new(220, 180, 120);

        private const float EquilibriumThreshold = 0.25f;
        private const int ProcCooldown = 24;

        private float _polarity;
        private int _cooldown;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.16f;
            ctx.AttackSpeedMul += 0.04f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            Shift(-0.34f);
            TryEquilibrium(beam.Projectile, target.Center, Math.Max((int)(beam.Projectile.damage * 0.6f), 1));
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            Shift(0.5f);
            TryEquilibrium(orb.Projectile, orb.Projectile.Center, Math.Max((int)(orb.Projectile.damage * 0.5f), 1));
        }

        public override void OnPlayerUpdate(Player player) {
            if (_cooldown > 0) {
                _cooldown--;
            }
            //极性向中点缓慢回落，静止时自然再平衡
            if (_polarity > 0f) {
                _polarity = MathF.Max(_polarity - 0.01f, 0f);
            }
            else if (_polarity < 0f) {
                _polarity = MathF.Min(_polarity + 0.01f, 0f);
            }
        }

        private void Shift(float delta) {
            _polarity = MathHelper.Clamp(_polarity + delta, -1f, 1f);
        }

        private void TryEquilibrium(Projectile src, Vector2 pos, int dmg) {
            if (_cooldown > 0 || MathF.Abs(_polarity) > EquilibriumThreshold) return;
            _cooldown = ProcCooldown;
            if (src.owner != Main.myPlayer) return;
            //一对绕射火花从目标两侧夹击：ai0=0 红、ai0=1 蓝
            for (int i = 0; i < 2; i++) {
                Vector2 side = (i == 0 ? Vector2.UnitX : -Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f));
                Vector2 spawn = pos + side * 120f;
                Vector2 vel = (pos - spawn).SafeNormalize(Vector2.UnitX) * 8f;
                Projectile.NewProjectile(src.GetSource_FromThis(), spawn, vel,
                    ModContent.ProjectileType<SHPCBalanceSparkProj>(),
                    dmg, 0f, src.owner, ai0: i);
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.4f, Pitch = 0.5f }, pos);
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, new Color(230, 200, 150, 0), 0.05f).Configure(0.05f, 0.32f, 16);
            }
        }
    }

    /// <summary>
    /// 平衡火花：从目标侧翼绕射并追踪夹击的小型弹丸，红蓝双色由 ai0 区分。
    /// </summary>
    internal sealed class SHPCBalanceSparkProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private Color Tint => Projectile.ai[0] < 0.5f ? new Color(255, 90, 90) : new Color(90, 160, 255);

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            NPC target = Projectile.Center.FindClosestNPC(560f, false, true);
            if (target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Tint.ToVector3() * 0.5f);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, -Projectile.velocity * 0.2f, Tint, Main.rand.NextFloat(0.5f, 1.0f)).Configure(Tint * 0.5f, Main.rand.Next(8, 16));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f, Pitch = 0.4f }, target.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center, vel, Tint, Main.rand.NextFloat(0.6f, 1.3f)).Configure(Tint * 0.5f, Main.rand.Next(10, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float life = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, Tint * life, Tint * life * 0.3f, 0.45f, 0f, 3);
        }
    }
}
