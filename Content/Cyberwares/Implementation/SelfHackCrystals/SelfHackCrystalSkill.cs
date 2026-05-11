using CalamityOverhaul.Content.Cyberwares.Skills;
using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>
    /// 自骇水晶的雷达技能描述符 —— Instant 类
    /// <br/>选中后立即触发一次自骇协议，消耗 RAM 清除所有 debuff 并附带短暂无敌
    /// <br/>就绪状态同时要求冷却结束与 RAM 充足，雷达会按这两条件灰显扇区
    /// </summary>
    internal sealed class SelfHackCrystalSkill : CyberwareSkillBase
    {
        public static readonly SelfHackCrystalSkill Instance = new();

        public override string DisplayName => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.SelfHackCrystal.SkillName");

        public override string Description => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.SelfHackCrystal.SkillDesc");

        public override int IconItemType => ModContent.ItemType<SelfHackCrystal>();

        public override CyberwareSkillKind Kind => CyberwareSkillKind.Instant;

        //冷却进度：1 表示完全就绪，0 表示刚释放
        public override float StatusFillRatio {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return 0f;
                }
                int frames = p.GetModPlayer<SelfHackCrystalPlayer>().SkillCooldownTimer;
                if (SelfHackCrystal.SkillCooldown <= 0) {
                    return 1f;
                }
                return 1f - MathHelper.Clamp((float)frames / SelfHackCrystal.SkillCooldown, 0f, 1f);
            }
        }

        public override bool IsReady {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                if (p.GetModPlayer<SelfHackCrystalPlayer>().SkillCooldownTimer > 0) {
                    return false;
                }
                return RamSystem.CanAfford(SelfHackCrystal.SkillRamCost);
            }
        }

        //状态文字：优先显示冷却剩余秒数，无冷却时显示 RAM 消耗
        public override string StatusText {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return string.Empty;
                }
                int frames = p.GetModPlayer<SelfHackCrystalPlayer>().SkillCooldownTimer;
                if (frames > 0) {
                    int seconds = (frames + 59) / 60;
                    return seconds + "s";
                }
                return "RAM " + SelfHackCrystal.SkillRamCost;
            }
        }

        public override void OnInstantTrigger(Player player) {
            player.GetModPlayer<SelfHackCrystalPlayer>().TryFireSelfHackFromRadial();
        }
    }
}
