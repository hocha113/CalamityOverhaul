using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RStarCannon : ItemOverride
    {
        public override int TargetID => ItemID.StarCannon;
        public override bool FormulaSubstitution => false;
        public override void SetDefaults(Item item) {
            item.SetHeldProj<StarCannonHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 300;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
