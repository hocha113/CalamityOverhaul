using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦的雷达技能描述符
    /// <br/>属于 <see cref="CyberwareSkillKind.Toggle"/> 类：每次选中切换时缓激活状态
    /// <br/>真实运行时状态全部保留在 <see cref="Sandevistan"/> 单例上，本类只是雷达的桥接
    /// </summary>
    internal sealed class SandevistanSkill : CyberwareSkillBase
    {
        public static readonly SandevistanSkill Instance = new();

        public override string DisplayName => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.Sandevistan.SkillName");

        public override string Description => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.Sandevistan.SkillDesc");

        //右上角状态文字：激活中显示"ON"，否则显示剩余冷却百分比
        public override string StatusText => Sandevistan.IsActive
            ? (CyberwareSkillRadialUI.StatusOn?.Value ?? "ON")
            : ((int)(Sandevistan.CooldownRatio * 100f)) + "%";

        //雷达扇区填充进度直接复用冷却比例：1 = 满，0 = 空
        public override float StatusFillRatio => Sandevistan.CooldownRatio;

        //已激活时即便冷却归零也允许选中（玩家可以主动关闭时缓）
        public override bool IsReady => Sandevistan.IsActive || Sandevistan.CurrentCooldown > 0f;

        public override CyberwareSkillKind Kind => CyberwareSkillKind.Toggle;

        public override bool IsActivated => Sandevistan.IsActive;

        public override int IconItemType => ModContent.ItemType<SandevistansItem>();

        public override void OnToggleTrigger(Player player) {
            if (Sandevistan.IsActive) {
                Sandevistan.ForceDeactivate();
                return;
            }
            //激活路径：与原版按键触发完全一致，仅在冷却可用时进入
            if (Sandevistan.CurrentCooldown > 0f) {
                Sandevistan.TryActivate();
            }
        }
    }
}
