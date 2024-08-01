using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
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
        public List<ElectrosphereLauncherOrb> Orbs = [];
        public const int MaxOrbNum = 4;
        private int fireIndex;
        public override void SetRangedProperty() {
            FireTime = 3;
            ShootPosToMouLengValue = 30;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0f;
            ControlForce = 0f;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            Recoil = 0f;
            kreloadMaxTime = 60;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override bool KreLoadFulfill() {
            fireIndex = 0;
            Orbs = [];
            return true;
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(dustID1: DustID.UnusedWhiteBluePurple, dustID2: DustID.UnusedWhiteBluePurple, dustID3: DustID.UnusedWhiteBluePurple);
        }

        public override void FiringShoot() {
            FireTime = 3;
            if (Orbs == null) {
                Orbs = [];
            }

            Projectile orb = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.52f) * Main.rand.NextFloat(0.9f, 1.5f)
                , ModContent.ProjectileType<ElectrosphereLauncherOrb>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

            ElectrosphereLauncherOrb newOrb = orb.ModProjectile as ElectrosphereLauncherOrb;
            if (newOrb != null) {
                Orbs.Add(newOrb);
                Orbs.RemoveAll((ElectrosphereLauncherOrb p) => p == null);
                Orbs.RemoveAll((ElectrosphereLauncherOrb p) => p.Projectile.active == false || p.Projectile.type != ModContent.ProjectileType<ElectrosphereLauncherOrb>());
                if (newOrb != null) {
                    newOrb.orbList = Orbs.ToArray();
                }
            }

            fireIndex++;
            if (fireIndex >= MaxOrbNum) {
                FireTime = 60;
                fireIndex = 0;
                Orbs = [];
            }
        }
    }
}
