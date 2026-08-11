using CalamityOverhaul.Content.HackTimes.CircuitNodes;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>喂弹判定结果，炮台按它决定这一发打不打、覆写收不收</summary>
    internal enum MunitionFeedVerdict : byte
    {
        /// <summary>可以开火（单人已扣弹 / 服务端已发扣弹意图 / 无尽弹药免扣）</summary>
        Fire,
        /// <summary>在途扣弹已满，停一拍等镜像回同步，覆写保持</summary>
        Hold,
        /// <summary>弹尽或喂弹者失效，覆写该收了</summary>
        Exhausted,
    }

    /// <summary>
    /// 弹药置换：炮台改吃施法者背包里的弹药，发射物换成该弹药的弹，
    /// 伤害按施法者的远程面板算，隐含翻转 IFF；弹尽或效果结束即回落原生弹。<br/>
    /// 每一发的扣弹结算落在拥有者本机（背包归客户端所有，服务端写不动——
    /// tml-netcode-pitfalls §6.2）：单人直接扣；联机由服务端校验镜像背包后发
    /// <see cref="CWRMessageType.MunitionSwapConsume"/> 意图包，喂弹者本机结算，
    /// 背包差分每帧自动回同步，无需回执。在途扣弹上限防镜像滞后被连发放大
    /// </summary>
    internal class MunitionSwap : QuickHackDef
    {
        //持续二十秒
        private const int DurationFrames = 1200;
        //炮台导轨的基础加成，叠在弹药自身伤害上
        private const int RailBaseDamage = 12;

        private static readonly Color FeedColor = new(255, 210, 90);

        public override void SetDefaults() {
            UploadTime = 140;
            RamCost = 5;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.Turret;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => DurationFrames;

        public override bool CanApplyTo(IHackTarget target) {
            //停摆的炮供不进弹；不支持弹药覆写的炮台实作直接拒绝
            return base.CanApplyTo(target)
                && target is IHackableTurret { IsCircuitDisabled: false }
                && target is IMunitionFeedTurret;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            return CanApplyTo(target) && TryFindFeedAmmo(caster, out _);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IMunitionFeedTurret feed
                || target is not IHackableTurret turret) {
                return false;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!TryFindFeedAmmo(caster, out Item ammo)) {
                    return false;
                }
                int damage = ComputeShotDamage(caster, ammo);
                feed.ApplyMunitionOverride(ammo.type, ammo.shoot, damage, caster, DurationFrames);
            }
            if (Main.netMode != NetmodeID.Server) {
                EmitFeedBurst(turret.WorldCenter);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is IHackableTurret turret) {
                EmitFeedBurst(turret.WorldCenter);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not IMunitionFeedTurret feed) {
                return true;
            }
            //弹尽时炮台已自行回落，效果跟着提前收尾，面板别再挂着假状态
            if (Main.netMode != NetmodeID.MultiplayerClient && !feed.MunitionOverrideActive) {
                return false;
            }
            if (Main.netMode != NetmodeID.Server && target is IHackableTurret turret
                && elapsed % 14 == 0) {
                //供弹带的细碎火花，读作弹链在走
                Vector2 offset = Main.rand.NextVector2Circular(16f, 12f);
                PRTLoader.NewParticle<PRT_Spark>(turret.WorldCenter + offset,
                    new Vector2(0f, Main.rand.NextFloat(-1.2f, -0.4f)), FeedColor, 0.45f)
                    ?.Configure(false, 16);
            }
            return true;
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                && target is IMunitionFeedTurret feed) {
                feed.ClearMunitionOverride();
            }
        }

        /// <summary>
        /// 施法者背包里第一叠可用弹药；弹药栏优先，主背包兜底，与原版取弹次序一致
        /// </summary>
        internal static bool TryFindFeedAmmo(Player caster, out Item ammo) {
            ammo = null;
            if (caster?.active != true) {
                return false;
            }
            //54..57 是弹药栏
            for (int i = 54; i <= 57; i++) {
                if (IsUsableAmmo(caster.inventory[i])) {
                    ammo = caster.inventory[i];
                    return true;
                }
            }
            for (int i = 0; i < 54; i++) {
                if (IsUsableAmmo(caster.inventory[i])) {
                    ammo = caster.inventory[i];
                    return true;
                }
            }
            return false;
        }

        private static bool IsUsableAmmo(Item item) {
            return item?.IsAir == false && item.ammo > 0 && item.stack > 0 && item.shoot > 0;
        }

        private static int ComputeShotDamage(Player caster, Item ammo) {
            float baseDamage = ammo.damage + RailBaseDamage;
            return Math.Max(1, (int)caster.GetTotalDamage(DamageClass.Ranged).ApplyTo(baseDamage));
        }

        /// <summary>
        /// 本机扣弹结算。背包归客户端所有，只在拥有者本机扣，
        /// 无尽类弹药（不可消耗）照打不扣。返回 false 表示这发供不上、覆写该收了。<br/>
        /// 调用方：单人的炮台开火前，以及联机喂弹者收到
        /// <see cref="CWRMessageType.MunitionSwapConsume"/> 意图包时；
        /// 服务端的判定与发包走 <see cref="RequestFeed"/>，不进这里
        /// </summary>
        internal static bool ConsumeFeederAmmo(Player feeder, int ammoType) {
            if (feeder?.active != true || feeder.dead || ammoType <= 0) {
                return false;
            }
            //背包不是本机的就不许代扣（服务端写镜像背包是坏账，见 §6.2）
            if (Main.netMode != NetmodeID.SinglePlayer && feeder.whoAmI != Main.myPlayer) {
                return false;
            }
            for (int i = 54; i <= 57; i++) {
                if (TryConsumeSlot(feeder, i, ammoType)) {
                    return true;
                }
            }
            for (int i = 0; i < 54; i++) {
                if (TryConsumeSlot(feeder, i, ammoType)) {
                    return true;
                }
            }
            return false;
        }

        #region 联机扣弹（服务端判定 → 喂弹者本机结算）

        //每台炮台未结算的在途扣弹上限，防镜像滞后被连发放大（§6.2 的 RAM 芯片前科）
        private const int MaxPendingConsumes = 3;
        //在途账多久没有观察到镜像回落就视作包已丢/被无声吞掉，清零放行重试
        private const int PendingStallFrames = 300;

        private sealed class PendingFeed
        {
            public int Count;
            public int LastMirrorStack = -1;
            public ulong LastProgressFrame;
        }

        //炮台身份 → 在途扣弹账。只在服务端写；世界切换由 CircuitNodeSpawner 清
        private static readonly Dictionary<CircuitActorKey, PendingFeed> pendingFeeds = [];

        public override void Unload() {
            base.Unload();
            pendingFeeds.Clear();
        }

        /// <summary>切世界清账（CircuitNodeSpawner.OnWorldUnload 调）</summary>
        internal static void ClearPendingFeeds() => pendingFeeds.Clear();

        /// <summary>覆写重挂或收掉时丢弃该炮台的在途账，别把旧账带进下一次覆写</summary>
        internal static void ForgetPending(CircuitActorKey turretKey)
            => pendingFeeds.Remove(turretKey);

        /// <summary>
        /// 炮台开火前的统一喂弹判定。单人直接本机扣；
        /// 服务端校验镜像背包后向喂弹者发扣弹意图，本发照常发射
        /// </summary>
        internal static MunitionFeedVerdict RequestFeed(CircuitActorKey turretKey,
            Player feeder, int ammoType) {
            if (feeder?.active != true || feeder.dead || ammoType <= 0) {
                return MunitionFeedVerdict.Exhausted;
            }
            if (Main.netMode != NetmodeID.Server) {
                return ConsumeFeederAmmo(feeder, ammoType)
                    ? MunitionFeedVerdict.Fire
                    : MunitionFeedVerdict.Exhausted;
            }

            //服务端视角的镜像背包：只做判定，绝不写它
            int mirrorStack = CountInventoryAmmo(feeder, ammoType, out bool consumable);
            if (mirrorStack <= 0) {
                pendingFeeds.Remove(turretKey);
                return MunitionFeedVerdict.Exhausted;
            }
            //无尽类弹药不消耗，不需要结算，也就没有在途账
            if (!consumable) {
                return MunitionFeedVerdict.Fire;
            }

            if (!pendingFeeds.TryGetValue(turretKey, out PendingFeed pending)) {
                pending = new PendingFeed();
                pendingFeeds[turretKey] = pending;
            }
            //镜像回落即视作在途已结算（客户端扣完，背包差分回同步）
            if (pending.LastMirrorStack >= 0 && mirrorStack < pending.LastMirrorStack) {
                pending.Count = Math.Max(0,
                    pending.Count - (pending.LastMirrorStack - mirrorStack));
                pending.LastProgressFrame = Main.GameUpdateCount;
            }
            pending.LastMirrorStack = mirrorStack;

            if (pending.Count >= MaxPendingConsumes) {
                //长期不结算多半是意图包丢了（客户端只可能少扣不可能多扣），清零重试
                if (Main.GameUpdateCount - pending.LastProgressFrame > PendingStallFrames) {
                    pending.Count = 0;
                    pending.LastProgressFrame = Main.GameUpdateCount;
                }
                else {
                    return MunitionFeedVerdict.Hold;
                }
            }

            SendConsumeIntent(turretKey, ammoType, feeder.whoAmI);
            pending.Count++;
            if (pending.Count == 1) {
                pending.LastProgressFrame = Main.GameUpdateCount;
            }
            return MunitionFeedVerdict.Fire;
        }

        //镜像背包该弹种总量；consumable 取第一叠的消耗性（同种弹药不会一半无尽一半普通）
        private static int CountInventoryAmmo(Player feeder, int ammoType,
            out bool consumable) {
            consumable = true;
            int total = 0;
            Item[] inventory = feeder.inventory;
            for (int i = 0; i < 58 && i < inventory.Length; i++) {
                Item item = inventory[i];
                if (item?.IsAir != false || item.type != ammoType || item.stack <= 0) {
                    continue;
                }
                if (total == 0) {
                    consumable = item.consumable;
                }
                total += item.stack;
            }
            return total;
        }

        private static void SendConsumeIntent(CircuitActorKey turretKey, int ammoType,
            int feederIndex) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.MunitionSwapConsume);
            turretKey.Write(packet);
            packet.Write(ammoType);
            packet.Send(feederIndex);
        }

        /// <summary>喂弹者客户端收扣弹意图：本机结算，背包差分每帧自动回同步，无需回执</summary>
        internal static void HandleConsume(BinaryReader reader, int whoAmI) {
            //定长负载先读干净（10 字节 key + 4 字节弹种），再做守卫
            CircuitActorKey.TryRead(reader, out _);
            int ammoType = reader.ReadInt32();
            if (Main.netMode != NetmodeID.MultiplayerClient
                || ammoType <= 0 || ammoType >= ItemLoader.ItemCount) {
                return;
            }
            ConsumeFeederAmmo(Main.LocalPlayer, ammoType);
        }

        #endregion

        private static bool TryConsumeSlot(Player feeder, int slot, int ammoType) {
            Item item = feeder.inventory[slot];
            if (item?.IsAir != false || item.type != ammoType || item.stack <= 0) {
                return false;
            }
            if (!item.consumable) {
                return true;
            }
            item.stack--;
            if (item.stack <= 0) {
                item.TurnToAir();
            }
            return true;
        }

        private static void EmitFeedBurst(Vector2 center) {
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                Color c = Color.Lerp(FeedColor, Color.White, Main.rand.NextFloat(0.4f));
                PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.0f)?.Configure(false, 24);
            }
            //向上竖直的一列，读作供弹带接入
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(center + new Vector2(0f, 18f - i * 7f),
                    new Vector2(0f, -2.2f), FeedColor, 0.7f)?.Configure(false, 22);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.3f, Volume = 0.7f }, center);
            }
        }
    }
}
