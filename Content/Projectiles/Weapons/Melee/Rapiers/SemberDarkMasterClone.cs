using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class SemberDarkMasterClone : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player playerClone;
        [VaultLoaden("@CalamityMod/Items/Weapons/Melee/TheDarkMaster")]
        private static Asset<Texture2D> swordAsset = null;
        public override void SetDefaults() {
            Projectile.CloneDefaults(CWRID.Proj_DarkMasterClone);
            if (playerClone == null) {
                playerClone = new Player();
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

            if (Item.type != CWRItemOverride.GetCalItemID("TheDarkMaster")
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
                PRT_Smoke smoke = new PRT_Smoke(Projectile.Center, angleVec * Main.rand.NextFloat(1f, 2f)
                    , Color.Black, 30, Main.rand.NextFloat(0.25f, 1f), 0.5f, 0.1f);
                PRTLoader.AddParticle(smoke);
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
                        , CWRID.Proj_DarkMasterBeam, (int)(Projectile.damage * 0.4f), Projectile.knockBack, Projectile.owner, 1, 1);
                    Main.projectile[proj].SetAllProjectilesHome(true);
                }
                Projectile.ai[1] = 0;
            }

            Projectile.localAI[0] = Math.Abs(MathF.Sin(Projectile.ai[1] * 0.5f)) * 25 - 10;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (playerClone == null) {
                return false;
            }

            playerClone.CopyVisuals(Owner);

            playerClone.hair = Owner.hair;
            playerClone.skinVariant = Owner.skinVariant;
            playerClone.skinColor = Color.Black;
            playerClone.shirtColor = Color.Black;
            playerClone.underShirtColor = Color.Black;
            playerClone.pantsColor = Color.Black;
            playerClone.shoeColor = Color.Black;
            playerClone.hairColor = Color.Black;
            playerClone.eyeColor = Color.Red;

            for (int i = 0; i < playerClone.dye.Length; i++) {
                if (playerClone.dye[i].type != ItemID.ShadowDye) {
                    playerClone.dye[i].SetDefaults(ItemID.ShadowDye);
                }
            }

            playerClone.ResetEffects();
            playerClone.ResetVisibleAccessories();
            playerClone.DisplayDollUpdate();
            playerClone.UpdateSocialShadow();
            playerClone.UpdateDyes();
            playerClone.PlayerFrame();

            playerClone.bodyFrame = Owner.bodyFrame;
            playerClone.legFrame = Owner.legFrame;
            playerClone.heldProj = -1;

            playerClone.direction = Math.Sign(Projectile.DirectionTo(InMousePos).X);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, playerClone, Projectile.position, 0f, playerClone.fullRotationOrigin, 0f, 1f);

            if (Owner.CountProjectilesOfID<TheDarkMasterRapier>() > 0) {
                Texture2D Sword = swordAsset.Value;
                float rots = Projectile.Center.DirectionTo(InMousePos).ToRotation() + MathHelper.PiOver4;
                Vector2 distToPlayer = Projectile.position - Owner.position;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + distToPlayer - Main.screenPosition
                    + (rots - MathHelper.PiOver4).ToRotationVector2() * (Projectile.localAI[0] - 5);
                Main.EntitySpriteDraw(Sword, drawPos, null, Color.Black, rots, new Vector2(0, Sword.Height), 1f, SpriteEffects.None);
            }

            return false;
        }
    }
}
