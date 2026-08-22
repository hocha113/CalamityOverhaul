using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 鬼伞·沉溺权威核心。封印=直接移除（无掉落不算击杀，CyberBanish 同款），
    /// 服务器只验资格/距离/频率，不验领域，服务器没有领域状态是既定契约；
    /// 领域就绪由客户端预检。定身走轻量方案：权威端逐帧钉位+定期 netUpdate，
    /// 40 帧短持有后在抓握节拍移除真身，此后演出全靠客户端鬼影。
    /// </summary>
    internal static class KikasaDrown
    {
        /// <summary>权威时间轴上的移除节拍（Apply 发出帧起算）</summary>
        public const int GrabBeatFrames = 40;

        /// <summary>完成后冷却</summary>
        public const int CooldownFrames = 90;

        /// <summary>技术远界：覆盖最大缩放下整屏的光标可及范围，
        /// 服务器用同一常量复检，防伪造包跨地图沉任意 NPC</summary>
        public const float MaxRange = 4000f;

        /// <summary>湖上抓握高度上限：与 FX 层臂展预算对齐（段长动态定标 26..240，
        /// 6 节解算臂展 ≈1411px、抓点筛选 1350px），此界之内手臂真伸得到，
        /// 不会出现卷指计时驱动下指尖离目标半屏远照样"攥中"的隔空贴手</summary>
        public const float MaxGrabHeight = 1200f;

        /// <summary>湖面之下的容许深度，再深就是往地里抓</summary>
        public const float MaxGrabDepth = 600f;

        internal struct DrownTarget
        {
            public NetworkNPCIdentity Identity;
            /// <summary>受理帧捕获的 npc.position，逐帧钉回</summary>
            public Vector2 Pin;
        }

        internal sealed class DrownActivation
        {
            public int OwnerWho;
            public int DrownId;
            public float Seed;
            public int Timer;
            /// <summary>[0]=主段，其余为组员（蠕虫等）</summary>
            public readonly List<DrownTarget> Targets = [];
        }

        //权威记录（服务器/单机）；客户端不持有
        private static readonly List<DrownActivation> activations = [];
        private static readonly int[] cooldowns = new int[Main.maxPlayers];
        private static readonly List<NPC> groupBuffer = [];
        private static int nextDrownId;

        //本机所有者的乐观锁：请求在途/演出进行/完成冷却，服务器另有真限频
        private static uint localLockUntil;

        //==================== 资格 ====================

        /// <summary>
        /// 共享资格谓词：客户端预检与服务器复检同一份（一处真相）。
        /// 2026-08 暂时全放开：任何活跃 NPC（含 boss/城镇/免伤）都能沉，
        /// 只留技术性守卫，正被放逐或已在沉溺中的目标不能被第二只手抓
        /// </summary>
        public static bool IsEligibleTarget(NPC npc)
            => npc?.active == true && npc.lifeMax > 0
            && !CyberBanish.IsBanishing(npc.whoAmI)
            && !IsDrowningAuthority(npc.whoAmI);

        /// <summary>权威端是否已有针对该槽位的沉溺（客户端上这份表为空，恒 false）</summary>
        internal static bool IsDrowningAuthority(int npcIndex) {
            for (int i = 0; i < activations.Count; i++) {
                List<DrownTarget> targets = activations[i].Targets;
                for (int j = 0; j < targets.Count; j++) {
                    if (targets[j].Identity.Index == npcIndex) {
                        return true;
                    }
                }
            }
            return false;
        }

        //==================== 客户端入口 ====================

        /// <summary>
        /// 光标下有生物时受理沉溺，返回是否消费了这次按键
        /// （冷却/湖未就绪的拒绝也算消费，玩家的意图明确是生物，不该误沉手中物）
        /// </summary>
        internal static bool TryDrownAtCursor(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return false;
            }
            NPC hover = FindCursorTarget();
            if (hover == null) {
                return false;
            }

            //意图已明确指向生物，以下拒绝均消费按键
            if (!player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                Refuse(player);
                return true;
            }
            if (Main.GameUpdateCount < localLockUntil
                || KikasaDrownFX.HasActiveShowFor(player.whoAmI)) {
                Refuse(player);
                return true;
            }
            if (!IsEligibleTarget(hover)) {
                //已被放逐/别只手攥着：湖收不了
                Refuse(player);
                return true;
            }
            float lakeY = LakeYOf(player);
            if (Vector2.Distance(hover.Center, player.Center) > MaxRange
                || hover.Center.Y < lakeY - MaxGrabHeight
                || hover.Center.Y > lakeY + MaxGrabDepth) {
                Refuse(player);
                return true;
            }

            //客户端可能铸不出 generation（§4.3），index+type 随行，服务器再盖自己的章
            NetworkNPCIdentity.TryCapture(hover, out NetworkNPCIdentity identity);

            //可丢弃的预兆预测：湖面先应两圈涟漪，一声低水滴；圈随目标体型放大
            float omen = MathHelper.Clamp(
                MathF.Sqrt(hover.width * (float)hover.height) / 30f, 0.9f, 2.4f);
            KikasaDomains.KikasaDomainDeco.RippleAt(
                new Vector2(hover.Center.X, lakeY), 0.5f * omen);
            KikasaDomains.KikasaDomainDeco.RippleAt(
                new Vector2(hover.Center.X + 24f * omen, lakeY), 0.35f * omen);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.55f, MaxInstances = 2 }, hover.Center);

            //请求在途短锁，防连点；真限频在权威端
            localLockUntil = Main.GameUpdateCount + 60;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                KikasaDrownNet.SendRequest(hover.whoAmI, hover.type, identity.Generation);
            }
            else {
                StartAuthoritative(player, hover);
            }
            return true;
        }

        private static NPC FindCursorTarget() {
            Vector2 mouse = Main.MouseWorld;
            NPC best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                //资格暂时全放开，选中只留技术性过滤
                if (npc?.active != true || npc.lifeMax <= 0) {
                    continue;
                }
                Rectangle hitbox = npc.Hitbox;
                hitbox.Inflate(12, 12);
                if (!hitbox.Contains(mouse.ToPoint())) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(npc.Center, mouse);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = npc;
                }
            }
            return best;
        }

        //==================== 悬停预兆 ====================

        //悬停涟漪节拍与目标记忆：换目标时首圈快些应
        private static int hoverOmenTimer;
        private static int hoverOmenNpc = -1;

        /// <summary>
        /// 可沉暗示：湖就绪时光标悬着够得到的生物，它脚下的湖面先泛起极淡的涟漪
        /// 把按键后的涟漪预兆提前到悬停。纯本机演出，每帧由
        /// <see cref="KikasaDrownSystem.PostUpdateEverything"/> 泵动
        /// </summary>
        internal static void UpdateHoverOmen() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead || Main.gameMenu) {
                hoverOmenNpc = -1;
                return;
            }
            //冷却/在途中悬停也别应声，按下去本就是拒绝
            if (Main.GameUpdateCount < localLockUntil
                || KikasaDrownFX.HasActiveShowFor(player.whoAmI)
                || !player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                hoverOmenNpc = -1;
                return;
            }
            NPC hover = FindCursorTarget();
            float lakeY = LakeYOf(player);
            bool reachable = hover != null && IsEligibleTarget(hover)
                && Vector2.Distance(hover.Center, player.Center) <= MaxRange
                && hover.Center.Y >= lakeY - MaxGrabHeight
                && hover.Center.Y <= lakeY + MaxGrabDepth;
            if (!reachable) {
                hoverOmenNpc = -1;
                return;
            }

            if (hover.whoAmI != hoverOmenNpc) {
                hoverOmenNpc = hover.whoAmI;
                //换目标：首圈略等半拍，避免扫过一排怪时满屏起涟漪
                hoverOmenTimer = 8;
            }
            if (--hoverOmenTimer > 0) {
                return;
            }
            hoverOmenTimer = 22;
            float omen = MathHelper.Clamp(
                MathF.Sqrt(hover.width * (float)hover.height) / 30f, 0.9f, 2.4f);
            //封顶在 RippleAt 的涌浪阈值(0.3)之下：悬停预兆只许极淡涟漪，
            //大型目标也不该每拍掀一次整条水线
            KikasaDomains.KikasaDomainDeco.RippleAt(
                new Vector2(hover.Center.X + Main.rand.NextFloat(-8f, 8f) * omen, lakeY),
                MathF.Min(0.22f * omen, 0.28f));
        }

        private static float LakeYOf(Player player)
            => player.GetModPlayer<KikasaDomains.KikasaDomainPlayer>().LakeWorldY;

        private static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, player.Center);
        }

        /// <summary>本机演出谢幕（完成或取消）后的冷却，由 FX 层回调</summary>
        internal static void OnLocalShowEnded(int ownerWho) {
            if (ownerWho == Main.myPlayer) {
                localLockUntil = Main.GameUpdateCount + CooldownFrames;
            }
        }

        /// <summary>本机沉溺手的锁剩余 0~1（1=刚上锁），HUD 冷却弧消费；无锁=0</summary>
        internal static float LocalCooldown01 {
            get {
                if (Main.GameUpdateCount >= localLockUntil) {
                    return 0f;
                }
                return MathHelper.Clamp(
                    (localLockUntil - Main.GameUpdateCount) / (float)CooldownFrames, 0f, 1f);
            }
        }

        //==================== 权威路径 ====================

        /// <summary>服务器收到请求：先解析目标（无 generation 时按 index+type 回退），再走共同校验</summary>
        internal static void HandleRequest(int ownerWho, int npcIndex, int npcType, ulong generation) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            Player owner = ownerWho >= 0 && ownerWho < Main.maxPlayers ? Main.player[ownerWho] : null;
            if (owner?.active != true) {
                Reject(ownerWho, "owner-invalid");
                return;
            }

            NPC target = null;
            if (generation != 0) {
                NetworkNPCIdentity requested = new(npcIndex, npcType, generation);
                requested.TryResolve(out target);
            }
            if (target == null && npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                //客户端铸不出 generation 的回退：index+type 松解析，随后全套复检兜底
                NPC candidate = Main.npc[npcIndex];
                if (candidate?.active == true && candidate.type == npcType) {
                    target = candidate;
                }
            }
            if (target == null) {
                Reject(ownerWho, "target-missing");
                return;
            }
            StartAuthoritative(owner, target);
        }

        /// <summary>共同权威路径：单机直通与服务器请求都走这里</summary>
        internal static bool StartAuthoritative(Player owner, NPC target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (owner?.active != true || owner.dead) {
                Reject(owner?.whoAmI ?? -1, "owner-dead");
                return false;
            }
            int ownerWho = owner.whoAmI;
            if (cooldowns[ownerWho] > 0 || HasActivationFor(ownerWho)) {
                Reject(ownerWho, "cooldown-or-busy");
                return false;
            }
            if (!IsEligibleTarget(target)) {
                Reject(ownerWho, "ineligible");
                return false;
            }
            if (Vector2.Distance(target.Center, owner.Center) > MaxRange) {
                Reject(ownerWho, "out-of-range");
                return false;
            }
            //权威端盖自己的章，后续一律用服务器的 generation
            if (!NetworkNPCIdentity.TryCapture(target, out NetworkNPCIdentity primary)) {
                Reject(ownerWho, "identity-mint");
                return false;
            }

            DrownActivation activation = new() {
                OwnerWho = ownerWho,
                DrownId = ++nextDrownId,
                Seed = Main.rand.NextFloat(1000f),
            };
            activation.Targets.Add(new DrownTarget { Identity = primary, Pin = target.position });

            //蠕虫等整组一起封印；演出全套只给主段，组员在拖入拍一起下沉
            NpcGroupHelper.CollectGroup(target, groupBuffer);
            for (int i = 0; i < groupBuffer.Count; i++) {
                NPC member = groupBuffer[i];
                if (member == null || member == target || !IsEligibleTarget(member)) {
                    continue;
                }
                if (NetworkNPCIdentity.TryCapture(member, out NetworkNPCIdentity id)) {
                    activation.Targets.Add(new DrownTarget { Identity = id, Pin = member.position });
                }
            }
            groupBuffer.Clear();

            activations.Add(activation);

            if (Main.netMode == NetmodeID.Server) {
                KikasaDrownNet.SendApply(activation);
            }
            else {
                //单机：权威与演出同机同帧
                StartShowFrom(activation);
            }
            return true;
        }

        private static void StartShowFrom(DrownActivation activation) {
            List<NetworkNPCIdentity> members = [];
            for (int i = 1; i < activation.Targets.Count; i++) {
                members.Add(activation.Targets[i].Identity);
            }
            KikasaDrownFX.StartShow(activation.OwnerWho, activation.DrownId,
                activation.Seed, activation.Targets[0].Identity, members);
        }

        private static bool HasActivationFor(int ownerWho) {
            for (int i = 0; i < activations.Count; i++) {
                if (activations[i].OwnerWho == ownerWho) {
                    return true;
                }
            }
            return false;
        }

        //被拒的请求写日志：静默拒绝没法诊断（§2.8）
        private static void Reject(int ownerWho, string clause) {
            CWRMod.Instance?.Logger?.Info($"[KikasaDrown] reject owner={ownerWho} clause={clause}");
        }

        //==================== 权威推进 ====================

        /// <summary>由 KikasaDrownSystem 逐帧驱动；多人客户端无事可做</summary>
        internal static void UpdateAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            for (int i = 0; i < cooldowns.Length; i++) {
                if (cooldowns[i] > 0) {
                    cooldowns[i]--;
                }
            }

            for (int i = activations.Count - 1; i >= 0; i--) {
                DrownActivation activation = activations[i];
                Player owner = Main.player[activation.OwnerWho];
                if (owner?.active != true || owner.dead) {
                    CancelActivation(activation, i, "owner-lost");
                    continue;
                }
                //主段没了（被打死/消失）整场取消；组员没了悄悄除名
                if (!activation.Targets[0].Identity.TryResolve(out _)) {
                    CancelActivation(activation, i, "primary-lost");
                    continue;
                }
                for (int j = activation.Targets.Count - 1; j >= 1; j--) {
                    if (!activation.Targets[j].Identity.TryResolve(out _)) {
                        activation.Targets.RemoveAt(j);
                    }
                }

                activation.Timer++;
                PinTargets(activation);

                if (activation.Timer >= GrabBeatFrames) {
                    CompleteActivation(activation);
                    activations.RemoveAt(i);
                }
            }
        }

        //权威钉身：位置钉回受理帧，速度钳零；周期 netUpdate 让客户端贴紧

        private static void PinTargets(DrownActivation activation) {
            bool push = activation.Timer % 8 == 0;
            for (int j = 0; j < activation.Targets.Count; j++) {
                DrownTarget target = activation.Targets[j];
                if (!target.Identity.TryResolve(out NPC npc)) {
                    continue;
                }
                npc.position = target.Pin;
                npc.velocity = Vector2.Zero;
                if (push && Main.netMode == NetmodeID.Server) {
                    npc.netUpdate = true;
                }
            }
        }

        //抓握节拍：真身移除，此后各端由鬼影接管

        private static void CompleteActivation(DrownActivation activation) {
            for (int j = 0; j < activation.Targets.Count; j++) {
                if (!activation.Targets[j].Identity.TryResolve(out NPC npc)) {
                    continue;
                }
                npc.life = 0;
                npc.active = false;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
            }
            cooldowns[activation.OwnerWho] = CooldownFrames;

            //沉湖记忆入账（能力复制的数据源）：只记主段类型，蠕虫组员不算；
            //记录活在所有者本机，服务器只发完成通报不留副本
            int primaryType = activation.Targets[0].Identity.Type;
            if (Main.netMode == NetmodeID.Server) {
                KikasaDrownNet.SendComplete(activation.OwnerWho, primaryType);
            }
            else {
                Main.player[activation.OwnerWho]
                    .GetModPlayer<KikasaServants.KikasaServantPlayer>()
                    .RecordDrowned(primaryType);
            }
        }

        private static void CancelActivation(DrownActivation activation, int index, string clause) {
            activations.RemoveAt(index);
            cooldowns[activation.OwnerWho] = CooldownFrames / 2;
            Reject(activation.OwnerWho, $"cancel:{clause}");
            if (Main.netMode == NetmodeID.Server) {
                KikasaDrownNet.SendCancel(activation.DrownId);
            }
            else {
                KikasaDrownFX.CancelShow(activation.DrownId);
            }
        }

        internal static void Reset() {
            activations.Clear();
            groupBuffer.Clear();
            for (int i = 0; i < cooldowns.Length; i++) {
                cooldowns[i] = 0;
            }
            localLockUntil = 0;
        }
    }
}
