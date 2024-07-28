using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class HolyColliderHeld : BaseSwing, IDrawWarp
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HolyCollider";
        public override string gradientTexturePath => CWRConstant.ColorBar + "HolyColliderEffectColorBar";
        
        public override void SetSwingProperty() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 122;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            distanceToOwner = 30;
            trailTopWidth = 50;
            canDrawSlashTrail = true;
            ownerOrientationLock = true;
            Length = 140;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                SpwanFireRainProj();
                return;
            }

            

            if (Projectile.ai[0] == 2) {
                SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.8f }, Projectile.Center);
                Vector2 toMouse2 = Projectile.Center.To(InMousePos);
                float lengs2 = toMouse2.Length();
                if (lengs2 < Length * Projectile.scale) {
                    lengs2 = Length * Projectile.scale;
                }
                Vector2 targetPos2 = Projectile.Center + toMouse2.UnitVector() * lengs2;
                Vector2 unitToM2 = toMouse2.UnitVector();
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos2, Vector2.Zero
                , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI, 1);
                for (int i = 0; i < lengs2 / 12; i++) {
                    DRK_LavaFire lavaFire = new DRK_LavaFire();
                    lavaFire.Velocity = toMouse2.UnitVector() * 2;
                    lavaFire.Position = Projectile.Center + unitToM2 * (1 + i) * 12;
                    lavaFire.Scale = Main.rand.NextFloat(1.8f, 3.2f);
                    lavaFire.Color = Color.White;
                    lavaFire.ai[0] = 1;
                    lavaFire.ai[1] = 0;
                    lavaFire.minLifeTime = 22;
                    lavaFire.maxLifeTime = 30;
                    DRKLoader.AddParticle(lavaFire);
                }
                return;
            }

            float lengs = ToMouse.Length();
            if (lengs < Length * Projectile.scale) {
                lengs = Length * Projectile.scale;
            }
            Vector2 targetPos = Owner.GetPlayerStabilityCenter() + ToMouse.UnitVector() * lengs;
            Vector2 unitToM = UnitToMouseV;

            Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos, Vector2.Zero
                , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage / 6, Projectile.knockBack, Owner.whoAmI);
            for (int i = 0; i < lengs / 12; i++) {
                DRK_LavaFire lavaFire = new DRK_LavaFire();
                lavaFire.Velocity = ToMouse.UnitVector() * 2;
                lavaFire.Position = Owner.GetPlayerStabilityCenter() + unitToM * (1 + i) * 12;
                lavaFire.Scale = Main.rand.NextFloat(0.8f, 1.2f);
                lavaFire.Color = Color.White;
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 22;
                lavaFire.maxLifeTime = 30;
                DRKLoader.AddParticle(lavaFire);
            }
        }

        public override void SwingAI() {
            if (Projectile.ai[0] == 1) {
                SwingBehavior(33, 6, 0.1f, 0.1f, 0.012f, 0.01f, 0.1f, 0, 0, 0, 16, 32);
                maxSwingTime = 32;
                return;
            }
            else if (Projectile.ai[0] == 2) {
                SwingBehavior(33, 2, 0.1f, 0.1f, 0.006f, 0.016f, 0.16f, 0, 0, 0, 12, 80);
                maxSwingTime = 80;
                return;
            }
            SwingBehavior(33, 3, 0.1f, 0.1f, 0.012f, 0.01f, 0.08f, 0, 0, 0, 12, 36);
        }

        private void HitEffect(Entity target, bool theofSteel) {
            if (theofSteel) {
                SoundEngine.PlaySound(MurasamaEcType.InorganicHit with { Pitch = 0.25f }, target.Center);
            }
            else {
                SoundEngine.PlaySound(MurasamaEcType.OrganicHit with { Pitch = 1.15f }, target.Center);
            }

            HitEffectValue(target, 13, out Vector2 rotToTargetSpeedTrengsVumVer, out int sparkCount);

            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity2 = rotToTargetSpeedTrengsVumVer.RotatedByRandom(0.35f) * Main.rand.NextFloat(0.3f, 1.6f);
                int sparkLifetime2 = Main.rand.Next(18, 30);
                float sparkScale2 = Main.rand.NextFloat(0.65f, 1.2f);
                Color sparkColor2 = Main.rand.NextBool(3) ? Color.OrangeRed : Color.DarkRed;
                if (theofSteel) {
                    sparkColor2 = Main.rand.NextBool(3) ? Color.Gold : Color.Goldenrod;
                }

                PRK_Spark spark = new PRK_Spark(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f
                        , target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * 1f
                        , false, (int)(sparkLifetime2 * 1.2f), sparkScale2 * 1.4f, sparkColor2);
                DRKLoader.AddParticle(spark);
            }

            if (Projectile.IsOwnedByLocalPlayer() && Projectile.numHits == 0 && Projectile.ai[0] == 0) {
                SpwanFireRainProj();
            }
        }

        private void SpwanFireRainProj() {
            for (int i = 0; i < Main.rand.Next(3, 5); i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center + Main.rand.NextVector2Unit() * Main.rand.Next(342, 468), Projectile.velocity / 3
                    , ModContent.ProjectileType<HolyColliderHolyFires>(), (int)(Projectile.damage * 0.5f), Projectile.knockBack, Owner.whoAmI);
                for (int j = 0; j < 3; j++) {
                    Vector2 pos = Owner.Center + Main.rand.NextVector2Unit() * Main.rand.Next(342, 468);
                    Vector2 particleSpeed = pos.To(Owner.Center).UnitVector() * 7;
                    BaseParticle energyLeak = new DRK_HolyColliderLight(pos, particleSpeed
                        , Main.rand.NextFloat(0.5f, 0.7f), Color.Gold, 90, 1, 1.5f, hueShift: 0.0f);
                    DRKLoader.AddParticle(energyLeak);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect(target, CWRLoad.NPCValue.TheofSteel[target.type]);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect(target, false);
        }

        bool IDrawWarp.canDraw() => true;

        bool IDrawWarp.noBlueshift() => true;

        void IDrawWarp.Warp() => WarpDraw();

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 toOwner = Projectile.Center - Owner.GetPlayerStabilityCenter();
            Vector2 offsetOwnerPos = toOwner.GetNormalVector() * 16 * Projectile.spriteDirection;
            Vector2 pos = Projectile.Center - RodingToVer(48, toOwner.ToRotation()) + offsetOwnerPos;
            Vector2 drawPos = pos - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            if (Projectile.ai[0] == 2) {
                CWRUtils.DrawMarginEffect(Main.spriteBatch, texture, Time, drawPos, null, Color.OrangeRed * 0.6f
                    , drawRoting, drawOrigin, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }

            Main.EntitySpriteDraw(texture, drawPos, new Rectangle?(rect), Color.White, drawRoting, drawOrigin, Projectile.scale, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
