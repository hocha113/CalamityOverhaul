using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class RemakeGhastlyVisageProj : ModProjectile
    {
        private Player Owner => CWRUtils.GetPlayerInstance(Projectile.owner);

        private Vector2 toMou => Owner.Center.To(Main.MouseWorld);

        private ref float Time => ref Projectile.ai[0];

        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<GhastlyVisageEcType>();

        public override string Texture => CWRConstant.Item_Magic + "GhastlyVisage";

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.65f, 0f, 0.1f);
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 6, 3);

            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }

            ObeyOwner();

            if (Projectile.IsOwnedByLocalPlayer()) {
                SpanProj();
            }

            Time++;
        }

        public void ObeyOwner() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = Projectile.direction = Math.Sign(toMou.X);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Projectile.Center = Owner.Center + new Vector2(Owner.direction * 16, 0);
            if (Owner.PressKey()) {
                Projectile.timeLeft = 2;
            }
            else {
                Projectile.Kill();
            }
        }

        public void SpanProj() {
            int type = ModContent.ProjectileType<GhastlyBlasts>();
            if (Time % 15 == 0 && Owner.statMana >= Owner.ActiveItem().mana) {
                SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
                Owner.statMana -= Owner.ActiveItem().mana;
                if (Owner.statMana < 0)
                    Owner.statMana = 0;
                for (int i = 0; i < Main.rand.Next(2, 4); i++) {
                    Projectile.NewProjectile(
                    Owner.parent(),
                    Projectile.Center,
                    toMou.RotatedBy(MathHelper.ToRadians(Main.rand.Next(-20, 20))).UnitVector() * 13,
                    type,
                    Projectile.damage,
                    2,
                    Owner.whoAmI
                    );
                }
            }
            if (Time % 5 == 0) {
                Vector2 vr = CWRUtils.GetRandomVevtor(-120, -60, 3);
                Projectile.NewProjectile(
                    Owner.parent(),
                    Projectile.Center,
                    vr,
                    ModContent.ProjectileType<SpiritFlame>(),
                    Projectile.damage / 4,
                    0,
                    Owner.whoAmI,
                    3
                    );
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 4),
                Color.White,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 4),
                Projectile.scale,
                Owner.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None
                );
            return false;
        }

        public override bool? CanDamage() {
            return false;
        }
    }
}
