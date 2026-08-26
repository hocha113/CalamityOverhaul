using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 旧网深潜会话：未结算账本（刻意不落存档，弹出即清是机制本身）、
    /// 噪音状态（源/衰减/四档阈值）、距离底噪扣 RAM、死亡/耗尽的强制弹出、会话统计
    /// </summary>
    internal class OldNetPlayer : ModPlayer
    {
        /// <summary>本次深潜未铭刻的六类碎片</summary>
        internal int[] PendingShards = new int[SHPCData.SlotCount];
        /// <summary>本次深潜采集节点数</summary>
        internal int HarvestCount;
        /// <summary>
        /// 已投保的六类碎片快照（保险契约终端写入）：烧断/死亡时按 min(投保,在账) 兑付。
        /// TODO MP: 投保快照与兑付写库是本机 per-player 语义，服务器权威化时随结算整体重排
        /// </summary>
        internal int[] InsuredShards = new int[SHPCData.SlotCount];

        //════════ 噪音状态（per-player，刻意不落存档；MP 时服务器权威后置）════════

        /// <summary>当前噪音 0..100</summary>
        internal float Noise;
        /// <summary>档位缓存 0..4，带迟滞</summary>
        internal int NoiseTier;
        //无新增噪音的连续帧数
        private int quietTimer;
        //T4 触发后的衰减免疫倒数
        private int t4DecayImmuneTimer;
        /// <summary>回收官静默余量：噪音增量减半的会话截止拍（DiveTicks 口径，0=无余量）</summary>
        internal int WardenGraceTicks;

        //════════ 账本容量（成长钩子留缝：进世界时由 RAM build/义体/模块写入，M1 恒 0）════════

        internal int LedgerCapacityBonus;
        internal int LedgerCapacity => OldNetMetrics.LedgerBaseCapacity + LedgerCapacityBonus;

        internal int PendingTotal {
            get {
                int total = 0;
                for (int i = 0; i < PendingShards.Length; i++) {
                    total += PendingShards[i];
                }
                return total;
            }
        }

        //════════ 会话统计（战报屏数据源，进世界复位）════════

        /// <summary>最远离墙距离（列）</summary>
        internal int MaxDepthCols;
        /// <summary>本次深潜铭刻总数（中继+登出累计）</summary>
        internal int SettledTotal;
        /// <summary>被追次数（director 每次生成猎杀小队 +1）</summary>
        internal int HuntedCount;
        /// <summary>深潜用时（tick）</summary>
        internal int DiveTicks;
        /// <summary>被目击次数（评级幽灵潜行判据；余震/热断链的自招响应不计）</summary>
        internal int SpottedCount;
        /// <summary>击杀巡逻 ICE 数（评级判据）</summary>
        internal int PatrolKills;
        /// <summary>击毁哨戒炮塔数（评级判据）</summary>
        internal int TurretKills;
        /// <summary>本潜到达过的最高噪音档位（评级判据）</summary>
        internal int MaxTierReached;

        //════════ 加密节点引导会话（站桩破解，本机语义）════════

        /// <summary>引导中的节点坐标；(-1,-1) = 无</summary>
        internal Point ChannelNode = new(-1, -1);
        /// <summary>引导累计帧</summary>
        internal int ChannelTimer;

        internal bool Channeling => ChannelNode.X >= 0;

        /// <summary>引导进度 0..1（节点绘制读取）</summary>
        internal float ChannelProgress => Channeling
            ? MathHelper.Clamp(ChannelTimer / (float)OldNetMetrics.EncryptChannelTicks, 0f, 1f) : 0f;

        //════════ 衰减区余震（2.9：疯域的加密锁反咬，破解完成 3s 后必引猎杀）════════

        /// <summary>余震倒数（tick），0=无。TODO MP: 本机会话字段，联机化随归属端仲裁</summary>
        internal int AftershockTimer;

        //════════ 热断链（2.3：高热撤离的 10 秒站桩终曲）════════

        /// <summary>断链中的终端坐标；(-1,-1)=无</summary>
        internal Point HotExtractNode = new(-1, -1);
        /// <summary>断链剩余帧。TODO MP: 完成判定需服务器仲裁</summary>
        internal int HotExtractTimer;
        /// <summary>本潜完成过热断链（评级风格旗标）</summary>
        internal bool HotExtractDone;

        internal bool HotExtracting => HotExtractNode.X >= 0;

        /// <summary>热断链门槛：T3+ 或清剿波在场时，登出改走 10 秒站桩断链（终端悬停/配色共用）</summary>
        internal bool HotExtractEligible => NoiseTier >= 3 || OldNetICEDirector.CleanupWaveActive;

        //弹出去抖：ExitWorld 到真正离开有延迟，防重复触发
        private bool ejecting;
        //死亡弹出挂起：Kill 时标记，OnRespawn 时执行
        private bool ejectOnRespawn;
        //烧断弹出倒数：先闪红，倒数到红峰帧才真正 ExitWorld
        private int ejectDelay;
        //L3 领域下潜蓄力（主世界侧，进阶入口）
        private int l3DiveHold;
        private bool l3ChargeShown;

        internal static OldNetPlayer Get(Player player) => player.GetModPlayer<OldNetPlayer>();

        internal void ResetLedger() {
            for (int i = 0; i < PendingShards.Length; i++) {
                PendingShards[i] = 0;
            }
            HarvestCount = 0;
        }

        private void ResetSession() {
            ResetLedger();
            Noise = 0f;
            NoiseTier = 0;
            quietTimer = 0;
            t4DecayImmuneTimer = 0;
            WardenGraceTicks = 0;
            LedgerCapacityBonus = 0;
            Array.Clear(InsuredShards, 0, InsuredShards.Length);
            MaxDepthCols = 0;
            SettledTotal = 0;
            HuntedCount = 0;
            DiveTicks = 0;
            SpottedCount = 0;
            PatrolKills = 0;
            TurretKills = 0;
            MaxTierReached = 0;
            AftershockTimer = 0;
            HotExtractNode = new Point(-1, -1);
            HotExtractTimer = 0;
            HotExtractDone = false;
            ChannelNode = new Point(-1, -1);
            ChannelTimer = 0;
        }

        //════════ 噪音入口 ════════

        /// <summary>
        /// 统一噪音入口：时停期间增量 ×0.25（时停考古的低噪路线）。
        /// RAM 距离底噪不产噪音，那是信号成本，不是声响
        /// </summary>
        internal void AddNoise(float amount) {
            if (!OldNetWorld.Active || amount <= 0f) {
                return;
            }
            if (WorldFreezeSystem.IsActive) {
                amount *= OldNetMetrics.NoiseFreezeMul;
            }
            //回收官静默余量：奖励期内一切增量减半（截止拍语义，见 SilenceNoise）
            amount *= WardenGraceTicks > DiveTicks ? OldNetMetrics.WardenGraceNoiseMul : 1f;
            Noise = MathHelper.Clamp(Noise + amount, 0f, 100f);
            quietTimer = 0;
        }

        /// <summary>把噪音直接抬到地板值（事件节点拉闸），无视时停系数</summary>
        internal void SetNoiseFloor(float floor) {
            if (!OldNetWorld.Active) {
                return;
            }
            Noise = MathHelper.Clamp(MathF.Max(Noise, floor), 0f, 100f);
            quietTimer = 0;
        }

        /// <summary>
        /// 全网静默（回收官击杀奖励，与 SetNoiseFloor 反向：只降不升）：
        /// 噪音直落到 cap，同时清 T4 衰减免疫不留幽灵计时，
        /// 并开启静默余量（WardenGraceTicks 内噪音增量减半）。
        /// TODO MP: per-player 语义，联机化归属端裁决
        /// </summary>
        internal void SilenceNoise(float cap) {
            if (!OldNetWorld.Active) {
                return;
            }
            Noise = MathHelper.Clamp(MathF.Min(Noise, cap), 0f, 100f);
            t4DecayImmuneTimer = 0;
            quietTimer = 0;
            WardenGraceTicks = DiveTicks + OldNetMetrics.WardenGraceTicks;
        }

        //带迟滞的档位推进：升档达到阈值即升，跌档需再低 Hysteresis 点
        private void UpdateNoiseTier() {
            int tier = NoiseTier;
            while (tier < 4 && Noise >= TierThreshold(tier + 1)) {
                tier++;
                if (tier == 4) {
                    t4DecayImmuneTimer = OldNetMetrics.NoiseT4DecayImmuneTicks;
                }
            }
            while (tier > 0 && Noise < TierThreshold(tier) - OldNetMetrics.NoiseTierHysteresis) {
                tier--;
            }
            NoiseTier = tier;
            //评级埋点：本潜档位高水位
            MaxTierReached = Math.Max(MaxTierReached, tier);
        }

        internal static float TierThreshold(int tier) => tier switch {
            1 => OldNetMetrics.NoiseT1,
            2 => OldNetMetrics.NoiseT2,
            3 => OldNetMetrics.NoiseT3,
            4 => OldNetMetrics.NoiseT4,
            _ => 0f,
        };

        //════════ 噪音源钩子（只认本机玩家；MP 化时改服务器权威 TODO）════════

        public override bool Shoot(Item item, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (OldNetWorld.Active && Player.whoAmI == Main.myPlayer) {
                AddNoise(OldNetMetrics.NoiseShoot);
            }
            return true;
        }

        public override void PostItemCheck() {
            if (!OldNetWorld.Active || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //任意武器挥动：射击武器与 Shoot 叠加（枪比刀响）
            if (Player.ItemAnimationJustStarted && Player.HeldItem?.damage > 0) {
                AddNoise(OldNetMetrics.NoiseSwing);
            }
        }

        //════════ 采集与结算 ════════

        /// <summary>采集入账（节点右键调用，本机）。满载拒收返回 false，碎片不消散</summary>
        internal bool TryAddHarvest(int category, int count) {
            if (category < 0 || category >= PendingShards.Length || count <= 0) {
                return false;
            }
            if (PendingTotal + count > LedgerCapacity) {
                return false;
            }
            PendingShards[category] += count;
            HarvestCount++;
            return true;
        }

        //════════ 加密节点引导 ════════

        /// <summary>开始站桩引导（加密节点右键调用，本机）。满载直接拒绝</summary>
        internal void StartChannel(int i, int j) {
            if (PendingTotal >= LedgerCapacity) {
                NotifyLedgerFull(new Vector2(i * 16 + 8, j * 16 + 8));
                return;
            }
            ChannelNode = new Point(i, j);
            ChannelTimer = 0;
            if (Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.55f, Pitch = -0.2f },
                    new Vector2(i, j) * 16f);
            }
        }

        /// <summary>中断引导：计时清零、节点保留</summary>
        internal void CancelChannel() {
            if (!Channeling) {
                return;
            }
            ChannelNode = new Point(-1, -1);
            ChannelTimer = 0;
            if (Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.6f }, Player.Center);
            }
        }

        //引导推进：站桩 + 未受击才累计；期间高噪音（站桩引导 = 主动点亮自己）
        private void TickChannel() {
            if (!Channeling) {
                return;
            }
            Point node = ChannelNode;
            Tile tile = Framing.GetTileSafely(node.X, node.Y);
            if (!tile.HasTile || tile.TileType != ModContent.TileType<Tiles.OldNetEncryptedNodeTile>()) {
                //节点没了（理论上只有完成路径）：静默清态
                ChannelNode = new Point(-1, -1);
                ChannelTimer = 0;
                return;
            }
            Vector2 nodeCenter = new(node.X * 16 + 8, node.Y * 16 + 8);
            if (Vector2.Distance(Player.Center, nodeCenter) > OldNetMetrics.EncryptChannelRadius) {
                CancelChannel();
                return;
            }

            AddNoise(OldNetMetrics.NoiseChannelPerSecond / 60f);
            ChannelTimer += ChannelStepNow();
            if (ChannelTimer < OldNetMetrics.EncryptChannelTicks) {
                return;
            }

            //引导完成：普通节点同分布 ×3；衰减区成功破解 → 余震（满载拒收不触发，失败的破解不挨打）
            ChannelNode = new Point(-1, -1);
            ChannelTimer = 0;
            if (Tiles.OldNetEncryptedNodeTile.CompleteHarvest(node.X, node.Y, this)
                && node.X >= OldNetMetrics.FadeLeft) {
                AftershockTimer = OldNetMetrics.AftershockDelayTicks;
                CombatText.NewText(Player.getRect(), new Color(235, 64, 44),
                    OldNetTexts.OldNetAftershockWarn.Value, dramatic: true);
            }
        }

        /// <summary>
        /// 加密引导步进（共享聚合口，06 §1 修饰符纪律）：多来源取最强档、不叠乘。
        /// 收网协议激活 → 2；带宽跌落/解封冲刺等未来来源并入此处判定
        /// </summary>
        private static int ChannelStepNow()
            => OldNetICEDirector.DragnetActive ? 2 : 1;

        //余震倒数：疯域的锁反咬，读秒结束引来一次猎杀响应
        private void TickAftershock() {
            if (AftershockTimer <= 0) {
                return;
            }
            //每秒一记低鸣：回溯的读秒
            if (AftershockTimer % 60 == 0) {
                SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.4f, Pitch = -0.5f }, Player.Center);
            }
            if (--AftershockTimer > 0) {
                return;
            }
            //回溯完成：一次性噪音 + 猎杀响应（幽灵豁免：余震是系统回礼，不计目击）
            AddNoise(OldNetMetrics.AftershockNoise);
            OldNetICEDirector.NotifySpotted(Player, countAsSpotted: false);
            CombatText.NewText(Player.getRect(), new Color(235, 64, 44),
                OldNetTexts.OldNetAftershockHit.Value, dramatic: true);
        }

        public override void OnHurt(Player.HurtInfo info) {
            //受击打断引导
            if (OldNetWorld.Active && Channeling) {
                CancelChannel();
            }
        }

        /// <summary>满载拒收反馈：红字 + Fault 音 + HUD 读数红闪（硬拒绝必须被告知）</summary>
        internal void NotifyLedgerFull(Vector2 worldPos) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            CombatText.NewText(new Rectangle((int)worldPos.X - 8, (int)worldPos.Y - 8, 16, 16),
                new Color(255, 120, 60), OldNetTexts.OldNetLedgerFull.Value, dramatic: true);
            SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.5f }, worldPos);
            UI.OldNetHud.FlashLedger();
        }

        /// <summary>
        /// 结算账本：写进 SHPCPlayer.MoldShards（本机 per-player 写入）并清账，
        /// 返回本次铭刻数。登出终端与中继站共用
        /// </summary>
        internal int SettleLedger() {
            int total = 0;
            SHPCPlayer shpc = SHPCPlayer.Get(Player);
            if (shpc?.MoldShards != null) {
                for (int i = 0; i < PendingShards.Length && i < shpc.MoldShards.Length; i++) {
                    shpc.MoldShards[i] += PendingShards[i];
                    total += PendingShards[i];
                }
            }
            SettledTotal += total;
            ResetLedger();
            return total;
        }

        /// <summary>
        /// 保险兑付（灾难弹出专用，Kill/ForceEject 在 CacheReport 之前调用）：
        /// 逐类 min(投保, 在账) 写进 MoldShards 并从账本移除（兑付即铭刻，计入 SettledTotal），
        /// min 防"投保后已在中继结算过"的双赔；安全登出不走这里，保费沉没。
        /// 顺序契约：先兑付再 CacheReport，战报 LostPending 只计未投保的净损失
        /// </summary>
        private void PayoutInsurance() {
            int total = 0;
            SHPCPlayer shpc = SHPCPlayer.Get(Player);
            if (shpc?.MoldShards != null) {
                for (int i = 0; i < InsuredShards.Length && i < shpc.MoldShards.Length; i++) {
                    int pay = Math.Min(InsuredShards[i], PendingShards[i]);
                    if (pay <= 0) {
                        continue;
                    }
                    shpc.MoldShards[i] += pay;
                    PendingShards[i] -= pay;
                    total += pay;
                }
            }
            Array.Clear(InsuredShards, 0, InsuredShards.Length);
            if (total <= 0) {
                return;
            }
            SettledTotal += total;
            if (Player.whoAmI == Main.myPlayer) {
                CombatText.NewText(Player.getRect(), new Color(190, 150, 60),
                    OldNetTexts.OldNetEscrowPayout.Format(total), dramatic: true);
                SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = -0.3f, Volume = 0.7f },
                    Player.Center);
            }
        }

        /// <summary>
        /// 热断链启动（登出终端高热分支）：10 秒站桩换立即离场。
        /// 噪音直抬 T4（终曲必须响，档位跃迁自带 HUD 白闪与派遣音），
        /// 受击不打断（门已开，扛住就行）；离台超 90px 中止
        /// </summary>
        internal void StartHotExtract(int i, int j) {
            if (ejecting || HotExtracting) {
                return;
            }
            //先校验后收价（二审修复）：交互触达可超中止半径，太远启动会在下一帧
            //立即中止=白吃 T4 地板与猎杀波，此处拒绝启动且不收任何代价
            Vector2 termCenter = new(i * 16 + 8, j * 16 + 8);
            if (Vector2.Distance(Player.Center, termCenter) > OldNetMetrics.HotExtractRadius) {
                if (Player.whoAmI == Main.myPlayer) {
                    CombatText.NewText(Player.getRect(), new Color(255, 150, 50),
                        OldNetTexts.OldNetHotExtractTooFar.Value);
                }
                return;
            }
            HotExtractNode = new Point(i, j);
            HotExtractTimer = OldNetMetrics.HotExtractTicks;
            SetNoiseFloor(OldNetMetrics.NoiseT4);
            if (Player.whoAmI == Main.myPlayer) {
                UI.OldNetHud.PushBanner(OldNetTexts.OldNetHotExtractStart.Value);
                SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.7f, Pitch = -0.3f },
                    Player.Center);
            }
            //启动瞬间先来一波（自招的响应，不计目击）
            OldNetICEDirector.NotifySpotted(Player, countAsSpotted: false);
        }

        //热断链静默清态（烧断/死亡竞态用）：弹出演出期间 HUD 的 SEVERING 行不得残留冻结
        private void CancelHotExtract() {
            HotExtractNode = new Point(-1, -1);
            HotExtractTimer = 0;
        }

        //热断链泵：站桩计时，离台中止（走开=放弃，无额外惩罚），完成走 SettleAndLogout 原路径
        private void TickHotExtract() {
            if (!HotExtracting) {
                return;
            }
            Vector2 center = new(HotExtractNode.X * 16 + 8, HotExtractNode.Y * 16 + 8);
            if (Vector2.Distance(Player.Center, center) > OldNetMetrics.HotExtractRadius) {
                HotExtractNode = new Point(-1, -1);
                HotExtractTimer = 0;
                CombatText.NewText(Player.getRect(), new Color(255, 150, 50),
                    OldNetTexts.OldNetHotExtractAbort.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.6f },
                    Player.Center);
                return;
            }
            //压力供给：每 4s 追加一只猎杀（NotifySpotted 受清剿波补员上限封顶，不会堆屏）
            int elapsed = OldNetMetrics.HotExtractTicks - HotExtractTimer;
            if (elapsed > 0 && elapsed % OldNetMetrics.HotExtractWaveInterval == 0) {
                OldNetICEDirector.NotifySpotted(Player, countAsSpotted: false);
            }
            if (--HotExtractTimer > 0) {
                return;
            }
            //完成：置风格旗标后走既有结算原路径（零复制，DiveCompleted 等语义天然兼容）
            HotExtractNode = new Point(-1, -1);
            HotExtractDone = true;
            SettleAndLogout();
        }

        /// <summary>登出终端：结算账本后安全断链回主世界</summary>
        internal void SettleAndLogout() {
            //烧断倒数期间不再接受安全登出（弹出已成事实）
            if (ejecting) {
                return;
            }
            int total = SettleLedger();
            //战报缓存在结算之后：SettledTotal 要含最后一笔
            UI.OldNetDebriefPanel.CacheReport(this, UI.OldNetExitKind.SafeLogout);

            if (Player.whoAmI == Main.myPlayer) {
                string text = total > 0
                    ? OldNetTexts.OldNetSettleDone.Format(total)
                    : OldNetTexts.OldNetSettleEmpty.Value;
                CombatText.NewText(Player.getRect(), new Color(120, 255, 170), text, dramatic: total > 0);
                SoundEngine.PlaySound(SoundID.ResearchComplete, Player.Center);
            }

            ejecting = true;
            if (Player.whoAmI == Main.myPlayer) {
                //首潜委托完成判据：一次安全登出
                Player.GetModPlayer<Narrative.Data.StoryPlayer>()
                    .Get<Narrative.Data.Modules.OldNetGuideData>().DiveCompleted = true;
                OldNetWorld.ExitWorld();
            }
        }

        /// <summary>链路烧断：清账本、闪红转场、倒数到红峰帧弹出</summary>
        private void ForceEject(string reason) {
            if (ejecting) {
                return;
            }
            ejecting = true;
            //保险兑付先于战报快照：LostPending 只计未投保的净损失
            PayoutInsurance();
            //战报缓存先于清账（HarvestCount/PendingTotal 随 ResetLedger 归零）
            UI.OldNetDebriefPanel.CacheReport(this, UI.OldNetExitKind.RamBurnout);
            ResetLedger();
            CancelChannel();
            CancelHotExtract();
            if (Player.whoAmI == Main.myPlayer) {
                CombatText.NewText(Player.getRect(), Color.OrangeRed, reason, dramatic: true);
                SoundEngine.PlaySound(CWRSound.Fault, Player.Center);
                UI.OldNetEjectFlash.Begin();
                ejectDelay = UI.OldNetEjectFlash.TotalFrames;
            }
        }

        public override void OnEnterWorld() {
            ejecting = false;
            ejectOnRespawn = false;
            ejectDelay = 0;
            //进旧网=新会话清账；回主世界=兜底清账（非登出途径离开视同弹出）
            ResetSession();
            if (!OldNetWorld.Active) {
                //先恢复快照再弹战报（§4.5 的执行顺序）
                OldNetGuard.RestoreOnReturn();
                if (Player.whoAmI == Main.myPlayer) {
                    UI.OldNetDebriefPanel.ConsumePending();
                }
            }
            else if (Player.whoAmI == Main.myPlayer) {
                //评级元奖励（2.1）：历史最佳 A 级以上，每次进旧网账本容量 +4。
                //+= 与扩容坞同字段叠加（覆写=毁掉会话内扩容）。TODO MP: per-player 奖励随进场同步
                var record = Player.GetModPlayer<Narrative.Data.StoryPlayer>()
                    .Get<Narrative.Data.Modules.OldNetRecordData>();
                if (record.BestGradeIndex >= OldNetRating.GradeA) {
                    LedgerCapacityBonus += OldNetMetrics.RatingLedgerBonus;
                }
            }
        }

        public override void PostUpdate() {
            //单人代驱动 PvP 协议时钟：不做旧网门控，登出后残余效果也要走完（桥内自门控）
            OldNetHostileHack.DriveClock(Player);

            //进阶入口：主世界 SHPC 赛博领域 L3 接管中按住下潜键深潜（桥内自门控）
            TickL3Dive();

            if (!OldNetWorld.Active || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //烧断弹出倒数：闪红先行，红峰帧交棒 ExitWorld
            if (ejecting) {
                if (ejectDelay > 0 && --ejectDelay == UI.OldNetEjectFlash.PeakFrame) {
                    OldNetWorld.ExitWorld();
                }
                return;
            }

            //──── 热断链泵：紧跟 ejecting 块，烧断/死亡弹出天然短路它 ────
            TickHotExtract();

            //──── 会话统计 ────
            DiveTicks++;
            int depthCols = (int)(Player.Center.X / 16f) - OldNetMetrics.WallCols;
            if (depthCols > MaxDepthCols) {
                MaxDepthCols = depthCols;
            }

            //──── 移动噪音：快速移动作响，静止/慢行不涨 ────
            if (Player.velocity.Length() > OldNetMetrics.NoiseMoveSpeedGate) {
                AddNoise(OldNetMetrics.NoiseMovePerSecond / 60f);
            }

            //──── 加密节点引导推进 ────
            TickChannel();

            //──── 衰减区余震倒数（2.9）────
            TickAftershock();

            //──── 噪音消散与档位 ────
            if (t4DecayImmuneTimer > 0) {
                t4DecayImmuneTimer--;
                quietTimer = 0;
            }
            else if (++quietTimer >= OldNetMetrics.NoiseQuietDelayTicks && Noise > 0f
                //疯域规则（M3）：衰减区内网永不平静，噪音不自然衰减，进来多少带走多少
                && (int)(Player.Center.X / 16f) < OldNetMetrics.FadeLeft) {
                float rate = Noise >= OldNetMetrics.NoiseDecayHighThreshold
                    ? OldNetMetrics.NoiseDecayHighPerSecond
                    : OldNetMetrics.NoiseDecayLowPerSecond;
                float before = Noise;
                Noise = MathF.Max(0f, Noise - rate / 60f);
                //收网棘轮（2.2）：衰减不得自上方跌破地板 70；
                //地板以下的唯一入口是回收官全网静默（SilenceNoise），不回填、任其自由衰减
                if (OldNetICEDirector.DragnetActive && before >= OldNetMetrics.DragnetNoiseFloor) {
                    Noise = MathF.Max(Noise, OldNetMetrics.DragnetNoiseFloor);
                }
            }
            UpdateNoiseTier();

            //──── 距离底噪：墙脚安全区零消耗，越远越贵（标定见 OldNetMetrics）────
            float drain = OldNetMetrics.DrainPerSecondAt((int)(Player.Center.X / 16f));
            if (drain > 0f && !HackTime.InfiniteHackAuthority) {
                RamSystem.TryConsumeOverTime(Player, drain, out _);
            }

            //──── 链路烧断：RAM 耗尽即弹出（ICE 咬合/锁定也走这条，不限深水区）────
            RAMPlayer ram = Player.GetModPlayer<RAMPlayer>();
            if (ram.ProfileInitialized && ram.CurrentRam <= 0.001f) {
                ForceEject(OldNetTexts.OldNetEjectRam.Value);
            }
        }

        //════════ L3 领域下潜（进阶入口）════════
        //赛博领域 L3 = 世界级接管，领域本身就是通往旧网的口子：
        //接管中按住下潜键蓄力 2 秒，经由领域越墙。单人门禁与 /oldnet 同口径
        private void TickL3Dive() {
            if (OldNetWorld.Active || Player.whoAmI != Main.myPlayer
                || Main.netMode != NetmodeID.SinglePlayer || Main.gameMenu) {
                l3DiveHold = 0;
                return;
            }
            var cyber = LegendWeapon.SHPCLegend.Cyberspaces.Cyberspace.Local;
            if (cyber == null || !cyber.Active
                || cyber.CurrentLayer < LegendWeapon.SHPCLegend.Cyberspaces.Cyberspace.MaxLayerCount) {
                l3DiveHold = 0;
                l3ChargeShown = false;
                return;
            }
            //其他子世界内不启用（子世界间不直跳）
            if (OtherMods.SubWorld.SubWorldRef.AnyActiveSubWorld()) {
                l3DiveHold = 0;
                return;
            }

            if (!Player.controlDown || Player.mount.Active) {
                l3DiveHold = 0;
                l3ChargeShown = false;
                return;
            }

            l3DiveHold++;
            //蓄力起步反馈：一次性文字 + 周期滴答
            if (l3DiveHold == 15 && !l3ChargeShown) {
                l3ChargeShown = true;
                CombatText.NewText(Player.getRect(), new Color(140, 200, 210),
                    OldNetTexts.OldNetDiveCharge.Value);
            }
            if (l3DiveHold % 30 == 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.3f }, Player.Center);
            }
            if (l3DiveHold >= OldNetMetrics.L3DiveHoldTicks) {
                l3DiveHold = 0;
                l3ChargeShown = false;
                SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.7f, Pitch = -0.2f }, Player.Center);
                OldNetWorld.EnterWorld();
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            if (!OldNetWorld.Active) {
                return;
            }
            //死亡即链路烧断：账本立即作废，复活后弹出（战报先于清账快照）
            //保险兑付先于战报快照：LostPending 只计未投保的净损失
            PayoutInsurance();
            UI.OldNetDebriefPanel.CacheReport(this, UI.OldNetExitKind.Death);
            ResetLedger();
            CancelChannel();
            CancelHotExtract();
            ejectOnRespawn = true;
            if (Player.whoAmI == Main.myPlayer) {
                CombatText.NewText(Player.getRect(), Color.OrangeRed, OldNetTexts.OldNetEjectDeath.Value, dramatic: true);
            }
        }

        public override void OnRespawn() {
            if (!OldNetWorld.Active || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //死亡弹出，兼收"烧断倒数期间死亡"的缝（死人不跑 PostUpdate，倒数会冻住）
            if (ejectOnRespawn || (ejecting && ejectDelay > 0)) {
                ejectOnRespawn = false;
                ejectDelay = 0;
                ejecting = true;
                UI.OldNetEjectFlash.Begin();
                OldNetWorld.ExitWorld();
            }
        }
    }
}
