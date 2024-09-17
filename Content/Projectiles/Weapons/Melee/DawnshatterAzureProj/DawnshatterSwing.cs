using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterSwing : ModProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 190;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.MaxUpdates = 3;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
        }

        public override bool PreAI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            return true;
        }

        public override void AI() {
            Projectile.velocity *= 0.98f;
            Projectile.rotation += 0.1f;
            Projectile.scale += 0.01f;
            BasePRT particle2 = new PRT_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), CWRUtils.randVr(3, (int)(26 * Projectile.scale))
                    , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.DarkRed)
                    , 23, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
            PRTLoader.AddParticle(particle2);
        }

        public override void OnKill(int timeLeft) {
            float spread = 180f * 0.0174f;
            double startAngle = Math.Atan2(Projectile.velocity.X, Projectile.velocity.Y) - (spread / 2);
            double deltaAngle = spread / 8f;
            double offsetAngle;
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 13; i++) {
                    offsetAngle = startAngle + (deltaAngle * (i + (i * i)) / 2f) + (32f * i);
                    _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y
                        , (float)(Math.Sin(offsetAngle) * 15f), (float)(Math.Cos(offsetAngle) * 15f)
                        , ModContent.ProjectileType<SandFire>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
                    _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y
                        , (float)(-Math.Sin(offsetAngle) * 15f), (float)(-Math.Cos(offsetAngle) * 15f)
                        , ModContent.ProjectileType<SandFire>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<DawnshatterEndOrb>(), (int)(Projectile.damage * 0.01), 0f, Projectile.owner);
            }
            Projectile.Explode(2220, Supernova.ExplosionSound with { Pitch = -0.7f });
            for (int i = 0; i < 132; i++) {
                BasePRT particle = new PRT_Light(Projectile.Center, CWRUtils.randVr(3, 116), Main.rand.NextFloat(0.3f, 0.7f), Color.OrangeRed, 12, 0.2f);
                PRTLoader.AddParticle(particle);
                BasePRT particle2 = new PRT_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), CWRUtils.randVr(3, 16)
                    , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.DarkRed)
                    , 15, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
                PRTLoader.AddParticle(particle2);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            float rot = Projectile.rotation;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = CWRUtils.GetOrig(texture, 4);
            Main.EntitySpriteDraw(texture, drawPosition, CWRUtils.GetRec(texture, Projectile.frame, 4), Color.White
                , rot, origin, Projectile.scale * 0.7f, 0, 0);
            return false;
        }
    }
}
