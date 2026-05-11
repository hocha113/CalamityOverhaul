using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂的雷达技能描述符 —— Charge 类（双形态）
    /// <list type="bullet">
    ///   <item>短按（蓄力 &lt; 1.0）：固定长度的"短线"模式，沿当前鼠标方向静态铺设 2 秒，
    ///     <b>不需要任何物块作为锚点</b>。解决"指不到方块就发不出去"的硬伤</item>
    ///   <item>长按（蓄力 = 1.0）：尝试在光标周围搜索可锚定的物块来铺"长线"5 秒；
    ///     若仍未找到锚点，则自然降级到短线模式，永远不"哑火"</item>
    ///   <item>冷却由 <see cref="PlowSteelClampArmPlayer.SkillCooldownTimer"/> 提供，用于雷达扇区填充</item>
    /// </list>
    /// 新双键模型下，瞄点直接读 <c>Main.MouseWorld</c>，因为触发键和雷达键已经物理隔离，
    /// 鼠标方向不会被劫持，不再需要快照
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

        /// <summary>
        /// 蓄力达到 1.0 即视为长线模式：30 帧（0.5 秒）的阈值在"轻点"与"按住"之间留出明确分界，
        /// 避免误触把短线打成长线
        /// </summary>
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

        /// <summary>
        /// 蓄力期间的视觉反馈：在玩家手心位置喷溅少量火花
        /// </summary>
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

        /// <summary>
        /// 蓄力松开瞬间：按蓄力比例选择短线 / 长线
        /// <br/>所有失败路径（没装备 / 冷却中）都内部播失败音，雷达不再二次提示
        /// </summary>
        public override void OnChargeRelease(Player player, float ratio) {
            //ratio == 1 表示蓄满（达到 FullChargeTicks）；否则视作短按
            bool longMode = ratio >= 1f;
            player.GetModPlayer<PlowSteelClampArmPlayer>()
                .TryFireWire(Main.MouseWorld, longMode);
        }

        /// <summary>
        /// 蓄力中途被打断（卸下装备 / 切换义体等）时的清理钩子；
        /// 实际无视觉残留需要清，留空即可
        /// </summary>
        public override void OnChargeCancel(Player player) { }
    }
}
