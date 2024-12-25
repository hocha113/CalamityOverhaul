using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HalibutLegend
{
    internal abstract class FishSkill
    {
        public Item Item;
        public virtual int TargetItemID => ItemID.None;
        /// <summary>
        /// 将要研究这条鱼时，比目鱼会如何介绍它
        /// </summary>
        public virtual string Explain =>
            HalibutText.GetTextValue($"FishSkill.{GetType().Name}.Explain");
        /// <summary>
        /// 这条鱼的藏品介绍内容
        /// </summary>
        public virtual string Introduce =>
            HalibutText.GetTextValue($"FishSkill.{GetType().Name}.Introduce");
        public virtual bool PreShoot() => true;
        public virtual void PostShoot(int projIndex) { }
    }
}
