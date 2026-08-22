using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.SelfRigs
{
    /// <summary>
    /// SelfRig 三条协议的玩家侧状态。字段归属端：<br/>
    /// · 效果窗口计时（超频/强驱），每个知晓效果的端各自倒数（权威由 OnApply/OnTick 写，
    ///   客户端由 OnReplicated* 写），彼此靠效果包的 elapsed 自愈，权威端为真值；<br/>
    /// · 攻速加成，读本端计时，开火端算伤害所以必须每端都落（照 ArmorParse 的教训）；<br/>
    /// · 掉血代价，仅拥有者本机扣（血量归 owner 客户端，服务端写不进）；<br/>
    /// · RAM 回复抑制，provider 在权威端被 <see cref="RAMPlayer"/> 求值，读权威侧计时；<br/>
    /// · 折算冷却，权威侧为校验真值，拥有者本机镜像一份供面板灰显；<br/>
    /// · 役鬼退款基线/待结算标记，仅权威端；休眠看门狗，权威端执行，拥有者镜像倒计时供显示
    /// </summary>
    internal sealed class SelfRigPlayer : ModPlayer
    {
        #region 常量（协议数值集中在此，便于对账）

        //== 电能折算 ==
        /// <summary>UE→RAM 兑换率</summary>
        internal const float TransmuteUEPerRam = 500f;
        /// <summary>单次折算上限（RAM）</summary>
        internal const int TransmuteMaxRam = 6;
        /// <summary>自身冷却：设计稿 1800f，因"先充枪再折算"的时间闸在当前树不存在
        /// （仓库没有任何储电武器，可折算物只有电池/机器物品），收紧为 3600f，论证见补丁文档</summary>
        internal const int TransmuteCooldownFrames = 60 * 60;

        //== 神经超频 ==
        internal const int OverclockDuration = 60 * 8;
        internal const float OverclockAttackSpeed = 0.4f;
        /// <summary>每次掉血占 statLifeMax2 比例</summary>
        internal const float OverclockDrainRatio = 0.03f;
        internal const int OverclockDrainInterval = 60;
        /// <summary>血线低于此比例时安全阀提前掐断</summary>
        internal const float OverclockCutoffRatio = 0.25f;
        /// <summary>Sandevistan 消耗折半：按帧累计、每 15f 批量回充一次，
        /// 与其快照节奏对齐，避免逐帧脏标记打满发包</summary>
        internal const float SandeRefundRatio = 0.5f;
        private const int SandeRefundFlushInterval = 15;

        //== 役鬼强驱 ==
        internal const int DriveDuration = 60 * 10;
        internal const float DriveErosionBill = 0.12f;
        internal const int DormantDuration = 60 * 60;

        #endregion

        #region 运行时字段（不持久化，进出世界清零）

        internal int OverclockFrames;
        private int overclockDrainTick;

        internal int TransmuteCooldown;

        internal int DriveFrames;
        internal string DriveKey = string.Empty;
        /// <summary>权威端待结算标记：窗口一开就立账，死亡也照常到期结算</summary>
        internal bool DrivePendingSettle;
        private float driveRevivalBaseline;
        private float driveErosionBaseline;

        internal int DormantFrames;
        internal string DormantKey = string.Empty;

        private float timeGearCarry;
        private float sandeRefundCarry;
        private int sandeRefundTick;

        #endregion

        internal bool OverclockActive => OverclockFrames > 0;
        internal bool DriveActive => DriveFrames > 0;

        /// <summary>从 IHackTarget 解析出目标玩家与其自体状态；非 SelfRig 目标返回 false</summary>
        internal static bool TryGet(IHackTarget target, out Player player,
            out SelfRigPlayer rig) {
            player = null;
            rig = null;
            if (target is not SelfRigScannable scan) return false;
            Player candidate = scan.ResolvePlayer();
            if (candidate == null) return false;
            player = candidate;
            return player.TryGetModPlayer(out rig);
        }

        public override void Initialize() => ResetState();

        public override void PlayerDisconnect() {
            //权威端在玩家离场前把账结掉；非 SSC 联机里这笔侵蚀多半来不及同步回
            //拥有者的存档，属已知边界（见补丁文档"缺口"节），至少休眠与本端状态不悬空
            if (Main.netMode != NetmodeID.MultiplayerClient && DrivePendingSettle) {
                SettleDrive();
            }
            ResetState();
        }

        private void ResetState() {
            OverclockFrames = 0;
            overclockDrainTick = 0;
            TransmuteCooldown = 0;
            DriveFrames = 0;
            DriveKey = string.Empty;
            DrivePendingSettle = false;
            driveRevivalBaseline = 0f;
            driveErosionBaseline = 0f;
            DormantFrames = 0;
            DormantKey = string.Empty;
            timeGearCarry = 0f;
            sandeRefundCarry = 0f;
            sandeRefundTick = 0;
        }

        #region 每帧推进

        //攻速写在每个端：伤害由开火的那台客户端算，只写权威端等于没写（照 ArmorParse 的教训）
        public override void UpdateEquips() {
            if (OverclockActive) {
                Player.GetAttackSpeed(DamageClass.Generic) += OverclockAttackSpeed;
            }
        }

        public override void PostUpdate() => Tick(dead: false);

        //死亡时 PostUpdate 不跑；强驱账单、休眠与冷却必须照走（tml-netcode-pitfalls §5.1）
        public override void UpdateDead() {
            //超频对死人没有意义，直接掐掉；效果本体由追踪器按施术者死亡收尾
            OverclockFrames = 0;
            overclockDrainTick = 0;
            Tick(dead: true);
        }

        private void Tick(bool dead) {
            bool authority = Main.netMode != NetmodeID.MultiplayerClient;

            //效果窗口与追踪器同步冻结：时停/时缓期间 Elapsed 不动，这里的窗口也不动。
            //各端自带独立 carry，互不消费
            bool gatedStep = TimeGear.PullFrameAdvance(ref timeGearCarry) > 0;
            if (gatedStep) {
                TickOverclock(dead);
                if (DriveFrames > 0) {
                    DriveFrames--;
                    if (DriveFrames <= 0 && authority && DrivePendingSettle) {
                        SettleDrive();
                    }
                }
            }

            //冷却与休眠走现实帧：它们是玩家层面的表，不吃效果冻结
            if (TransmuteCooldown > 0) TransmuteCooldown--;
            TickDormancy(authority);

            if (authority) {
                TickSandevistanRefund();
                if (DriveActive && Player.TryGetModPlayer(out WraithPlayer wraith)) {
                    WraithDriveShim.RefundWindow(wraith, DriveKey,
                        ref driveRevivalBaseline, ref driveErosionBaseline);
                }
            }
        }

        private void TickOverclock(bool dead) {
            if (OverclockFrames <= 0) return;
            OverclockFrames--;
            if (dead) return;

            //血量归拥有者客户端，代价只在本机扣
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) return;

            if (Player.statLife < Player.statLifeMax2 * OverclockCutoffRatio) {
                //安全阀本机预测提前收；权威端 OnTick 有同一判据做真值
                OverclockFrames = 0;
                return;
            }

            if (++overclockDrainTick < OverclockDrainInterval) return;
            overclockDrainTick = 0;
            int drain = Math.Max(1, (int)(Player.statLifeMax2 * OverclockDrainRatio));
            //真实伤害：不吃防御不可闪避，但最低留 1 HP
            int applied = Math.Min(drain, Player.statLife - 1);
            if (applied <= 0) return;
            Player.statLife -= applied;
            CombatText.NewText(Player.getRect(), CombatText.DamagedFriendly,
                applied, dramatic: false, dot: true);
        }

        private void TickDormancy(bool authority) {
            if (DormantFrames <= 0) return;
            DormantFrames--;

            //看门狗：休眠期内该鬼被结印到任一槽就立刻再摘掉。框架的休眠位在 v2 被废弃，
            //强制卸下是不改共用文件的降级方案；一等成员提案见补丁文档
            if (authority && !string.IsNullOrEmpty(DormantKey)
                && Player.TryGetModPlayer(out WraithPlayer wraith)) {
                int slot = wraith.SlotOf(DormantKey);
                if (slot >= 0) {
                    wraith.TrySetSlotAuthority(slot, string.Empty);
                }
            }

            if (DormantFrames <= 0) {
                DormantKey = string.Empty;
            }
        }

        private void TickSandevistanRefund() {
            if (!OverclockActive) {
                sandeRefundCarry = 0f;
                sandeRefundTick = 0;
                return;
            }
            SandevistanPlayer sande = Sandevistan.GetState(Player);
            if (sande == null || !sande.IsActive || sande.ConsumptionRate <= 0f) return;

            //按 TickAuthority 同一口径估算本帧真实消耗，再折回一半
            float scale = TimeGear.TimeScaleExcluding<SandevistanTimeSlow>();
            if (!float.IsFinite(scale)) scale = 1f;
            scale = Math.Clamp(scale, 0f, 1f);
            sandeRefundCarry += sande.ConsumptionRate * SandeRefundRatio * scale;

            if (++sandeRefundTick < SandeRefundFlushInterval) return;
            sandeRefundTick = 0;
            if (sandeRefundCarry <= 0f) return;
            sande.SetLegacyCooldown(sande.CurrentCooldown + sandeRefundCarry);
            sandeRefundCarry = 0f;
        }

        #endregion

        #region 役鬼强驱：开窗与结算（仅权威端调用写入口）

        internal void BeginDrive(string key, float revivalNow, float erosionNow) {
            DriveKey = key ?? string.Empty;
            DriveFrames = DriveDuration;
            DrivePendingSettle = true;
            driveRevivalBaseline = revivalNow;
            driveErosionBaseline = erosionNow;
        }

        /// <summary>客户端镜像窗口（表现与 HUD 用），不带结算责任</summary>
        internal void MirrorDrive(string key, int remainingFrames) {
            DriveKey = key ?? string.Empty;
            DriveFrames = Math.Max(remainingFrames, 1);
        }

        /// <summary>
        /// 到期结算：一次性侵蚀 +0.12，并强制休眠该鬼一分钟。<br/>
        /// 由效果 OnRemove 或本类倒计时归零触发，Pending 标记保证只结一次；
        /// 施术者死亡不豁免（UpdateDead 里照走）
        /// </summary>
        internal void SettleDrive() {
            if (!DrivePendingSettle) return;
            DrivePendingSettle = false;
            DriveFrames = 0;
            string key = DriveKey;
            DriveKey = string.Empty;
            if (string.IsNullOrEmpty(key)
                || !Player.TryGetModPlayer(out WraithPlayer wraith)) {
                return;
            }

            WraithDriveShim.AddErosion(wraith, DriveErosionBill);
            BeginDormancy(key);
            int slot = wraith.SlotOf(key);
            if (slot >= 0) {
                wraith.TrySetSlotAuthority(slot, string.Empty);
            }
        }

        internal void BeginDormancy(string key) {
            DormantKey = key ?? string.Empty;
            DormantFrames = DormantDuration;
        }

        #endregion
    }

    /// <summary>神经超频期间把 RAM 回复压到零；求和后统一 clamp 到 [0, Max]，负满额即封死</summary>
    internal sealed class NeuralOverclockRamSuppressor : IRamModifierProvider, ICWRLoader
    {
        public int MaxRamBonus => 0;
        public float RecoveryRateBonus => -RamSystem.MaxEffectiveRecoveryRate;
        public bool IsActive(Player player)
            => player?.active == true
                && player.TryGetModPlayer(out SelfRigPlayer rig)
                && rig.OverclockActive;

        void ICWRLoader.LoadData() => RamSystem.RegisterProvider(this);
        void ICWRLoader.UnLoadData() => RamSystem.UnregisterProvider(this);
    }
}
