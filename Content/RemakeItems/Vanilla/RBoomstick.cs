using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RBoomstick : BaseRItem
    {
        public override int TargetID => ItemID.Boomstick;
        public override bool FormulaSubstitution => false;
        public override void SetDefaults(Item item) {
            item.SetHeldProj<BoomstickHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 12;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, CWRLocText.GetText("Wap_Boomstick_Text"));
    }
}
