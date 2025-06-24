using CalamityOverhaul.Content.RemakeItems;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal abstract class BaseTurretItem : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }
}
