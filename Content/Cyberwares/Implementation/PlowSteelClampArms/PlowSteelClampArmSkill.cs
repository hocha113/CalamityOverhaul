using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂雷达技能，Charge 双形态
    /// <br/>短按：固定短线 2 秒；长按满蓄：搜锚点铺长线 5 秒，无锚点降级短线
    /// <br/>满蓄阈值 30 帧，瞄点读 Main.MouseWorld
    /// </summary>
    internal sealed class PlowSteelClampArmSkill : CyberwareSkillBase
    {
        public static readonly PlowSteelClampArmSkill Instance = new();

        public override string DisplayName => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.PlowSteelClampArm.SkillName");

        public override string Description => Language.GetTextValue(
            "Mods.CalamityOverhaul.Items.PlowSteelClampArm.SkillDesc");

        public override int IconItemType => ModContent.ItemType<PlowSteelClampArm>();

        public override CyberwareSkillKind Kind => CyberwareSkillKind.Charge;

        /// <summary>满蓄阈值 30 帧，区分轻点与长按</summary>
        public override int FullChargeTicks => 30;

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

        /// <summary>蓄力期间手心火花反馈</summary>
        public override void OnChargeTick(Player player, float ratio) {
            //仅本机玩家做反馈，避免多人下重复播放
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //蓄力刚到阈值的那一刻给一个明显的提示点：粒子变密 + 改色
            if (ratio >= 1f && Main.rand.NextBool(4)) {
                Vector2 vel = Main.rand.NextVector2Circular(1.8f, 1.8f);
                Dust d = Dust.NewDustPerfect(player.Center + new Vector2(player.direction * 12f, -2f),
                    Terraria.ID.DustID.MartianSaucerSpark, vel, 100, default, 1.1f);
                d.noGravity = true;
            }
            else if (Main.rand.NextBool(8)) {
                Vector2 vel = Main.rand.NextVector2Circular(1.2f, 1.2f);
                Dust d = Dust.NewDustPerfect(player.Center + new Vector2(player.direction * 12f, -2f),
                    Terraria.ID.DustID.Torch, vel, 100, default, 0.9f);
                d.noGravity = true;
            }
        }

        /// <summary>松开时按蓄力比例选短线/长线</summary>
        public override void OnChargeRelease(Player player, float ratio) {
            //ratio == 1 表示蓄满（达到 FullChargeTicks）；否则视作短按
            bool longMode = ratio >= 1f;
            player.GetModPlayer<PlowSteelClampArmPlayer>()
                .TryFireWire(Main.MouseWorld, longMode);
        }

        /// <summary>蓄力打断钩子，无残留可清</summary>
        public override void OnChargeCancel(Player player) { }
    }
}
