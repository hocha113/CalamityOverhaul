﻿using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs.Vanilla
{
    internal class LunarFlareBookHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.LunarFlareBook].Value; 
        public override int targetCayItem => ItemID.LunarFlareBook;
        public override int targetCWRItem => ItemID.LunarFlareBook;
        public override void SetRangedProperty() {
            Projectile.DamageType = DamageClass.Magic;
            GunPressure = 0;
            HandDistance = 15;
            HandDistanceY = -5;
            Recoil = 0;
            ArmRotSengsFrontNoFireOffset = 13;
            AngleFirearmRest = 0;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFire) {
                Projectile.Center = Owner.MountedCenter + (DirSign > 0 ? new Vector2(13, -20) : new Vector2(-13, -20));
                Projectile.rotation = DirSign > 0 ? 0 : MathHelper.Pi;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 + (DirSign > 0 ? MathHelper.ToRadians(60) : MathHelper.ToRadians(120))) * DirSign;
            }
        }

        public override int Shoot() {
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Center;
                pos.X += DirSign * Main.rand.Next(130, 360);
                pos.Y -= Main.rand.Next(660, 760);
                Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * ScaleFactor;
                Projectile.NewProjectile(Source, pos, vr, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            return 0;
        }
    }
}