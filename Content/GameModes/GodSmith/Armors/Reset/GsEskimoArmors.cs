using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 防雪套两色共用层：单件沿用原版（无属性）；套装奖励为免疫冰冻与寒冷、攻击附带霜冻、防御 +15
    /// </summary>
    internal abstract class GsEskimoArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Immune to Frozen and Chilled, attacks inflict Frostburn, 15 more defense";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.buffImmune[BuffID.Frozen] = true;
            player.buffImmune[BuffID.Chilled] = true;
            player.statDefense += 15;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            target.AddBuff(BuffID.Frostburn, 180);
        }
    }

    /// <summary>防雪套</summary>
    internal class GsEskimoArmor : GsEskimoArmorScheme
    {
        public override int[] HeadIDs => [ItemID.EskimoHood];
        public override int BodyID => ItemID.EskimoCoat;
        public override int LegsID => ItemID.EskimoPants;
    }

    /// <summary>粉色防雪套</summary>
    internal class GsPinkEskimoArmor : GsEskimoArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PinkEskimoHood];
        public override int BodyID => ItemID.PinkEskimoCoat;
        public override int LegsID => ItemID.PinkEskimoPants;
    }
}
