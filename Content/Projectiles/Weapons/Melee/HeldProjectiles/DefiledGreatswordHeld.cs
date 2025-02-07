using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Melee;
using InnoVault.GameContent.BaseEntity;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class DefiledGreatswordHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool oldChargeSet;
        private int oldItemType;
        private float MaxCarge => Item.type == ModContent.ItemType<BlightedCleaver>() ? RBlightedCleaver.BlightedCleaverMaxRageEnergy : RDefiledGreatsword.DefiledGreatswordMaxRageEnergy;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 11;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
        }
        public override bool? CanDamage() => false;
        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Item item = Owner.GetItem();
            if (item.IsAir ||
                item.type != ModContent.ItemType<DefiledGreatsword>()
                && item.type != ModContent.ItemType<BlightedCleaver>()
                ) {
                Projectile.Kill();
                return;
            }
            if (item.CWR().MeleeCharge > 0) {
                item.useAnimation = 16;
                item.useTime = 16;
                if (Projectile.ai[1] < 1) {
                    Projectile.ai[1] += 0.05f;
                }
            }
            else {
                item.useAnimation = 26;
                item.useTime = 26;
                if (Projectile.ai[1] > 0) {
                    Projectile.ai[1] -= 0.05f;
                }
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter();

            if (Projectile.ai[0] == 0) {
                oldChargeSet = item.CWR().MeleeCharge > 0;
            }

            if (Projectile.ai[0] > 2) {
                int type = item.type;
                if (type != oldItemType) {//如果不一样就说明切换了武器，这里就同步一次状态
                    oldChargeSet = item.CWR().MeleeCharge > 0;
                }
                oldItemType = type;
                bool set = item.CWR().MeleeCharge > 0;
                if (set && !oldChargeSet) {
                    SoundEngine.PlaySound(CWRSound.Pecharge with { Volume = 0.4f }, Owner.Center);
                }
                if (!set && oldChargeSet) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge with { Volume = 0.4f }, Owner.Center);
                }
                oldChargeSet = set;
            }
            Projectile.ai[0]++;
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) {
            Item item = Main.player[Projectile.owner].GetItem();
            if (item != null && item.type > ItemID.None) {
                RDefiledGreatsword.DrawRageEnergyChargeBar(Main.player[Projectile.owner], Projectile.ai[1]
                    , item.CWR().MeleeCharge / MaxCarge);
            }

            return false;
        }
    }
}
