using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足雷达技能，Charge 类
    /// <br/>扇区悬停蓄力、松键释放；仅地面可选，瞄点=蓄力比例
    /// </summary>
    internal sealed class OmniElectricFootSkill : CyberwareSkillBase
    {
        public static readonly OmniElectricFootSkill Instance = new();

        public override string DisplayName => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.OmniElectricFoot.SkillName");

        public override string Description => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.OmniElectricFoot.SkillDesc");

        public override int IconItemType => ModContent.ItemType<OmniElectricFoot>();

        public override CyberwareSkillKind Kind => CyberwareSkillKind.Charge;

        public override int FullChargeTicks => OmniElectricFoot.FullChargeTicks;

        //仅地面可蓄力
        public override bool IsReady {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                OmniElectricFootPlayer fp = p.GetModPlayer<OmniElectricFootPlayer>();
                if (!fp.IsOnGround) {
                    return false;
                }
                if (p.mount.Active || p.grappling[0] >= 0) {
                    return false;
                }
                return true;
            }
        }

        //扇区填充=实时蓄力比例
        public override float StatusFillRatio => RadialChargeRatio;

        public override string StatusText => ((int)(RadialChargeRatio * 100f)) + "%";

        public override void OnChargeTick(Player player, float ratio) {
            OmniElectricFootPlayer fp = player.GetModPlayer<OmniElectricFootPlayer>();
            fp.RadialDriveCharge(ratio);
        }

        public override void OnChargeRelease(Player player, float ratio) {
            OmniElectricFootPlayer fp = player.GetModPlayer<OmniElectricFootPlayer>();
            fp.RadialReleaseCharge(ratio);
        }

        public override void OnChargeCancel(Player player) {
            OmniElectricFootPlayer fp = player.GetModPlayer<OmniElectricFootPlayer>();
            fp.RadialCancelCharge();
        }
    }
}
