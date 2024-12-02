using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class SemberDarkMasterClone : BaseHeldProj, ICWRLoader
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player player;
        private Item item => Owner.GetItem();
        private static Asset<Texture2D> swordAsset;
        void ICWRLoader.LoadAsset() => swordAsset = CWRUtils.GetT2DAsset("CalamityMod/Items/Weapons/Melee/TheDarkMaster");
        void ICWRLoader.UnLoadData() => swordAsset = null;
        public override void SetDefaults() {
            Projectile.CloneDefaults(ModContent.ProjectileType<DarkMasterClone>());
            if (player == null) {
                player = new Player();
            }
        }
        public override bool? CanDamage() => false;
        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[1] += Owner.velocity.X * 0.1f;
            if (Owner.velocity.X == 0) {
                Projectile.localAI[1] += Owner.direction * 0.1f;
            }

            Vector2 moveTo = (Projectile.localAI[1] * 0.05f + MathHelper.TwoPi / 3 * Projectile.ai[0]).ToRotationVector2() * 160;

            Lighting.AddLight(Projectile.Center, Color.DarkBlue.ToVector3());

            if (item.type != ModContent.ItemType<TheDarkMaster>()
                && item.type != ModContent.ItemType<TheDarkMasterEcType>()
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0
                || !Owner.active || Owner.CCed || Owner == null) {
                if (Projectile.ai[0] == 1) {
                    Owner.AddBuff(BuffID.Darkness, 180);
                }
                Projectile.Kill();
            }

            Projectile.timeLeft = 30;
            Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center + moveTo, 0.4f);
            if (Projectile.Distance(Owner.Center + moveTo) < 16) {
                Projectile.ai[2] = 1;
            }
            if (Projectile.ai[2] == 0) {
                float angle = MathHelper.TwoPi * Main.rand.NextFloat(0f, 1f);
                Vector2 angleVec = angle.ToRotationVector2();
                Particle smoke = new HeavySmokeParticle(Projectile.Center, angleVec * Main.rand.NextFloat(1f, 2f)
                    , Color.Black, 30, Main.rand.NextFloat(0.25f, 1f), 0.5f, 0.1f);
                GeneralParticleHandler.SpawnParticle(smoke);
            }

            if (DownLeft) {
                Projectile.ai[1]++;
            }
            else {
                Projectile.ai[1] = 0;
            }

            if (Projectile.ai[1] > 30) {
                Vector2 direction = Projectile.Center.DirectionTo(Main.MouseWorld);
                Projectile.direction = Math.Sign(direction.X);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, direction * 16
                        , ModContent.ProjectileType<DarkMasterBeam>(), (int)(Projectile.damage * 0.4f), Projectile.knockBack, Projectile.owner, 1, 1);
                    Main.projectile[proj].Calamity().allProjectilesHome = true;
                }
                Projectile.ai[1] = 0;
            }

            Projectile.localAI[0] = Math.Abs(MathF.Sin(Projectile.ai[1] * 0.5f)) * 25 - 10;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (player == null) {
                return false;
            }

            player.CopyVisuals(Owner);

            player.hair = Owner.hair;
            player.skinVariant = Owner.skinVariant;
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

            player.bodyFrame = Owner.bodyFrame;
            player.legFrame = Owner.legFrame;

            player.direction = Math.Sign(Projectile.DirectionTo(Main.MouseWorld).X);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, player, Projectile.position, 0f, player.fullRotationOrigin, 0f, 1f);

            if (CWRUtils.GetProjectileHasNum(ModContent.ProjectileType<TheDarkMasterRapier>(), Owner.whoAmI) > 0) {
                Texture2D Sword = swordAsset.Value;
                float rots = Projectile.Center.DirectionTo(Main.MouseWorld).ToRotation() + MathHelper.PiOver4;
                Vector2 distToPlayer = Projectile.position - Owner.position;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + distToPlayer - Main.screenPosition
                    + (rots - MathHelper.PiOver4).ToRotationVector2() * (Projectile.localAI[0] - 5);
                Main.EntitySpriteDraw(Sword, drawPos, null, Color.Black, rots, new Vector2(0, Sword.Height), 1f, SpriteEffects.None);
            }

            return false;
        }
    }
}
