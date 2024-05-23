using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class TerrorBladeHeld : BaseHeldProj
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

            TerrorBladeEcType.UpdateBar(item);
            Projectile.Center = player.GetPlayerStabilityCenter();
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) {
            TerrorBladeEcType.DrawRageEnergyChargeBar(Main.player[Projectile.owner], Projectile.ai[1]);
            return false;
        }
    }
}
