using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.SkeletronPrime
{
    /// <summary>
    /// 过载指令核心：机械骷髅王残酷遗物。
    /// 命中积累离子充能，充满进入离子过载窗口（攻速大增+命中链电弧+背后四臂虚影协同），
    /// 窗口结束短暂过热。数值按平衡框架 §8 T3b 机械层级标定（二次重做后基调）
    /// </summary>
    internal class OverloadCommandCore : BaseBrutalRelic
    {
        //==================== 调参区 ====================
        /// <summary>充能上限</summary>
        internal const float MaxCharge = 100f;
        /// <summary>单次命中充能</summary>
        internal const float ChargePerHit = 4f;
        /// <summary>充能入账最短间隔（帧），防穿透弹一帧灌满</summary>
        internal const int ChargeGainICD = 3;
        /// <summary>连击中断阈值（帧），超过开始衰减</summary>
        internal const int ComboIdleLimit = 60;
        /// <summary>衰减速率（点/帧）</summary>
        internal const float ChargeDecay = 0.35f;
        /// <summary>过载窗口时长（帧）＝5秒</summary>
        internal const int OverloadFrames = 300;
        /// <summary>过热时长（帧）＝3秒</summary>
        internal const int OverheatFrames = 180;
        /// <summary>过载攻速乘子（全 DamageClass，经 UseSpeedMultiplier）</summary>
        internal const float OverloadUseSpeed = 1.30f;
        /// <summary>过热攻速乘子（轻微负面）</summary>
        internal const float OverheatUseSpeed = 0.92f;
        /// <summary>电弧链最短触发间隔（帧）</summary>
        internal const int ArcICD = 7;
        /// <summary>电弧链跳跃搜索半径 px</summary>
        internal const float ArcJumpRange = 380f;
        /// <summary>电弧伤害＝本次命中实伤的比例</summary>
        internal const float ArcDamageMul = 0.4f;
        /// <summary>电弧额外跳跃次数（首段之后）</summary>
        internal const int ArcExtraJumps = 2;

        //==================== 配色（离子青系，与 Prime 特斯拉橙刻意区分） ====================
        /// <summary>离子青主色</summary>
        internal static readonly Color IonCyan = new(72, 214, 255);
        /// <summary>深海青（暗部）</summary>
        internal static readonly Color IonDeep = new(16, 110, 140);
        /// <summary>白热高光</summary>
        internal static readonly Color IonHot = new(196, 250, 255);
        /// <summary>过热余烬橙</summary>
        internal static readonly Color HeatEmber = new(255, 150, 60);

        public override void SetDefaults() {
            base.SetDefaults();
            //同期（机械三王期）掉落物卖价约 2~3 金，取 4~5 倍档
            Item.value = Item.buyPrice(0, 60, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<OverloadCorePlayer>().Equipped = true;
        }
    }

    /// <summary>
    /// 过载指令核心玩家侧：离子充能记账与过载/过热状态机。<br/>
    /// 充能只在 owner 端入账（命中钩子本就跑在 owner，netcode §2.2），
    /// 四臂虚影/电弧均为真实弹幕经原版同步全端可见；
    /// 字符流/漩涡/入场爆发经 <see cref="OverloadStateNet"/> 状态沿转播喂远端镜像
    /// （纯表现镜像：远端本地推演倒计时，服务器不落状态）。<br/>
    /// 攻速走 <see cref="UseSpeedMultiplier"/> 正规通道（近战/远程/魔法/召唤全类覆盖）
    /// </summary>
    internal class OverloadCorePlayer : ModPlayer
    {
        /// <summary>本帧装备生效，物品钩子逐帧点亮</summary>
        internal bool Equipped;
        private bool equippedLast;

        /// <summary>离子充能 0~MaxCharge（远端为 25 一档的量化镜像）</summary>
        internal float IonCharge;
        /// <summary>过载窗口剩余帧，&gt;0 即窗口中</summary>
        internal int OverloadTimer;
        /// <summary>过热剩余帧</summary>
        internal int OverheatTimer;
        /// <summary>距上次有效命中帧数</summary>
        private int comboIdleTimer;
        private int chargeGainICD;
        private int arcICD;
        /// <summary>过载入场演出计时（渲染层消费）</summary>
        internal int BurstFlashTimer;
        /// <summary>指令字符流相位（本地推进，渲染层消费）</summary>
        internal float StreamPhase;
        //25/50/75 里程碑音效闩
        private int milestoneLatch;
        //上次广播的充能档位（owner 端状态沿去重）
        private int lastSentTier;

        internal bool OverloadActive => OverloadTimer > 0;
        internal bool Overheated => OverheatTimer > 0;
        internal float ChargeRatio => IonCharge / OverloadCommandCore.MaxCharge;
        /// <summary>近 20 帧内有过命中（owner 端才有值；持械弹幕型武器的攻击热度信号）</summary>
        internal bool RecentComboHit => comboIdleTimer < 20;

        public override void ResetEffects() => Equipped = false;

        //==================== 攻速正规通道 ====================

        public override float UseSpeedMultiplier(Item item) {
            if (OverloadTimer > 0) {
                return OverloadCommandCore.OverloadUseSpeed;
            }
            if (OverheatTimer > 0) {
                return OverloadCommandCore.OverheatUseSpeed;
            }
            return 1f;
        }

        //==================== 命中入口（仅 owner 端触发，netcode §2.2） ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => HandleHit(target, damageDone);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            //遗物自身的弹幕不回喂：臂击/离子弹/电弧再触发＝永动
            if (proj.type == ModContent.ProjectileType<OverloadArmProj>()
                || proj.type == ModContent.ProjectileType<OverloadIonBolt>()
                || proj.type == ModContent.ProjectileType<OverloadArcProj>()) {
                return;
            }
            HandleHit(target, damageDone);
        }

        private void HandleHit(NPC target, int damageDone) {
            if (!Equipped || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (target == null || !target.active || !target.CanBeChasedBy()) {
                return;
            }

            comboIdleTimer = 0;

            //过载窗口：命中链电弧
            if (OverloadTimer > 0) {
                TryChainArc(target, damageDone);
                return;
            }
            //过热：不入账
            if (OverheatTimer > 0) {
                return;
            }

            if (chargeGainICD > 0) {
                return;
            }
            chargeGainICD = OverloadCommandCore.ChargeGainICD;
            IonCharge += OverloadCommandCore.ChargePerHit;
            PlayMilestone();

            if (IonCharge >= OverloadCommandCore.MaxCharge) {
                EnterOverload();
            }
            else {
                SyncChargeTier();
            }
        }

        /// <summary>电弧链首段：从被击目标跳向最近敌人（无近邻则不出弧，单敌时四臂承伤）</summary>
        private void TryChainArc(NPC target, int damageDone) {
            if (arcICD > 0) {
                return;
            }
            NPC jump = target.Center.FindClosestNPC(OverloadCommandCore.ArcJumpRange,
                onHitNPCs: new[] { target });
            if (jump == null) {
                return;
            }
            arcICD = OverloadCommandCore.ArcICD;
            int damage = Math.Max((int)(damageDone * OverloadCommandCore.ArcDamageMul), 30);
            Projectile.NewProjectile(Player.GetSource_Misc("OverloadCommandCore"),
                target.Center, Vector2.Zero, ModContent.ProjectileType<OverloadArcProj>(),
                damage, 0f, Player.whoAmI, jump.whoAmI, target.whoAmI, OverloadCommandCore.ArcExtraJumps);
        }

        //==================== 状态切换 ====================

        /// <summary>进入离子过载：天幕电光+爆发环+四臂虚影展开（只会在 owner 端到达）</summary>
        private void EnterOverload() {
            IonCharge = 0f;
            milestoneLatch = 0;
            lastSentTier = 0;
            OverloadTimer = OverloadCommandCore.OverloadFrames;
            BurstFlashTimer = 18;

            PlayOverloadEntranceFx();
            OverloadStateNet.SendState(Player.whoAmI, OverloadStateNet.StateOverload);

            //四臂虚影：真实弹幕，臂位随生成包出发（ai0=臂型，netcode §2.7 安全）
            if (Player.whoAmI == Main.myPlayer) {
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(Player.GetSource_Misc("OverloadCommandCore"),
                        Player.Center, Vector2.Zero, ModContent.ProjectileType<OverloadArmProj>(),
                        0, 2f, Player.whoAmI, i);
                }
            }
        }

        /// <summary>过载入场演出：天幕电光+三重音效+离子新星（本端播放，owner 与远端镜像共用一份）</summary>
        internal void PlayOverloadEntranceFx() {
            //机械 Boss 战中天幕电光一闪（MachineEffect 自带 isServer/IsActive 门）
            MachineEffect.TriggerSkyFlash(Player.Center, 1f);

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.75f, Pitch = -0.1f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.1f }, Player.Center);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.6f }, Player.Center);
            SpawnOverloadNova();
            if (Player.whoAmI == Main.myPlayer) {
                Main.LocalPlayer?.CWR()?.GetScreenShake(4f);
            }
        }

        /// <summary>窗口结束进入过热：蒸汽嘶鸣，轻微负面（owner 与远端镜像各自倒数到点触发）</summary>
        private void EnterOverheat() {
            OverheatTimer = OverloadCommandCore.OverheatFrames;
            OverloadStateNet.SendState(Player.whoAmI, OverloadStateNet.StateOverheat);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.8f, Pitch = 0.1f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = -0.5f }, Player.Center);
            for (int i = 0; i < 9; i++) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(16f, 22f);
                PRTLoader.NewParticle<PRT_FluidSteam>(pos,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.6f, -0.6f)),
                    new Color(225, 232, 238) * 0.55f, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(30, 55), 0.045f);
            }
        }

        //==================== 状态转播（纯表现，OverloadStateNet） ====================

        /// <summary>owner 端充能档位沿变更广播（每跨 25 一档，升降都发）</summary>
        private void SyncChargeTier() {
            int tier = Math.Clamp((int)(IonCharge / 25f), 0, 4);
            if (tier == lastSentTier) {
                return;
            }
            lastSentTier = tier;
            OverloadStateNet.SendState(Player.whoAmI, (byte)tier);
        }

        /// <summary>远端镜像入口：按状态码驱动本地表现状态机（无伤害权威，丢包只损失演出）</summary>
        internal void ApplyRemoteState(byte state) {
            switch (state) {
                case OverloadStateNet.StateOverload:
                    IonCharge = 0f;
                    OverloadTimer = OverloadCommandCore.OverloadFrames;
                    OverheatTimer = 0;
                    BurstFlashTimer = 18;
                    PlayOverloadEntranceFx();
                    break;
                case OverloadStateNet.StateOverheat:
                    OverloadTimer = 0;
                    //本地倒数常已先一步入过热（演出已播），此时只重锚计时防漂移
                    if (OverheatTimer <= 0) {
                        EnterOverheat();
                    }
                    else {
                        OverheatTimer = OverloadCommandCore.OverheatFrames;
                    }
                    break;
                default:
                    //充能档位镜像（×25）：远端不走衰减，等下一档包重锚
                    IonCharge = Math.Clamp((int)state, 0, 4) * 25f;
                    break;
            }
        }

        //==================== 逐帧状态机 ====================

        public override void PostUpdate() {
            //卸下清场
            if (!Equipped) {
                if (equippedLast) {
                    IonCharge = 0f;
                    OverloadTimer = 0;
                    OverheatTimer = 0;
                    milestoneLatch = 0;
                    lastSentTier = 0;
                }
                equippedLast = false;
                TickSharedTimers();
                StampRenderIfVisible();
                return;
            }
            equippedLast = true;

            TickSharedTimers();

            if (OverloadTimer > 0) {
                OverloadTimer--;
                UpdateOverloadAmbience();
                if (OverloadTimer == 0) {
                    EnterOverheat();
                }
            }
            else if (OverheatTimer > 0) {
                OverheatTimer--;
                UpdateOverheatAmbience();
            }
            else if (Player.whoAmI == Main.myPlayer
                && IonCharge > 0f && comboIdleTimer > OverloadCommandCore.ComboIdleLimit) {
                //连击中断缓慢衰减（仅 owner：远端镜像持档等下一包重锚）
                IonCharge = Math.Max(0f, IonCharge - OverloadCommandCore.ChargeDecay);
                RollbackMilestone();
                SyncChargeTier();
            }

            //字符流速率随充能/过载加快（本地表现，渲染层消费）
            float rate = OverloadTimer > 0 ? 0.11f : 0.012f + ChargeRatio * 0.05f;
            StreamPhase += rate;

            StampRenderIfVisible();
        }

        /// <summary>渲染层帧戳：本玩家有可见状态即放行 RenderHandle 的全表扫描</summary>
        private void StampRenderIfVisible() {
            if (Main.dedServ) {
                return;
            }
            if (IonCharge > 0.5f || OverloadTimer > 0 || OverheatTimer > 0 || BurstFlashTimer > 0) {
                OverloadCommandRender.RenderStamp.Stamp();
            }
        }

        private void TickSharedTimers() {
            if (chargeGainICD > 0) {
                chargeGainICD--;
            }
            if (arcICD > 0) {
                arcICD--;
            }
            if (BurstFlashTimer > 0) {
                BurstFlashTimer--;
            }
            comboIdleTimer++;
        }

        public override void UpdateDead() {
            //死亡即断电：窗口/过热/充能全清，四臂在自身 AI 里看到 owner 死亡自行熄灭
            IonCharge = 0f;
            OverloadTimer = 0;
            OverheatTimer = 0;
            BurstFlashTimer = 0;
            milestoneLatch = 0;
            lastSentTier = 0;
        }

        //==================== 表现（全部 client-only） ====================

        /// <summary>25/50/75 里程碑升调提示，仅 owner 本机</summary>
        private void PlayMilestone() {
            if (VaultUtils.isServer || Player.whoAmI != Main.myPlayer) {
                return;
            }
            int stage = (int)(IonCharge / OverloadCommandCore.MaxCharge * 4f);
            if (stage > milestoneLatch && stage < 4) {
                milestoneLatch = stage;
                SoundEngine.PlaySound(SoundID.Item93 with {
                    Volume = 0.3f,
                    Pitch = -0.2f + stage * 0.25f,
                    MaxInstances = 2
                }, Player.Center);
            }
        }

        //衰减跌档时回落闩，允许再次升档提示
        private void RollbackMilestone() {
            int stage = (int)(IonCharge / OverloadCommandCore.MaxCharge * 4f);
            if (stage < milestoneLatch) {
                milestoneLatch = stage;
            }
        }

        /// <summary>过载入场爆发：离子新星+火花环</summary>
        private void SpawnOverloadNova() {
            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi * i / 26f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                PRTLoader.NewParticle<PRT_Spark>(Player.Center, vel,
                    Color.Lerp(OverloadCommandCore.IonCyan, OverloadCommandCore.IonHot, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.2f, 2f))?.Configure(false, Main.rand.Next(18, 32), Player);
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f);
                PRTLoader.NewParticle<PRT_Light>(Player.Center, vel,
                    OverloadCommandCore.IonCyan, 0.5f)
                    ?.Configure(Main.rand.Next(18, 30), opacity: 1.2f, squishStrenght: 2.2f);
            }
        }

        /// <summary>过载窗口常态：离子光照+零星电火花，尾声 60 帧火花转橙预警</summary>
        private void UpdateOverloadAmbience() {
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Player.Center, OverloadCommandCore.IonCyan.ToVector3() * 0.55f);

            bool ending = OverloadTimer < 60;
            if (Main.GameUpdateCount % 4 == 0) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(24f, 30f);
                Color c = ending && Main.rand.NextBool()
                    ? OverloadCommandCore.HeatEmber
                    : OverloadCommandCore.IonCyan;
                PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Circular(2f, 2f),
                    c, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(8, 14), Player);
            }
        }

        /// <summary>过热常态：体表蒸汽+橙色火花</summary>
        private void UpdateOverheatAmbience() {
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Player.Center, OverloadCommandCore.HeatEmber.ToVector3() * 0.25f
                * (OverheatTimer / (float)OverloadCommandCore.OverheatFrames));

            if (Main.GameUpdateCount % 5 == 0) {
                Vector2 pos = Player.Center + new Vector2(
                    Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-22f, 6f));
                PRTLoader.NewParticle<PRT_FluidSteam>(pos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.2f, -0.5f)),
                    new Color(218, 226, 232) * 0.4f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(24, 42), 0.04f);
            }
            if (Main.GameUpdateCount % 9 == 0 && Main.rand.NextBool()) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Player.Center + Main.rand.NextVector2Circular(14f, 20f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2.5f, -0.5f)),
                    OverloadCommandCore.HeatEmber, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(true, Main.rand.Next(12, 20), Player);
            }
        }
    }

    /// <summary>
    /// 过载状态演出转播：owner 状态沿变更时发送（充能每跨 25 一档 / 入过载 / 入过热），
    /// 服务器校验 whoAmI 防伪后转播，远端收包驱动字符流与入场爆发一拍。
    /// 纯表现包（镜像 BlackFlashSigilNet 范式）：不携带任何伤害结算字段，
    /// 服务器不落状态，丢包只损失演出无状态污染
    /// </summary>
    internal class OverloadStateNet : CWRNetChannel
    {
        /// <summary>状态码：0~4＝充能档位（×25），5＝入过载，6＝入过热</summary>
        internal const byte StateOverload = 5;
        internal const byte StateOverheat = 6;

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净负载再守卫
            int owner = reader.ReadByte();
            byte state = reader.ReadByte();

            if (Main.netMode == NetmodeID.Server) {
                if (owner != whoAmI) {
                    CWRMod.Instance.Logger.Info($"OverloadCommandCore state spoof dropped: claim={owner} actual={whoAmI}");
                    return;
                }
                ModPacket relay = CWRNetWork.GetPacket<OverloadStateNet>();
                relay.Write((byte)owner);
                relay.Write(state);
                relay.Send(ignoreClient: whoAmI);
                return;
            }
            if (owner < 0 || owner >= Main.maxPlayers || owner == Main.myPlayer) {
                return;
            }
            Player player = Main.player[owner];
            if (player?.active != true || !player.TryGetModPlayer(out OverloadCorePlayer mp)) {
                return;
            }
            mp.ApplyRemoteState(state);
        }

        /// <summary>owner 端状态沿发送（单人无包，本地演出已在触发处播放）</summary>
        internal static void SendState(int owner, byte state) {
            if (Main.netMode != NetmodeID.MultiplayerClient || owner != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<OverloadStateNet>();
            packet.Write((byte)owner);
            packet.Write(state);
            packet.Send();
        }
    }
}
