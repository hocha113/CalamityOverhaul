using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DefiledGreatswordHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 11;
            Projectile.friendly = true;
        }
        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }
            Item item = player.ActiveItem();
            if (item.IsAir 
                || (item.type != ModContent.ItemType<DefiledGreatswordEcType>()
                && item.type != ModContent.ItemType<DefiledGreatsword>())
                && item.type != ModContent.ItemType<BlightedCleaverEcType>()
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
            DefiledGreatswordEcType.UpdateBar(item);
            Projectile.Center = player.GetPlayerStabilityCenter();
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) {
            DefiledGreatswordEcType.DrawRageEnergyChargeBar(Main.player[Projectile.owner], Projectile.ai[1]);
            return false;
        }
    }
}
