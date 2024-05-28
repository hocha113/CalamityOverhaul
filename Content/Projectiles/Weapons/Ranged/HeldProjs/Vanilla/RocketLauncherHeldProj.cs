using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 火箭发射器
    /// </summary>
    internal class RocketLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.RocketLauncher].Value;
        public override int targetCayItem => ItemID.RocketLauncher;
        public override int targetCWRItem => ItemID.RocketLauncher;
        public override void SetRangedProperty() {
            FireTime = 45;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            HandFireDistance = 5;
            GunPressure = 0.5f;
            ControlForce = 0.05f;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 13;
            RecoilOffsetRecoverValue = 0.9f;
            Recoil = 3.5f;
            RangeOfStress = 10;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(-30, 3, -3);
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound with { Pitch = -0.6f, Volume = 0.6f }, Projectile.Center);
        }

        public override void FiringShoot() {
            ModOwner.SetScreenShake(3);
            //火箭弹药特判
            float newDamg = WeaponDamage;
            Item ammoItem = GetSelectedBullets();
            AmmoTypes = ProjectileID.RocketI;
            if (ammoItem.type == ItemID.RocketII) {
                AmmoTypes = ProjectileID.RocketII;
                newDamg *= 0.9f;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                AmmoTypes = ProjectileID.RocketIII;
                newDamg *= 0.85f;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                AmmoTypes = ProjectileID.RocketIV;
                newDamg *= 0.8f;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                AmmoTypes = ProjectileID.ClusterRocketI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                AmmoTypes = ProjectileID.ClusterRocketII;
                newDamg *= 0.85f;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                AmmoTypes = ProjectileID.DryRocket;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                AmmoTypes = ProjectileID.WetRocket;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                AmmoTypes = ProjectileID.HoneyRocket;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                AmmoTypes = ProjectileID.LavaRocket;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                AmmoTypes = ProjectileID.MiniNukeRocketI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                AmmoTypes = ProjectileID.MiniNukeRocketII;
                newDamg *= 0.85f;
            }

            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * 0.5f, AmmoTypes, (int)newDamg, WeaponKnockback, Owner.whoAmI, 0, 0, 3);
            Main.projectile[proj].scale *= 1.6f;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = 5;
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.RocketLauncher;
        }
    }
}
