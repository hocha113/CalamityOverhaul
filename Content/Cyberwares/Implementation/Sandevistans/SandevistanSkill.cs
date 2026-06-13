using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦雷达描述符，Toggle 切换时缓
    /// <br/>运行时状态在 Sandevistan，本类仅桥接
    /// </summary>
    internal sealed class SandevistanSkill : CyberwareSkillBase
    {
        public static readonly SandevistanSkill Instance = new();

        public override string DisplayName => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.Sandevistan.SkillName");

        public override string Description => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.Sandevistan.SkillDesc");

        //激活 ON，否则冷却百分比
        public override string StatusText => Sandevistan.IsActive
            ? (CyberwareSkillRadialUI.StatusOn?.Value ?? "ON")
            : ((int)(Sandevistan.CooldownRatio * 100f)) + "%";

        public override float StatusFillRatio => Sandevistan.CooldownRatio;

        //激活中冷却归零仍可关；否则需有冷却
        public override bool IsReady => Sandevistan.IsActive || Sandevistan.CurrentCooldown > 0f;

        public override CyberwareSkillKind Kind => CyberwareSkillKind.Toggle;

        public override bool IsActivated => Sandevistan.IsActive;

        public override int IconItemType => ModContent.ItemType<SandevistansItem>();

        public override void OnToggleTrigger(Player player) {
            if (Sandevistan.IsActive) {
                Sandevistan.ForceDeactivate();
                return;
            }
            if (Sandevistan.CurrentCooldown > 0f) {
                Sandevistan.TryActivate();
            }
        }
    }
}
