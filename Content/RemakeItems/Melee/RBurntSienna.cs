using CalamityMod.Items;
using CalamityMod.Projectiles.Healing;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBurntSienna : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.BurntSienna>();
        public override int ProtogenesisID => ModContent.ItemType<BurntSienna>();
        public override string TargetToolTipItemName => "BurntSiennaEcType";
        public override void SetDefaults(Item item) {
            item.width = 42;
            item.damage = 32;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = item.useTime = 21;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 5.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 54;
            item.value = CalamityGlobalItem.RarityBlueBuyPrice;
            item.rare = 1;
            item.shoot = ModContent.ProjectileType<BurntSiennaProj>();
            item.shootSpeed = 7f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            item.initialize();
            item.CWR().ai[0]++;
            if (item.CWR().ai[0] > 3) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))) * Main.rand.NextFloat(0.75f, 1.12f);
                    int proj = Projectile.NewProjectile(
                        source,
                        position,
                        vr,
                        ProjectileID.SandBallGun,
                        item.damage / 2,
                        item.knockBack,
                        player.whoAmI
                        );
                    Main.projectile[proj].hostile = false;
                    Main.projectile[proj].friendly = true;
                }
                item.CWR().ai[0] = 0;
            }
            return false;
        }
    }
}
