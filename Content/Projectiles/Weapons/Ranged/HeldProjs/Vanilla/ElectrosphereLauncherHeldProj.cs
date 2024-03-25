using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ElectrosphereLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ElectrosphereLauncher].Value;
        public override int targetCayItem => ItemID.ElectrosphereLauncher;
        public override int targetCWRItem => ItemID.ElectrosphereLauncher;
        public List<Projectile> Orbs = new List<Projectile>();
        public const int MaxOrbNum = 4;
        public override void SetRangedProperty() {
            FireTime = 18;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0f;
            ControlForce = 0f;
            RepeatedCartridgeChange = true;
            Recoil = 0f;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            return true;
        }

        public override void FiringShoot() {
            Projectile orb = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f)
                , ModContent.ProjectileType<ElectrosphereLauncherOrb>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (Orbs == null) {
                Orbs = new List<Projectile>();
            }
            Orbs.Add(orb);
            Orbs.RemoveAll((Projectile p) => p == null);
            Orbs.RemoveAll((Projectile p) => p.active == false || p.type != ModContent.ProjectileType<ElectrosphereLauncherOrb>());
            if (Orbs.Count > MaxOrbNum) {
                Orbs.RemoveRange(0, 1);
            }
            ElectrosphereLauncherOrb newOrb = orb.ModProjectile as ElectrosphereLauncherOrb;
            if (newOrb != null) {
                newOrb.orbList = Orbs.ToArray();
            }
        }
    }
}
