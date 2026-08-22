using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 战术榨取（芯片档）：十秒内攻击方打防守方，按伤害给攻击方回 RAM。<br/>
    /// <b>落点是攻击方 RAM：服务端拥有的资源</b>：额度记在授予账载荷
    /// （<see cref="OnAuthorityGranted"/>），逐击结算由
    /// <see cref="CombatSiphonSettlement"/> 在服务端处理 msg 117 转播的位置捕获
    /// （tML 源码核对见该文件头注释）。防守方侧无任何数值变化，只挂 HUD 条目
    /// ：知道自己在被当电池打。<br/>
    /// 预算形状镜像 DataLeech 修复后的裁决：单次上限 + 全程总额 + 回流间隔三闸，
    /// 多段武器一帧几十跳也钉不满 RAM 条
    /// </summary>
    internal class CombatSiphon : PlayerHackDef
    {
        /// <summary>每点转播伤害折算的 RAM</summary>
        internal const float RamPerDamage = 0.004f;
        /// <summary>单次命中回流上限（经济泄压阀，设计 §5.4 写死）</summary>
        internal const float PerHitCap = 1.2f;
        /// <summary>单条授予的回流总额</summary>
        internal const float TotalBudget = 6f;
        /// <summary>两次回流的最小间隔（帧）：鞭子与穿透弹一帧能进十几次命中</summary>
        internal const int SettleCooldownFrames = 10;

        internal static readonly Color Drain = new(255, 180, 90);

        /// <summary>晶粒纹：躯体上着一圈瞄准刻线，命中处引出的数据流注进右上的电量格，人形电池</summary>
        internal const string Die =
            "M -0.66 -0.40 L -0.30 -0.40 M -0.66 0.48 L -0.30 0.48 "
            + "M -0.66 -0.40 Q -0.76 0.04 -0.66 0.48 M -0.30 -0.40 Q -0.20 0.04 -0.30 0.48 "
            + "M -0.48 -0.58 L -0.48 -0.46 M -0.48 0.54 L -0.48 0.66 M -0.80 0.04 L -0.70 0.04 "
            + "M -0.24 0.04 L -0.14 0.04 "
            + "M -0.10 -0.02 L 0.04 -0.08 M 0.12 -0.12 L 0.26 -0.18 M 0.32 -0.22 L 0.42 -0.26 "
            + "M 0.46 -0.52 L 0.76 -0.52 L 0.76 -0.24 L 0.46 -0.24 Z "
            + "M 0.61 -0.46 L 0.61 -0.30 M 0.53 -0.38 L 0.69 -0.38";

        /// <summary>授予账载荷：剩余额度与回流节拍，随授予自清</summary>
        internal sealed class SiphonQuota
        {
            public float Remaining = TotalBudget;
            public ulong NextFrame;
        }

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 4;
            Category = QuickHackCategory.Contagion;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 600;

        public override void OnAuthorityGranted(Player caster, Player defender,
            PlayerHackGrant grant) {
            grant.AuthorityState = new SiphonQuota();
        }

        /// <summary>
        /// 服务端逐击结算（<see cref="CombatSiphonSettlement.OnHurt"/> 调进来）。
        /// 预算按批准额扣：RAM 满桶溢出的部分也算花掉，防"顶着满桶白嫖续航"
        /// （DataLeech 同款）
        /// </summary>
        internal static void SettleAuthority(PlayerHackGrant grant, Player attacker,
            int damage) {
            if (grant.AuthorityState is not SiphonQuota quota || damage <= 0) return;
            if (Main.GameUpdateCount < quota.NextFrame) return;
            float amount = MathHelper.Min(damage * RamPerDamage, PerHitCap);
            amount = MathHelper.Min(amount, quota.Remaining);
            if (amount <= 0f) return;

            quota.Remaining -= amount;
            quota.NextFrame = Main.GameUpdateCount + SettleCooldownFrames;
            RamSystem.Restore(attacker, amount, out _);
        }

        //防守方通道刻意零逻辑：帐本条目本身就是"你在被当电池打"的告知

        //各端低频表现：防守方身上飘出朝攻击方去的窃电微光（命中的那一缕在结算钩里发）
        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ || elapsed % 24 != 0) return;
            Player caster = casterIndex >= 0 && casterIndex < Main.maxPlayers
                ? Main.player[casterIndex] : null;
            if (caster?.active != true) return;
            Vector2 dir = (caster.Center - defender.Center)
                .SafeNormalize(-Vector2.UnitY);
            PRTLoader.NewParticle<PRT_Spark>(
                defender.Center + Main.rand.NextVector2Circular(14f, 18f),
                dir * Main.rand.NextFloat(1.2f, 2.4f), Drain * 0.7f, 0.5f)
                ?.Configure(false, 20);
        }

        /// <summary>命中回流的可见化：一缕数据流从防守方飘向攻击方（各端在受击钩里各自发）</summary>
        internal static void EmitDrain(Player defender, Player attacker) {
            if (Main.dedServ) return;
            Vector2 dir = (attacker.Center - defender.Center)
                .SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(defender.Center,
                    dir.RotatedByRandom(0.25f) * Main.rand.NextFloat(3.5f, 7f),
                    Drain, 0.75f)?.Configure(false, 15);
            }
        }

        public override string GlyphDiePath => Die;
    }
}
