using CalamityMod;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj
{
    internal class StormGoddessSpearProj : ModProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<StormGoddessSpear>();

        public Color Light => Lighting.GetColor((int)(Projectile.position.X + (Projectile.width * 0.5)) / 16, (int)((Projectile.position.Y + (Projectile.height * 0.5)) / 16.0));

        public override string Texture => CWRConstant.Projectile_Melee + "StormGoddessSpearProj";

        public Player Owner => Main.player[Projectile.owner];

        public Item elementalLance => Owner.HeldItem;

        protected float HoldoutRangeMin => -24f;
        protected float HoldoutRangeMax => 96f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
            Projectile.hide = true;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            player.heldProj = Projectile.whoAmI;
            int duration = player.itemAnimationMax;
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }
            Projectile.velocity = Vector2.Normalize(Projectile.velocity);
            float halfDuration = duration * 0.5f;
            float progress = Projectile.timeLeft < halfDuration ? Projectile.timeLeft / halfDuration : (duration - Projectile.timeLeft) / halfDuration;
            if (Projectile.timeLeft == duration / 2) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity, Projectile.velocity * 15
                , ModContent.ProjectileType<StormLightning>(), (int)(Projectile.damage * 0.8f), Projectile.knockBack, Projectile.owner, Projectile.velocity.ToRotation());
                }
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < Main.rand.Next(13, 26); i++) {
                        Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(13);
                        Vector2 particleSpeed = Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)).UnitVector() * Main.rand.NextFloat(15.5f, 37.7f);
                        BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                            , 0.3f, Light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                        PRTLoader.AddParticle(energyLeak);
                    }
                }
            }
            Projectile.Center = player.MountedCenter + Vector2.SmoothStep(Projectile.velocity * HoldoutRangeMin, Projectile.velocity * HoldoutRangeMax, progress);
            Projectile.rotation = Projectile.velocity.ToRotation();
            player.direction = Math.Sign(player.position.To(Main.MouseWorld).X);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center
                , Projectile.rotation.ToRotationVector2() * Projectile.height * 3 + Projectile.Center, Projectile.width, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            const float piOver3 = MathHelper.TwoPi / 3f;
            int index = 3;
            float damageset = 0.5f;
            bool isadrenal = false;
            if (Owner.Calamity().adrenalineModeActive) {
                index = 6;
                damageset = 1;
                isadrenal = true;
            }
            for (int i = 0; i < index; i++) {
                Vector2 spanPos = Projectile.Center + (piOver3 * i + Main.rand.NextFloat(MathHelper.TwoPi)).ToRotationVector2() * 560;
                Vector2 vr = spanPos.To(target.Center).UnitVector() * 15;
                Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), spanPos, vr
                    , ModContent.ProjectileType<StormArc>(), (int)(Projectile.damage * damageset), Projectile.knockBack, Projectile.owner);
                proj.timeLeft = 30;
                proj.penetrate = 3;
                proj.tileCollide = false;
                if (isadrenal) {
                    proj.usesIDStaticNPCImmunity = true;
                    proj.localNPCHitCooldown = 6;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            float rot = Projectile.rotation + MathHelper.PiOver4;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2;
            Main.EntitySpriteDraw(texture, drawPosition, null, Projectile.GetAlpha(lightColor), rot + (Owner.direction > 0 ? 0 : MathHelper.PiOver2)
                , origin, Projectile.scale * 0.8f, Owner.direction > 0 ? 0 : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
