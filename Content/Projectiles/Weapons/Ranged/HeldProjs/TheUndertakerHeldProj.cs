using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TheUndertakerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TheUndertaker].Value;
        public override int targetCayItem => ItemID.TheUndertaker;
        public override int targetCWRItem => ItemID.TheUndertaker;
        private int bulletNum {
            get => heldItem.CWR().NumberBullets;
            set => heldItem.CWR().NumberBullets = value;
        }
        public override void SetRangedProperty() {
            kreloadMaxTime = 40;
            fireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
        }

        public override bool WhetherStartChangingAmmunition() {
            return base.WhetherStartChangingAmmunition() && bulletNum < heldItem.CWR().AmmoCapacity && !onFire;
        }

        public override void KreloadSoundCaseEjection() {
            base.KreloadSoundCaseEjection();
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
        }

        public override bool PreFireReloadKreLoad() {
            if (bulletNum <= 0) {

                loadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                isKreload = false;
                if (heldItem.type != ItemID.None) {
                    heldItem.CWR().IsKreload = false;
                }

                bulletNum = 0;
            }
            return false;
        }

        public override void SpawnGunFireDust(Vector2 pos = default, Vector2 velocity = default, int splNum = 1, int dustID1 = 262, int dustID2 = 54, int dustID3 = 53) {
            base.SpawnGunFireDust(pos, velocity, splNum, dustID1, dustID2, dustID3);
        }

        public override void OnKreLoad() {
            bulletNum = heldItem.CWR().AmmoCapacity;
        }

        public override void PostFiringShoot() {
            bulletNum--;
        }
    }
}
