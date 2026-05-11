using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂的雷达技能描述符 —— Instant 类
    /// <br/>选中后立即调用 <see cref="PlowSteelClampArmPlayer.TryFireWireFromRadial"/> 发射单分子线
    /// <br/>冷却信息由 <see cref="PlowSteelClampArmPlayer.SkillCooldownTimer"/> 提供，用于雷达扇区填充
    /// </summary>
    internal sealed class PlowSteelClampArmSkill : CyberwareSkillBase
    {
        public static readonly PlowSteelClampArmSkill Instance = new();

        public override string DisplayName => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.PlowSteelClampArm.SkillName");

        public override string Description => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.PlowSteelClampArm.SkillDesc");

        public override int IconItemType => ModContent.ItemType<PlowSteelClampArm>();

        public override CyberwareSkillKind Kind => CyberwareSkillKind.Instant;

        //单分子线必须确定一个世界目标点，让雷达在按下技能键时快照鼠标坐标
        //避免开盘期间鼠标被劫持去选扇区导致瞄点丢失
        public override bool RequiresAim => true;

        //冷却进度：1 表示完全就绪，0 表示刚释放
        public override float StatusFillRatio {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return 0f;
                }
                PlowSteelClampArmPlayer mp = p.GetModPlayer<PlowSteelClampArmPlayer>();
                return 1f - mp.CooldownRatio;
            }
        }

        public override bool IsReady {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                return p.GetModPlayer<PlowSteelClampArmPlayer>().SkillCooldownTimer <= 0;
            }
        }

        public override string StatusText {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return string.Empty;
                }
                int frames = p.GetModPlayer<PlowSteelClampArmPlayer>().SkillCooldownTimer;
                if (frames <= 0) {
                    return string.Empty;
                }
                int seconds = (frames + 59) / 60;
                return seconds + "s";
            }
        }

        public override void OnInstantTrigger(Player player) {
            //单技能直触路径会进到这里：使用玩家当前真实鼠标
            player.GetModPlayer<PlowSteelClampArmPlayer>().TryFireWireFromRadial(Main.MouseWorld);
        }

        public override void OnInstantTrigger(Player player, Vector2 aimWorld) {
            //雷达路径会进到这里：aimWorld 是按下技能键瞬间快照的鼠标位置
            player.GetModPlayer<PlowSteelClampArmPlayer>().TryFireWireFromRadial(aimWorld);
        }
    }
}
