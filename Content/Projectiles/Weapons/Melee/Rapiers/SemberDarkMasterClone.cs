using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class SemberDarkMasterClone : DarkMasterClone
    {
        private Item item => Main.player[Projectile.owner].ActiveItem();
        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Player owner = Main.player[Projectile.owner];
            Vector2 moveTo;
            Projectile.localAI[1] += owner.velocity.X * 0.1f;
            moveTo = (Projectile.localAI[1] * 0.05f + MathHelper.TwoPi / 3 * Projectile.ai[0]).ToRotationVector2() * 160;

            Lighting.AddLight(Projectile.Center, Color.DarkBlue.ToVector3());

            if (item.type != ModContent.ItemType<TheDarkMaster>()
                && item.type != ModContent.ItemType<TheDarkMasterEcType>()
                || owner.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0
                || !owner.active || owner.CCed || owner == null) {
                if (Projectile.ai[0] == 1) {
                    owner.AddBuff(BuffID.Darkness, 180);
                }
                Projectile.Kill();
            }

            Projectile.timeLeft = 30;
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center + moveTo, 0.4f);
            if (Projectile.Distance(owner.Center + moveTo) < 16) {
                Projectile.ai[2] = 1;
            }
            if (Projectile.ai[2] == 0) {
                float angle = MathHelper.TwoPi * Main.rand.NextFloat(0f, 1f);
                Vector2 angleVec = angle.ToRotationVector2();
                Particle smoke = new HeavySmokeParticle(Projectile.Center, angleVec * Main.rand.NextFloat(1f, 2f)
                    , Color.Black, 30, Main.rand.NextFloat(0.25f, 1f), 0.5f, 0.1f);
                GeneralParticleHandler.SpawnParticle(smoke);
            }

            if (owner.PressKey()) {
                Projectile.ai[1]++;
            }
            else {
                Projectile.ai[1] = 0;
            }

            if (Projectile.ai[1] > 30) {
                Vector2 direction = Projectile.Center.DirectionTo(Main.MouseWorld);
                Projectile.direction = Math.Sign(direction.X);
                if (Projectile.owner == Main.myPlayer) {
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, direction * 16
                        , ModContent.ProjectileType<DarkMasterBeam>(), (int)(Projectile.damage * 0.4f), Projectile.knockBack, Projectile.owner, 1, 1);
                    Main.projectile[proj].Calamity().allProjectilesHome = true;
                }
                Projectile.ai[1] = 0;
            }

            Projectile.localAI[0] = Math.Abs(MathF.Sin(Projectile.ai[1] * 0.5f)) * 25;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.playerVisualClone[Projectile.owner] ??= new();
            Player owner = Main.player[Projectile.owner];

            Player player = Main.playerVisualClone[Projectile.owner];
            player.CopyVisuals(Main.player[Projectile.owner]);
            player.hair = owner.hair;
            player.skinVariant = owner.skinVariant;
            player.skinColor = Color.Black;
            player.shirtColor = Color.Black;
            player.underShirtColor = Color.Black;
            player.pantsColor = Color.Black;
            player.shoeColor = Color.Black;
            player.hairColor = Color.Black;
            player.eyeColor = Color.Red;
            for (int i = 0; i < player.dye.Length; i++) {
                if (player.dye[i].type != ItemID.ShadowDye) {
                    player.dye[i].SetDefaults(ItemID.ShadowDye);
                }
            }
            player.ResetEffects();
            player.ResetVisibleAccessories();
            player.DisplayDollUpdate();
            player.UpdateSocialShadow();
            player.UpdateDyes();
            player.PlayerFrame();
            if (owner.ItemAnimationActive && owner.altFunctionUse != 2)
                player.bodyFrame = owner.bodyFrame;
            else
                player.bodyFrame.Y = 0;
            player.legFrame.Y = 0;
            player.direction = Math.Sign(Projectile.DirectionTo(Main.MouseWorld).X);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, player, Projectile.position, 0f, player.fullRotationOrigin, 0f, 1f);
            if (owner.ItemAnimationActive && owner.altFunctionUse != 2) {
                Texture2D Sword = ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/TheDarkMaster").Value;
                float rots = Projectile.Center.DirectionTo(Main.MouseWorld).ToRotation() + MathHelper.PiOver4;
                Vector2 distToPlayer = Projectile.position - owner.position;
                Vector2 drawPos = owner.GetPlayerStabilityCenter() + distToPlayer - Main.screenPosition + (rots - MathHelper.PiOver4).ToRotationVector2() * (Projectile.localAI[0] - 5);
                Main.EntitySpriteDraw(Sword, drawPos, null, lightColor, rots, new Vector2(0, Sword.Height), 1f, SpriteEffects.None);
            }
            return false;
        }
    }
}
