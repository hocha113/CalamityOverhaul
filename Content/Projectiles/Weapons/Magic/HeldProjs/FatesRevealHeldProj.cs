using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class FatesRevealHeldProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "FatesReveal";

        private Player Owner => CWRUtils.GetPlayerInstance(Projectile.owner);

        private Vector2 toMou => Owner.Center.To(Main.MouseWorld);

        private ref float Time => ref Projectile.ai[0];

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
            return false;//同其他的手持弹幕一样，不希望该实体受到速度更新的影响，这往往会导致一些出乎意料的效果
        }

        public override void AI() {
            if (Owner == null) {
                Projectile.Kill();
                return;
            }

            ObeyOwner();

            if (Projectile.IsOwnedByLocalPlayer())
                SpanProj();

            Time++;
        }

        public void ObeyOwner() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = Projectile.direction = Math.Sign(toMou.X);

            Projectile.Center = Owner.Center + toMou.UnitVector() * 53;
            Projectile.rotation = toMou.ToRotation();

            if (Owner.PressKey()) {
                Projectile.timeLeft = 2;
            }
            else {
                Projectile.Kill();
            }
        }

        public void SpanProj() {
            int type = ModContent.ProjectileType<HatredFire>();
            if (Time % 35 == 0) {
                SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
                for (int i = 0; i < Main.rand.Next(2, 4); i++) {
                    Projectile.NewProjectile(
                    Owner.parent(),
                    Projectile.Center,
                    toMou.RotatedBy(MathHelper.ToRadians(Main.rand.Next(-20, 20))).UnitVector() * 7,
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
                    Projectile.Center + Projectile.rotation.ToRotationVector2() * 36 + Main.rand.NextVector2Unit() * 16,
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
                null,
                Color.White,
                Projectile.rotation + MathHelper.PiOver4,
                CWRUtils.GetOrig(mainValue),
                Projectile.scale,
                SpriteEffects.None
                );
            return false;
        }

        public override bool? CanDamage() {
            return false;
        }

        public override void DrawBehind
            (int index, List<int> behindNPCsAndTiles
            , List<int> behindNPCs, List<int> behindProjectiles
            , List<int> overPlayers, List<int> overWiresUI) {
            overPlayers.Add(index);
        }
    }
}
