using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using System;
using System.Text;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 惊吓档期号。S 级（每次进入 1 次）：LampFall 窗火蔓延熄灭、Moon 云后月轮；
    /// A 级（各 ≤2 次）：LampBehind 回头灯灭、StillBell 无风铃、Footprints 泥地脚印、Geta 雾里木屐；
    /// B 级（不占槽，仅 debug 直通标识）：CrowOmen 鸦群惊起、TorchLine 远山火把队列。
    /// 成员一次性全量预植（C/D/E 包不再改此枚举）。
    /// </summary>
    internal enum KiyumeScareId
    {
        LampFall,
        Moon,
        LampBehind,
        StillBell,
        Footprints,
        Geta,
        CrowOmen,
        TorchLine,
    }

    /// <summary>
    /// 鬼梦氛围导演（KIY-P5-B）：单一仲裁人。持有紧张度曲线（EMA 平滑）、惊吓槽
    /// （同刻至多一个 A/S 演出）、分级预算与共享冷却、P2 犬让位开关、
    /// 守田人静默区统一门（裁决 16，W4 收口为一处实现）。
    /// 灯火/天幕/微演出以消费者身份 <see cref="TryClaimScare"/> 申请档期；床层不经审批，
    /// 只吃 <see cref="KiyumeSoundscape.PushDuck"/>；B 级点缀自走周期只读让位门。
    /// 军备门制镜像 DungeonworldSnuff。<br/>
    /// 权威端+同步字段：无。刻意的每客户端本地事件——各人被吓的时机不同，
    /// 正是"独自遇鬼"的叙事。本类 static 状态只是本地演出进度，非 per-player 游戏状态，
    /// netcode 静态禁令不适用（DungeonworldSnuff 同款口径）。不生成任何实体，犬位只读。
    /// </summary>
    internal class KiyumeDirector : ModSystem
    {
        private static float tension;
        /// <summary>紧张度 0..1（事件模块只读）</summary>
        internal static float Tension => tension;

        //惊吓槽与冷却
        private static int slotOwner = -1;    //-1=空闲，否则=(int)KiyumeScareId
        private static int sharedCooldown;
        private static int calmTimer;
        private static readonly int[] budgetLeft = new int[Enum.GetValues<KiyumeScareId>().Length];

        //军备追踪（全部 LocalPlayer 本地，镜像 DungeonworldSnuff）
        private static int worldTicks;
        private static int sinceHurt = int.MaxValue / 2;
        private static int lastLife = -1;

        //==================== 对外接口（事件模块消费面）====================

        /// <summary>
        /// P2 犬位唯一消费口：事件模块只从这里读犬（数据来自声景包的 4f 扫描缓存，
        /// P2-C 名单落地前恒 false）。只读位置，绝不改实体字段。
        /// </summary>
        internal static bool HoundNearby(out Vector2 pos, out float dist) {
            pos = KiyumeSoundscape.HoundPos;
            dist = KiyumeSoundscape.HoundDist;
            return KiyumeSoundscape.HoundFound;
        }

        /// <summary>P2 让位开关：最近犬 &lt;900px 时 A/S 全挂起（真实威胁在场，假吓是干扰）</summary>
        internal static bool HoundYieldActive
            => KiyumeSoundscape.HoundFound && KiyumeSoundscape.HoundDist < KiyumeScore.HoundYieldDistPx;

        /// <summary>
        /// 申请惊吓档期（原子：过十门 + 占槽 + 盖共享冷却 + 扣预算）。
        /// minT/maxT 为事件自报紧张度窗（公约窗见 <see cref="KiyumeScore.ScareWindowLo"/>/Hi）；
        /// lotteryOneIn 为事件自报抽签分母（≤0 用 <see cref="KiyumeScore.DefaultScareLottery"/>）。
        /// 事件在自身额外条件（带位/驻留等）成立的 tick 里持续调用，拿到 true 即开演，
        /// 收尾必须 <see cref="ReleaseScare"/>。
        /// </summary>
        internal static bool TryClaimScare(KiyumeScareId id, float minT, float maxT, int lotteryOneIn = 0) {
            if (Main.dedServ || !KiyumeWorld.Active || Main.gameMenu || KiyumeDirectorDebug.DisableScares) {
                return false;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            //门6 槽独占：debug 武装也不豁免，同刻至多一个 A/S 演出
            if (slotOwner >= 0) {
                return false;
            }
            //门10 守田人静默区（裁决 16，W4 收口统一门）：物理门，武装也拦——
            //场地气氛归守田人，氛围层 A/S 一律避让，事件模块不再各自实现
            if (InScarecrowSilence(player)) {
                return false;
            }
            bool armed = KiyumeDirectorDebug.PeekArm(id);
            if (!armed) {
                //门8 事件自身预算
                if (budgetLeft[(int)id] <= 0) {
                    return false;
                }
                //门1 入世界热身（落地先看世界再闹鬼）；门2 近伤不吓
                if (worldTicks < KiyumeScore.WarmupTicks || sinceHurt < KiyumeScore.NoHurtTicks) {
                    return false;
                }
                //门3 脚踏实地（坠落/腾空中不触发）
                if (player.velocity.Y != 0f) {
                    return false;
                }
                //门4 紧张度窗（太低没铺垫，太高玩家已应激，再吓是噪音）
                if (tension < minT || tension > maxT) {
                    return false;
                }
                //门5 共享惊吓冷却
                if (sharedCooldown > 0) {
                    return false;
                }
                //门7 P2 让位
                if (HoundYieldActive) {
                    return false;
                }
                //门9 抽签（事件自报概率）
                int oneIn = lotteryOneIn > 0 ? lotteryOneIn : KiyumeScore.DefaultScareLottery;
                if (Main.rand.Next(oneIn) != 0) {
                    return false;
                }
            }
            //过门：消费武装、占槽、盖共享冷却、扣预算、发鸦群前置信号
            KiyumeDirectorDebug.ConsumeArm(id);
            slotOwner = (int)id;
            sharedCooldown = KiyumeScore.GlobalScareCooldown;
            if (budgetLeft[(int)id] > 0) {
                budgetLeft[(int)id]--;
            }
            NotifyCrowOmen(player.Center);
            return true;
        }

        /// <summary>演出收尾释放槽，并点火泄压包络（300f 内紧张度目标 0.55→1 缓回）</summary>
        internal static void ReleaseScare(KiyumeScareId id) {
            if (slotOwner != (int)id) {
                return;
            }
            slotOwner = -1;
            calmTimer = KiyumeScore.CalmTicks;
        }

        /// <summary>
        /// 守田人静默区（裁决 16 统一门）：旱田外扩 500px 内 A/S 一律静默。
        /// W4 收口：原 C/E 包各事件的本地避让上收至此，一处实现防漂移。
        /// <see cref="KiyumeStructures.ScarecrowPlot"/> 由生成端写入，联机客户端恒 null →
        /// 门自动放行（导演是本地系统，单人有值即够；客户端漏避为已接受口径）。
        /// </summary>
        internal static bool InScarecrowSilence(Player player) {
            if (KiyumeStructures.ScarecrowPlot is not Rectangle plot) {
                return false;
            }
            Rectangle worldRect = new(plot.X * 16, plot.Y * 16, plot.Width * 16, plot.Height * 16);
            worldRect.Inflate(500, 500);
            return worldRect.Contains(player.Center.ToPoint());
        }

        /// <summary>请求鸦群前置信号：A/S 档期过门时自动转发一次（训练"鸦飞=有事"的语法）</summary>
        internal static void NotifyCrowOmen(Vector2 origin) {
            if (Main.dedServ) {
                return;
            }
            //E 包接收侧：从 origin 附近地面惊起一群。转发发生在档期过门帧，
            //提前量=惊起演出全长 180~300f（鸦群先于事件高潮在场，详 KiyumeCrowFlight）
            KiyumeCrowFlight.StartleFrom(origin);
        }

        //==================== 生命周期 ====================

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();

        public override void Unload() {
            HardReset();
            KiyumeDirectorDebug.ForceTension = -1f;
            KiyumeDirectorDebug.DisableScares = false;
        }

        private static void HardReset() {
            //ShouldSave=false：每次进入是新的一夜，预算回满
            tension = 0f;
            slotOwner = -1;
            sharedCooldown = 0;
            calmTimer = 0;
            worldTicks = 0;
            sinceHurt = int.MaxValue / 2;
            lastLife = -1;
            for (int i = 0; i < budgetLeft.Length; i++) {
                budgetLeft[i] = KiyumeScore.ScareBudget((KiyumeScareId)i);
            }
            KiyumeDirectorDebug.ResetArms();
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (!KiyumeWorld.Active || Main.gameMenu || KiyumeAmbienceSystem.Presence < 0.01f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            if (worldTicks < int.MaxValue / 2) {
                worldTicks++;
            }
            if (sharedCooldown > 0) {
                sharedCooldown--;
            }
            if (calmTimer > 0) {
                calmTimer--;
            }
            TrackHurt(player);
            UpdateTension(player);
        }

        private static void TrackHurt(Player player) {
            if (lastLife < 0) {
                lastLife = player.statLife;
            }
            if (player.statLife < lastLife) {
                sinceHurt = 0;
            }
            else if (sinceHurt < int.MaxValue / 2) {
                sinceHurt++;
            }
            lastLife = player.statLife;
        }

        private static void UpdateTension(Player player) {
            if (KiyumeDirectorDebug.ForceTension >= 0f) {
                tension = MathHelper.Clamp(KiyumeDirectorDebug.ForceTension, 0f, 1f);
                return;
            }
            //预算红线：全导演每 tick 仅此一次 DensityAt（O(1) 窗口采样），NPC 扫描复用声景包缓存
            float fog = KiyumeFogSim.DensityAt(player.Center);
            float submerged = player.Center.Y > KiyumeFogTide.SurfaceAt(player.Center.X) ? 1f : 0f;
            float hound = KiyumeSoundscape.HoundFound
                ? MathHelper.Clamp(1f - KiyumeSoundscape.HoundDist / KiyumeScore.HoundFactorSpanPx, 0f, 1f)
                : 0f;
            //带序 1 滩涂 / 3 枯林：前不着村后不着店
            int band = KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f));
            float strand = band == 1 || band == 3 ? KiyumeScore.WStrand : 0f;
            float target = MathHelper.Clamp(
                fog * KiyumeScore.WFog + submerged * KiyumeScore.WSubmerged
                + hound * KiyumeScore.WHound + strand, 0f, 1f);
            //吓完泄压：惊吓结束后 300f 内目标压到 0.55 再缓回
            if (calmTimer > 0) {
                target *= MathHelper.Lerp(1f, KiyumeScore.CalmFloor, calmTimer / (float)KiyumeScore.CalmTicks);
            }
            tension = MathHelper.Lerp(tension, target, KiyumeScore.TensionLerp);
        }

        /// <summary>一行状态摘要（TestItem 验收用，经 KiyumeDirectorDebug.StatusLine 暴露）</summary>
        internal static string BuildStatusLine() {
            var sb = new StringBuilder(160);
            sb.Append($"[氛围导演] tension{tension:F2}");
            sb.Append($" 槽{(slotOwner >= 0 ? ((KiyumeScareId)slotOwner).ToString() : "空")}");
            sb.Append($" 共冷{sharedCooldown} 热身{Math.Min(worldTicks, KiyumeScore.WarmupTicks)}/{KiyumeScore.WarmupTicks}");
            sb.Append($" 未伤{Math.Min(sinceHurt, 9999)}f");
            sb.Append(KiyumeSoundscape.HoundFound
                ? $" 犬{KiyumeSoundscape.HoundDist:F0}px{(HoundYieldActive ? "(让位)" : "")}"
                : " 犬无");
            Player statusPlayer = Main.LocalPlayer;
            if (statusPlayer != null && statusPlayer.active && InScarecrowSilence(statusPlayer)) {
                sb.Append(" 守田静默");
            }
            sb.Append(" 预算[");
            bool first = true;
            for (int i = 0; i < budgetLeft.Length; i++) {
                var id = (KiyumeScareId)i;
                if (KiyumeScore.ScareBudget(id) <= 0) {
                    continue;    //B 级不占槽，不进预算行
                }
                if (!first) {
                    sb.Append(' ');
                }
                first = false;
                sb.Append(id).Append(':').Append(budgetLeft[i]);
            }
            sb.Append(']');
            if (KiyumeDirectorDebug.DisableScares) {
                sb.Append(" 已禁吓");
            }
            if (KiyumeDirectorDebug.ForceTension >= 0f) {
                sb.Append($" 伪tension{KiyumeDirectorDebug.ForceTension:F2}");
            }
            return sb.ToString();
        }
    }

    /// <summary>氛围导演 debug 静态口（TestItem 验收用，全部本地）</summary>
    internal static class KiyumeDirectorDebug
    {
        /// <summary>伪紧张度：≥0 时直接顶替合成值（跳过 EMA），调窗验收用；-1=关闭</summary>
        internal static float ForceTension = -1f;
        /// <summary>全局禁吓：A/S 一律不放行（武装也拦），光敏感/纯观光降级口</summary>
        internal static bool DisableScares;

        //武装标记：ArmScare 置位，A/S 事件在 TryClaimScare 过门时消费；
        //B 级模块（TorchLine 等）可自行 ConsumeArm 直通
        private static readonly bool[] armed = new bool[Enum.GetValues<KiyumeScareId>().Length];

        /// <summary>
        /// 立即武装：对应事件下次 TryClaimScare 跳过热身/未伤/站立/窗口/冷却/让位/预算/抽签，
        /// 仍守槽独占与守田人静默区（物理门口径，与各事件的带位/灯量/风窗/雾门一致）。
        /// CrowOmen 特例：不武装，立即从玩家位请求一次鸦群惊起。
        /// </summary>
        internal static void ArmScare(KiyumeScareId id) {
            if (id == KiyumeScareId.CrowOmen) {
                Player player = Main.LocalPlayer;
                if (player != null && player.active) {
                    KiyumeDirector.NotifyCrowOmen(player.Center);
                }
                return;
            }
            armed[(int)id] = true;
        }

        internal static bool PeekArm(KiyumeScareId id) => armed[(int)id];

        internal static bool ConsumeArm(KiyumeScareId id) {
            bool wasArmed = armed[(int)id];
            armed[(int)id] = false;
            return wasArmed;
        }

        internal static void ResetArms() {
            for (int i = 0; i < armed.Length; i++) {
                armed[i] = false;
            }
        }

        /// <summary>tension/槽/冷却/预算/犬距一行摘要</summary>
        internal static string StatusLine() => KiyumeDirector.BuildStatusLine();
    }
}
