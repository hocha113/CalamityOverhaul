using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSausageMaker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.SausageMaker>();
        public override int ProtogenesisID => ModContent.ItemType<SausageMaker>();
        public override void SetDefaults(Item item) => item.shoot = ModContent.ProjectileType<RSausageMakerSpear>();
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "SausageMaker");
    }
}
