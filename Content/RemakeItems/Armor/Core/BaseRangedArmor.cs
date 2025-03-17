using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Armor.Core
{
    internal abstract class BaseRangedArmor : ItemOverride
    {
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        /// <summary>
        /// 需要降低的装弹时间比例
        /// </summary>
        public virtual float KreloadTimeIncreaseValue => 0;
        /// <summary>
        /// 需要关联的胸甲ID
        /// </summary>
        public virtual int BodyID => ItemID.None;
        /// <summary>
        /// 需要关联的护腿ID
        /// </summary>
        public virtual int LegsID => ItemID.None;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) {
            if (body.type != BodyID || legs.type != LegsID) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + ((int)(KreloadTimeIncreaseValue * 100)) + "%";
            player.CWR().KreloadTimeIncrease -= KreloadTimeIncreaseValue;
            UpdateBonus();
        }

        public virtual void UpdateBonus() { }
    }
}
