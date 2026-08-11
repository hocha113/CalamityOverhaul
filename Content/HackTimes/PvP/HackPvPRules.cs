using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.PvP
{
    /// <summary>
    /// PvP 骇入的准入门槛与全部红线数值，一处谓词两端共用。<br/>
    /// <see cref="CanTarget(Player, Player, out HackRequestResultCode)"/> 是唯一判定入口：
    /// 攻击方客户端预检（<c>PlayerScannable.IsHackable</c>）、服务端请求校验
    /// （<c>ValidateAuthorityRequest</c> 的 Player 分支）与上传期逐帧重验调的都是它，
    /// 谓词只写一份（tml-netcode-pitfalls §2.4 companion rule）。<br/>
    /// 部分子句依赖只有一端持有的数据（对冷却/叠加账在服务端、复活保护在各自本机），
    /// 数据缺席的端对该子句直接放行——预检宽松无害，服务端重验才是闸。
    /// </summary>
    internal static class HackPvPRules
    {
        #region 准入数值（设计文档 §2.4，改动要先过设计）

        /// <summary>PvP 骇入最大距离（px）。刻意小于 PvE 的 6400——跨屏骇人没有反制窗口</summary>
        internal const float MaxDistance = 2400f;
        /// <summary>复活后保护帧数，窗口内不可被选中（出生点蹲人红线）</summary>
        internal const int SpawnProtectFrames = 300;
        /// <summary>同 (攻击方, 防守方) 对：任一协议落地后多少帧内不能再次开始上传</summary>
        internal const int PairCooldownFrames = 600;
        /// <summary>同一防守方全局在册效果上限</summary>
        internal const int MaxEffectsPerDefender = 3;
        /// <summary>同一攻击方对同一防守方的在册效果上限</summary>
        internal const int MaxEffectsPerPair = 2;

        #endregion

        #region 剥夺红线（框架层硬编码，第二波协议一律经 Clamp* 落地，不许自行发挥）

        /// <summary>完全失控（硬晕/夺输入/强制位移/强制传送）：0 帧，不存在此类协议。
        /// 框架不提供任何写入口，这行常量是给后来者看的墓碑</summary>
        internal const int HardControlFrames = 0;
        /// <summary>移动迟滞上限：减速比例</summary>
        internal const float MaxMoveSlowFraction = 0.40f;
        /// <summary>移动迟滞上限：单条时长（帧）</summary>
        internal const int MaxMoveSlowFrames = 120;
        /// <summary>出手迟滞上限：useTime 放缓比例</summary>
        internal const float MaxUseSlowFraction = 0.35f;
        /// <summary>出手迟滞上限：单条时长（帧）</summary>
        internal const int MaxUseSlowFrames = 360;
        /// <summary>RAM 单次烧蚀上限</summary>
        internal const int MaxRamScorch = 4;
        /// <summary>生命类协议单条全程伤害合计上限</summary>
        internal const int MaxTotalLifeDamage = 120;

        /// <summary>移动减速值进红线：比例与时长双 clamp，协议作者不用自己记数值</summary>
        internal static float ClampMoveSlow(float fraction)
            => MathHelper.Clamp(fraction, 0f, MaxMoveSlowFraction);

        internal static int ClampMoveSlowDuration(int frames)
            => System.Math.Clamp(frames, 0, MaxMoveSlowFrames);

        /// <summary>出手迟滞值进红线</summary>
        internal static float ClampUseSlow(float fraction)
            => MathHelper.Clamp(fraction, 0f, MaxUseSlowFraction);

        internal static int ClampUseSlowDuration(int frames)
            => System.Math.Clamp(frames, 0, MaxUseSlowFrames);

        /// <summary>RAM 烧蚀值进红线</summary>
        internal static int ClampRamScorch(int amount)
            => System.Math.Clamp(amount, 0, MaxRamScorch);

        /// <summary>生命伤害进红线：按"该效果已结算总额"传入，返回本次还能打多少</summary>
        internal static int ClampLifeDamage(int requested, int alreadyDealt) {
            int budget = MaxTotalLifeDamage - System.Math.Max(alreadyDealt, 0);
            return System.Math.Clamp(requested, 0, System.Math.Max(budget, 0));
        }

        #endregion

        /// <summary>
        /// 服务端总开关。运行时在请求校验处读，不需要 ReloadRequired；
        /// 键在 <see cref="CWRServerConfig"/>（共用文件，接线批落地）
        /// </summary>
        internal static bool ServerEnabled => CWRServerConfig.Instance?.HackPvP ?? true;

        /// <summary>
        /// PvP 骇入准入判定，全部条件按序短路。<br/>
        /// 返回 false 时 <paramref name="reason"/> 是拒绝码（服务端拒绝必须点名子句写日志）。<br/>
        /// 端别差异：对冷却与叠加上限只在持有账本的端上生效（服务端授予账 / 客户端镜像），
        /// 复活保护只在观测得到复活事件的端上生效——缺数据的子句自动放行，服务端重验兜底
        /// </summary>
        internal static bool CanTarget(Player attacker, Player defender,
            out HackRequestResultCode reason) {
            //1 服务端总开关
            if (!ServerEnabled) {
                reason = HackRequestResultCode.PvPDisabled;
                return false;
            }
            //基础可用性（含自指排除——自己走 SelfRig 位，不走 PvP 准入）
            if (attacker?.active != true || attacker.dead
                || defender?.active != true || defender.dead || defender.ghost
                || attacker.whoAmI == defender.whoAmI) {
                reason = HackRequestResultCode.InvalidTarget;
                return false;
            }
            //2 双方 hostile（镜像 msg 117 的转播闸：单向 hostile 不可选中）
            if (!attacker.hostile || !defender.hostile) {
                reason = HackRequestResultCode.NotHostile;
                return false;
            }
            //3 队伍谓词（镜像原版弹幕命中：同一支非零队伍互相免疫）
            if (attacker.team != 0 && attacker.team == defender.team) {
                reason = HackRequestResultCode.SameTeam;
                return false;
            }
            //4 PvP 距离（服务端另有 claimedCenter 一致性校验，这里查双方实测距离）。
            //  专用拒绝码：上传期重验按它单独走 45f 拉距宽限，
            //  与 claim 不一致的 InvalidPayload 必须可区分
            if (Vector2.DistanceSquared(attacker.Center, defender.Center)
                > MaxDistance * MaxDistance) {
                reason = HackRequestResultCode.OutOfRange;
                return false;
            }
            //5 复活保护（数据在观测端：服务端脉冲 / 防守方本机，缺席即放行）
            if (PlayerHackAuthority.IsSpawnProtected(defender.whoAmI)) {
                reason = HackRequestResultCode.SpawnProtected;
                return false;
            }
            //6 对冷却（服务端真值；攻击方本机有自己那份镜像供预检变灰）
            if (PlayerHackAuthority.IsPairOnCooldown(attacker.whoAmI, defender.whoAmI)) {
                reason = HackRequestResultCode.PairCooldown;
                return false;
            }
            //7 叠加上限（服务端读授予账，客户端读 PlayerEffectState 镜像）
            if (PlayerHackAuthority.CountEffectsOn(defender.whoAmI) >= MaxEffectsPerDefender
                || PlayerHackAuthority.CountEffectsOnPair(attacker.whoAmI, defender.whoAmI)
                    >= MaxEffectsPerPair) {
                reason = HackRequestResultCode.StackLimit;
                return false;
            }
            //8 状态排除的服务端可见部分（dead/ghost 已在上面；演出保护
            //   服务端不知道，由防守方收到 DefenderApply 时本机终审兜底）
            reason = HackRequestResultCode.Success;
            return true;
        }

        /// <summary>本机是否处于多人环境且 PvP 骇入内容可见（面板/扫描侧的粗闸）</summary>
        internal static bool ContentVisible
            => Main.netMode != NetmodeID.SinglePlayer && ServerEnabled;
    }
}
