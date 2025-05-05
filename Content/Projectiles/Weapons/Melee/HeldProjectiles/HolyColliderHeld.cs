using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class HolyColliderHeld : BaseSwing, IWarpDrawable
    {
        public override int TargetID => ModContent.ItemType<HolyCollider>();
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HolyCollider";
        public override string gradientTexturePath => CWRConstant.ColorBar + "HolyCollider_Bar";

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
            drawTrailTopWidth = 70;
            canDrawSlashTrail = true;
            ownerOrientationLock = true;
            Length = 140;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 2) {
                SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.8f }, Projectile.Center);
                Vector2 toMouse2 = Projectile.Center.To(InMousePos);
                float lengs2 = toMouse2.Length();
                if (lengs2 < Length * Projectile.scale) {
                    lengs2 = Length * Projectile.scale;
                }
                Vector2 targetPos2 = Projectile.Center + toMouse2.UnitVector() * lengs2;
                Vector2 unitToM2 = toMouse2.UnitVector();
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos2, unitToM2
                , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage, Projectile.knockBack
                , Owner.whoAmI, 1, Projectile.Center.X, Projectile.Center.Y);
                return;
            }

            float lengs = ToMouse.Length();
            if (lengs < Length * Projectile.scale) {
                lengs = Length * Projectile.scale;
            }
            Vector2 targetPos = Owner.GetPlayerStabilityCenter() + ToMouse.UnitVector() * lengs;
            Vector2 unitToM = UnitToMouseV;

            Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos, unitToM
                , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage / 6, Projectile.knockBack, Owner.whoAmI);
        }

        public override void SwingAI() {
            if (Projectile.ai[0] == 1) {
                SwingBehavior(33, 6, 0.1f, 0.1f, 0.012f, 0.01f, 0.1f, 0, 0, 0, 16, 32);
                maxSwingTime = 32;
                return;
            }
            else if (Projectile.ai[0] == 2) {
                canDrawSlashTrail = false;
                OtherMeleeSize = 1.25f;
                SwingBehavior(33, 2, 0.1f, 0.1f, 0.006f, 0.016f, 0.16f, 0, 0, 0, 12, 80);
                maxSwingTime = 80;
                return;
            }
            SwingBehavior(33, 3, 0.1f, 0.1f, 0.012f, 0.01f, 0.08f, 0, 0, 0, 12, 36);
        }

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.Warp() => WarpDraw();

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 toOwner = Projectile.Center - Owner.GetPlayerStabilityCenter();
            Vector2 offsetOwnerPos = toOwner.GetNormalVector() * 16 * Projectile.spriteDirection * MeleeSize;
            Vector2 pos = Projectile.Center - RodingToVer(48, toOwner.ToRotation()) + offsetOwnerPos;
            Vector2 drawPos = pos - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            if (Projectile.ai[0] == 2) {
                VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time, drawPos, null, Color.Gold * 0.6f
                    , drawRoting, drawOrigin, Projectile.scale * MeleeSize, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }

            Main.EntitySpriteDraw(texture, drawPos, new Rectangle?(rect), Color.White
                , drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
