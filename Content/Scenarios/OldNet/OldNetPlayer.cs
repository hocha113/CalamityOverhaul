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

        //════════ 噪音状态（per-player，刻意不落存档；MP 时服务器权威后置）════════

        /// <summary>当前噪音 0..100</summary>
        internal float Noise;
        /// <summary>档位缓存 0..4，带迟滞</summary>
        internal int NoiseTier;
        //无新增噪音的连续帧数
        private int quietTimer;
        //T4 触发后的衰减免疫倒数
        private int t4DecayImmuneTimer;

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

        //════════ 加密节点引导会话（站桩破解，本机语义）════════

        /// <summary>引导中的节点坐标；(-1,-1) = 无</summary>
        internal Point ChannelNode = new(-1, -1);
        /// <summary>引导累计帧</summary>
        internal int ChannelTimer;

        internal bool Channeling => ChannelNode.X >= 0;

        /// <summary>引导进度 0..1（节点绘制读取）</summary>
        internal float ChannelProgress => Channeling
            ? MathHelper.Clamp(ChannelTimer / (float)OldNetMetrics.EncryptChannelTicks, 0f, 1f) : 0f;

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
            LedgerCapacityBonus = 0;
            MaxDepthCols = 0;
            SettledTotal = 0;
            HuntedCount = 0;
            DiveTicks = 0;
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
            if (++ChannelTimer < OldNetMetrics.EncryptChannelTicks) {
                return;
            }

            //引导完成：普通节点同分布 ×3
            ChannelNode = new Point(-1, -1);
            ChannelTimer = 0;
            Tiles.OldNetEncryptedNodeTile.CompleteHarvest(node.X, node.Y, this);
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
            //战报缓存先于清账（HarvestCount/PendingTotal 随 ResetLedger 归零）
            UI.OldNetDebriefPanel.CacheReport(this, UI.OldNetExitKind.RamBurnout);
            ResetLedger();
            CancelChannel();
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
                Noise = MathF.Max(0f, Noise - rate / 60f);
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
            UI.OldNetDebriefPanel.CacheReport(this, UI.OldNetExitKind.Death);
            ResetLedger();
            CancelChannel();
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
