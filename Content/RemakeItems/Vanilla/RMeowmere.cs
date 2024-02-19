using CalamityMod.NPCs.TownNPCs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMeowmere : BaseRItem
    {
        public override int TargetID => ItemID.Meowmere;
        public override bool FormulaSubstitution => false;
        public override void On_PostSetDefaults(Item item) {
            item.rare = ItemRarityID.Red;
            item.UseSound = SoundID.Item58;
            item.useStyle = ItemUseStyleID.Swing;
            item.damage = 130;
            item.useAnimation = 9;
            item.useTime = 9;
            item.width = 30;
            item.height = 30;
            item.shoot = ProjectileID.Meowmere;
            item.scale = 1f;
            item.shootSpeed = 17f;
            item.knockBack = 1.7f;
            item.DamageType = DamageClass.Melee;
            item.value = Item.sellPrice(0, 25);
            item.autoReuse = true;
        }

        public static void SpanDust(Projectile projectile, float offsetScale = 0) {
            if (projectile.type == ProjectileID.Meowmere) {
                for (float i = 0f; i < 37; i += 1f) {
                    int dust = Dust.NewDust(projectile.Center, 0, 0, DustID.RainbowTorch, 0f, 0f, 0, Color.Transparent);
                    Main.dust[dust].position = projectile.Center;
                    Main.dust[dust].velocity = CWRUtils.randVr(13, 17);
                    Main.dust[dust].color = Main.hslToRgb(i / 37, 1f, 0.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].scale = 1f + projectile.ai[0] / 3f + offsetScale;
                }
            }
        }
    }
}
