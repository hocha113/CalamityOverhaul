using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class TerrorBladeHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool oldChargeSet;
        private int oldItemType;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 11;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
        }
        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }
            Item item = player.GetItem();
            if (item.IsAir || (item.type != ModContent.ItemType<TerrorBladeEcType>()
                && item.type != ModContent.ItemType<TerrorBlade>())) {
                Projectile.Kill();
                return;
            }

            if (item.CWR().MeleeCharge > 0) {
                item.shootSpeed = 20f;
                item.useAnimation = 15;
                item.useTime = 15;
                item.CWR().ai[0] = 1;
                if (Projectile.ai[1] < 1) {
                    Projectile.ai[1] += 0.05f;
                }
            }
            else {
                item.shootSpeed = 15f;
                item.useAnimation = 20;
                item.useTime = 20;
                item.CWR().ai[0] = 0;
                if (Projectile.ai[1] > 0) {
                    Projectile.ai[1] -= 0.05f;
                }
            }

            if (item.CWR().MeleeCharge > TerrorBladeEcType.TerrorBladeMaxRageEnergy) {
                item.CWR().MeleeCharge = TerrorBladeEcType.TerrorBladeMaxRageEnergy;
            }

            Projectile.Center = player.GetPlayerStabilityCenter();

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
            TerrorBladeEcType.DrawRageEnergyChargeBar(Main.player[Projectile.owner], Projectile.ai[1]);
            return false;
        }
    }
}
