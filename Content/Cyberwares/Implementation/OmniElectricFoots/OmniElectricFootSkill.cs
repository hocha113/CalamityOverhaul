using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足的雷达技能描述符
    /// <br/>属于 <see cref="CyberwareSkillKind.Charge"/> 类：在雷达扇区上的悬停时间即为蓄力时间，
    /// 松开方向键时根据当前蓄力比例释放蓄力跳，与原版"按住-松开"的肌肉记忆完全一致
    /// <br/>仅在玩家"脚踏地面"时允许选中，避免空中释放出现无效跳跃
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

        //仅在地面且未在挂钩/坐骑等特殊状态下允许蓄力，避免空中误触
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

        //扇区填充：实时蓄力比例，松开瞬间会被雷达通过 OnChargeRelease 消费
        public override float StatusFillRatio => RadialChargeRatio;

        //状态文字：百分比快速阅读
        public override string StatusText => ((int)(RadialChargeRatio * 100f)) + "%";

        public override void OnChargeTick(Player player, float ratio) {
            //每帧把雷达累积的比例同步给 ModPlayer，让头顶 HUD 显示真实的蓄力进度
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
