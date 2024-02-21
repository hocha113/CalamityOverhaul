using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMinishark : BaseRItem
    {
        public override int TargetID => ItemID.Minishark;
        public override bool FormulaSubstitution => false;
        public override void On_PostSetDefaults(Item item) {
            item.useTime = 5;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<MinisharkHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
            => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, CWRLocText.GetText("Wap_Minishark_Text"));
    }
}
