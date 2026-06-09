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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 自适应瞄具（世纪之花）：持续命中同一目标会建立自适应锁定，逐步提升追踪与暴击。
    /// 当锁定目标高速移动时，瞄具会在其预测落点投下一枚短暂准星，自动朝目标补射分叉预判束。
    /// </summary>
    internal sealed class AdaptiveOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //智能跟踪洋红
        public override Color TintColor => new(255, 70, 200);

        private const int MaxStacks = 12;
        private const int ReticleThreshold = 5;
        private int _lockTarget = -1;
        private int _lockStacks;
        private int _lockTime;
        private int _reticleCd;

        public override void Apply(ref ShootContext ctx) {
            float ramp = Math.Min(_lockStacks, 6);
            ctx.HomingMul += 0.2f + ramp * 0.04f;
            ctx.CritAdd += 4 + (int)ramp;
            ctx.AttackSpeedMul += 0.03f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) => Mark(target);

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            //激光高频命中按帧节流，避免瞬间堆满锁定
            if ((int)Main.GameUpdateCount % 8 == 0) Mark(target);
        }

        private void Mark(NPC target) {
            if (target.whoAmI == _lockTarget) {
                _lockStacks = Math.Min(_lockStacks + 1, MaxStacks);
            }
            else {
                _lockTarget = target.whoAmI;
                _lockStacks = 1;
            }
            _lockTime = 90;
        }

        public override void OnPlayerUpdate(Player player) {
            if (_lockTime > 0) _lockTime--; else { _lockStacks = 0; _lockTarget = -1; }
            if (_reticleCd > 0) _reticleCd--;
            if (player.whoAmI != Main.myPlayer || _lockStacks < ReticleThreshold || _lockTarget < 0) return;

            NPC t = Main.npc[_lockTarget];
            if (t == null || !t.active || t.friendly) { _lockTarget = -1; _lockStacks = 0; return; }
            if (_reticleCd > 0) return;
            if (t.velocity.Length() < 6f) return;
            if (SHPCNaturalFx.CountOwned(player.whoAmI, ModContent.ProjectileType<SHPCPredictiveReticleProj>()) > 0) return;

            _reticleCd = 50;
            Vector2 predicted = t.Center + t.velocity * 18f;
            int dmg = Math.Max((player.HeldItem?.damage ?? 1) / 2, 1);
            Projectile.NewProjectile(player.GetSource_FromThis(), predicted, Vector2.Zero,
                ModContent.ProjectileType<SHPCPredictiveReticleProj>(), dmg, 0f, player.whoAmI, ai0: _lockTarget);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item43 with { Volume = 0.4f, Pitch = 0.4f }, predicted);
            }
        }
    }

    /// <summary>
    /// 预测准星：短暂悬停在锁定目标的预测落点，周期性朝目标补射一组分叉预判束。
    /// </summary>
    internal sealed class SHPCPredictiveReticleProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 54;
        private const int FireInterval = 12;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.rotation -= 0.12f;
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.3f, 0.8f) * 0.5f);
            int target = (int)Projectile.ai[0];
            if (Projectile.owner == Main.myPlayer && (int)Main.GameUpdateCount % FireInterval == 0 && target >= 0 && target < Main.maxNPCs) {
                NPC t = Main.npc[target];
                if (t != null && t.active && !t.friendly) {
                    Vector2 baseDir = (t.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    //分叉预判：三道小角度散开的派生束
                    for (int i = -1; i <= 1; i++) {
                        Vector2 vel = baseDir.RotatedBy(i * 0.14f) * 15f;
                        SHPCNaturalFx.SpawnDerivedBeam(Projectile, Projectile.Center, vel, Math.Max(Projectile.damage, 1), 2.2f, 0.4f, theme: 0);
                    }
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.25f, Pitch = 0.6f }, Projectile.Center);
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 screen = Projectile.Center - Main.screenPosition;
                float life = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
                Main.spriteBatch.Draw(star, screen, null, new Color(255, 90, 200, 0) * life, Projectile.rotation, star.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(star, screen, null, new Color(255, 90, 200, 0) * life, -Projectile.rotation, star.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float life = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(255, 70, 190, 0) * life, new Color(150, 20, 110, 0) * life * 0.4f, 0.5f, Projectile.rotation, 3);
        }
    }
}
