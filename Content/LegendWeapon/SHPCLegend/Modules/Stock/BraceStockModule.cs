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
    /// <summary>
    /// 支架枪托（支撑锚定）：消除散布、延长射程。右键发射时会在出膛处钉下一枚锚点，
    /// 玩家与锚点之间拉起一条稳定线，线附近的敌人被牵制减速，锚点也会周期性释放压制脉冲。
    /// </summary>
    internal sealed class BraceStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //精准钢银
        public override Color TintColor => new(160, 185, 210);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -1f;
            ctx.BeamSpeedMul += 0.5f;
            ctx.BeamLifeMul += 0.5f;
            ctx.AttackSpeedMul += -0.20f;
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int type = ModContent.ProjectileType<SHPCBraceAnchorProj>();
            if (SHPCNaturalFx.CountOwned(orb.Projectile.owner, type) >= 1) return;
            Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, Vector2.Zero, type,
                Math.Max(orb.Projectile.damage / 3, 1), 0f, orb.Projectile.owner, ai0: 240f);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.45f, Pitch = -0.2f }, orb.Projectile.Center);
            }
        }
    }

    /// <summary>
    /// 支撑锚点：钉在场上的稳定锚。与玩家之间拉出一条稳定线，沿线牵制减速敌人，并周期性释放压制脉冲。
    /// </summary>
    internal sealed class SHPCBraceAnchorProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int PulseInterval = 42;
        private const float TetherWidth = 64f;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Projectile.ai[0] > 1f) Projectile.timeLeft = (int)Projectile.ai[0];
            }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation += 0.03f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.6f, 0.75f) * 0.5f);

            //稳定线牵制：服务器权威地对沿线敌人轻度减速
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 a = Projectile.Center;
                Vector2 b = owner.Center;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC n = Main.npc[i];
                    if (!n.active || n.friendly || n.dontTakeDamage || n.boss) continue;
                    if (DistToSegment(n.Center, a, b) > TetherWidth) continue;
                    n.velocity *= 0.95f;
                }
            }

            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % PulseInterval == 0 && Projectile.owner == Main.myPlayer) {
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    Math.Max(Projectile.damage, 1), 0f, Projectile.owner, ai0: 0.18f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].localAI[2] = 80f;
                    Main.projectile[idx].usesLocalNPCImmunity = true;
                    Main.projectile[idx].localNPCHitCooldown = -1;
                }
            }
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), Vector2.Zero, new Color(170, 200, 230), Main.rand.NextFloat(0.3f, 0.6f)).Configure(new Color(90, 120, 160), Main.rand.Next(8, 14));
            }
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1f) return Vector2.Distance(p, a);
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(star, screen, null, new Color(180, 205, 235, 0), Projectile.rotation, star.Size() * 0.5f, 0.15f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Player owner = Main.player[Projectile.owner];
            Vector2 origin = glow.Size() * 0.5f;
            float life = MathHelper.Clamp(Projectile.timeLeft / 20f, 0.2f, 1f);
            //稳定线：沿玩家到锚点采样若干光点
            if (owner != null && owner.active) {
                Vector2 a = Projectile.Center;
                Vector2 b = owner.Center;
                int steps = 14;
                for (int i = 0; i <= steps; i++) {
                    Vector2 pos = Vector2.Lerp(a, b, i / (float)steps) - Main.screenPosition;
                    spriteBatch.Draw(glow, pos, null, new Color(150, 190, 230, 0) * 0.28f * life, 0f, origin, 0.16f, SpriteEffects.None, 0f);
                }
            }
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(170, 205, 240, 0) * 0.5f * life, new Color(80, 110, 150, 0) * 0.3f * life, 0.45f, 0f, 2);
        }
    }
}
