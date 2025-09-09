﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CosmicBolterHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "VernalBolter";
        public override int TargetID => ModContent.ItemType<VernalBolter>();
        private int fireIndex2;
        private bool fire;
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            DrawArrowMode = -26;
        }
        public override void SetShootAttribute() {
            Item.useTime = 20;
            if (fire) {
                Item.useTime = 5;
                fireIndex++;
                if (fireIndex >= 5) {
                    fire = false;
                    fireIndex = 0;
                }
            }
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<VernalBolt>();
            }
        }
        public override void BowShoot() {
            for (int i = 0; i < 3; i++) {
                FireOffsetPos = ShootVelocity.GetNormalVector() * ((-1 + i) * 8);
                base.BowShoot();
            }
        }
        public override void PostBowShoot() {
            if (!fire) {
                if (++fireIndex2 > 12) {
                    fire = true;
                    fireIndex2 = 0;
                }
            }
        }
    }
}
