using CalamityOverhaul.Content.Cyberwares.Skills;
using CalamityOverhaul.Content.RAMSystems;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>
    /// 自骇水晶雷达技能，Instant 类
    /// <br/>耗 RAM 清 debuff + 短暂无敌；IsReady 要求冷却结束且 RAM 充足
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

        //冷却进度，1 就绪，0 刚放
        public override float StatusFillRatio {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return 0f;
                }
                int frames = p.GetModPlayer<SelfHackCrystalPlayer>().SkillCooldownTimer;
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

        //冷却剩余秒，无冷却显示 RAM 消耗
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
                return Language.GetTextValue("Mods.CalamityOverhaul.UI.CyberwareUI.SkillRamCost", SelfHackCrystal.SkillRamCost);
            }
        }

        public override void OnInstantTrigger(Player player) {
            player.GetModPlayer<SelfHackCrystalPlayer>().TryFireSelfHackFromRadial();
        }
    }
}
