using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class NapalmBombHeld : BaseHeldBox
    {
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<AmmoBoxFire>();
            AmmoBoxID = ModContent.ProjectileType<NapalmBombBox>();
            MaxCharge = 30;
        }
        public override void ExtraGeneration() {
            Projectile proj = Projectile.NewProjectileDirect(Item.GetSource_FromThis(), Owner.Center, Vector2.Zero, ModContent.ProjectileType<SuccessfullyDeployedEffct>(), 0, 0, Owner.whoAmI);
            SuccessfullyDeployedEffct successfullyDeployedEffct = proj.ModProjectile as SuccessfullyDeployedEffct;
            if (successfullyDeployedEffct != null) {
                successfullyDeployedEffct.text = "燃烧弹药箱已部署";
                successfullyDeployedEffct.textColor = Color.OrangeRed;
            }
        }
    }
}
