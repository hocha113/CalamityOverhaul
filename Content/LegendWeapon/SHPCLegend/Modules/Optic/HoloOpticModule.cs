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
    /// 全息瞄具（海瘟兽）：开火时在鼠标附近有节奏地投影全息准星。左键命中后会向最近的准星折返，
    /// 再从准星朝另一名敌人发出短束，制造拐弯交叉火力。
    /// </summary>
    internal sealed class HoloOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //全息投影湖蓝
        public override Color TintColor => new(50, 200, 255);

        private const int MaxDecoys = 3;
        private const float DecoySpacing = 120f;
        private int _placeCd;
        private int _hitThrottle;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.55f;
            ctx.AttackSpeedMul += 0.1f;
            ctx.ManaCostMul += 0.18f;
        }

        public override void OnPlayerUpdate(Player player) {
            if (_placeCd > 0) _placeCd--;
            if (player.whoAmI != Main.myPlayer) return;
            //仅在持握并开火时铺设投影
            if (player.channel || player.controlUseItem) {
                if (_placeCd <= 0) {
                    int type = ModContent.ProjectileType<SHPCHoloDecoyProj>();
                    Vector2 spot = Main.MouseWorld + Main.rand.NextVector2Circular(40f, 40f);
                    if (SHPCNaturalFx.CountOwned(player.whoAmI, type) < MaxDecoys
                        && !SHPCNaturalFx.HasOwnedNear(player.whoAmI, type, spot, DecoySpacing)) {
                        _placeCd = 26;
                        Projectile.NewProjectile(player.GetSource_FromThis(), spot, Vector2.Zero, type,
                            1, 0f, player.whoAmI);
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Item43 with { Volume = 0.3f, Pitch = 0.7f }, spot);
                        }
                    }
                }
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if (++_hitThrottle < 2) return;
            _hitThrottle = 0;
            Ricochet(beam.Projectile, target);
        }

        private static void Ricochet(Projectile src, NPC hitTarget) {
            int type = ModContent.ProjectileType<SHPCHoloDecoyProj>();
            Projectile decoy = null;
            float best = 520f * 520f;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != src.owner || p.type != type) continue;
                float d = Vector2.DistanceSquared(p.Center, hitTarget.Center);
                if (d < best) { best = d; decoy = p; }
            }
            if (decoy == null) return;
            //从准星朝另一名敌人折返出短束
            NPC next = FindOtherTarget(decoy.Center, hitTarget.whoAmI, 700f);
            Vector2 dir = next != null
                ? (next.Center - decoy.Center).SafeNormalize(Vector2.UnitX)
                : (hitTarget.Center - decoy.Center).SafeNormalize(Vector2.UnitX);
            SHPCNaturalFx.SpawnDerivedBeam(decoy, decoy.Center, dir * 14f, Math.Max(src.damage / 2, 1), 2f, 0.45f, theme: 0);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item43 with { Volume = 0.3f, Pitch = 0.2f }, decoy.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(decoy.Center, Vector2.Zero, new Color(60, 210, 255, 0), 0.05f).Configure(0.05f, 0.28f, 12);
            }
        }

        private static NPC FindOtherTarget(Vector2 from, int excludeWho, float range) {
            NPC best = null;
            float bestD = range * range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.whoAmI == excludeWho || n.dontTakeDamage) continue;
                if (!n.CanBeChasedBy()) continue;
                float d = Vector2.DistanceSquared(n.Center, from);
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }
    }

    /// <summary>
    /// 全息准星：在鼠标附近短暂存在的折返锚点，本身不造成伤害，仅作为光束折返的发射点。
    /// </summary>
    internal sealed class SHPCHoloDecoyProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 150;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
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
            Projectile.rotation += 0.05f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.8f, 1f) * 0.4f);
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 6 == 0) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f), Vector2.Zero, new Color(80, 210, 255), Main.rand.NextFloat(0.3f, 0.6f)).Configure(new Color(20, 120, 200), Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 screen = Projectile.Center - Main.screenPosition;
                //全息闪烁
                float flicker = 0.6f + 0.4f * MathF.Sin((float)Main.timeForVisualEffects * 0.6f + Projectile.whoAmI);
                Main.spriteBatch.Draw(star, screen, null, new Color(70, 210, 255, 0) * flicker, Projectile.rotation, star.Size() * 0.5f, 0.14f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float flicker = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.5f + Projectile.whoAmI);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(50, 190, 255, 0) * 0.5f * flicker, new Color(20, 90, 160, 0) * 0.3f * flicker, 0.42f, 0f, 2);
        }
    }
}
