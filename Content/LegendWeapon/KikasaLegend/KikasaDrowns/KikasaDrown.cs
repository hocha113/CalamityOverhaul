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
    /// BOSS 门槛：未在本世界击败过的 BOSS 级目标沉不下去，请求解析处按
    /// <see cref="KikasaBossGate.DrownBlocked"/> 分流去 <see cref="KikasaScourge"/> 鞭笞。
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

        /// <summary>湖上抓握高度基准：与 FX 层臂展预算对齐（段长动态定标 26..240，
        /// 6 节解算臂展 ≈1411px、抓点筛选 1350px），此界之内手臂真伸得到，
        /// 不会出现卷指计时驱动下指尖离目标半屏远照样"攥中"的隔空贴手。
        /// 玩家高于水线时经 <see cref="GrabHeightFor"/> 动态放宽，FX 段长与筛选同口径联动拉伸</summary>
        public const float MaxGrabHeight = 1200f;

        /// <summary>动态放宽的硬顶，防高空无限远抓跨图（反馈三·#28）</summary>
        public const float MaxGrabHeightHardCap = 2800f;

        /// <summary>湖面之下的容许深度，再深就是往地里抓</summary>
        public const float MaxGrabDepth = 600f;

        /// <summary>可及高度帽：玩家高于水线多少，帽就抬多少（反馈三·#28 拍板），硬顶封界</summary>
        public static float GrabHeightFor(Player player) {
            float rise = LakeYOf(player) - player.Center.Y;
            return MathHelper.Clamp(MaxGrabHeight + MathF.Max(rise, 0f),
                MaxGrabHeight, MaxGrabHeightHardCap);
        }

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
        private static int localLockTotal = CooldownFrames;

        //==================== 资格 ====================

        /// <summary>
        /// 共享资格谓词：客户端预检与服务器复检同一份（一处真相）。
        /// 这里只留技术性守卫（正被放逐或已在沉溺中的目标不能被第二只手抓）；
        /// BOSS 未击败的门槛不在资格里，在入口分流（<see cref="KikasaBossGate.DrownBlocked"/>，
        /// 命中者转 <see cref="KikasaScourge"/> 鞭笞），城镇与普通生物照旧全放开。
        /// 不可伤害体与事件核心（撒旦军团传送门/水晶等）除外——拖走事件门整场事件卡死（反馈六·#114）。
        /// 已击败月总部件的战斗无敌窗不挡抓取，见 <see cref="KikasaMoonLordDrown.IsDefeatedPart"/>。
        /// </summary>
        public static bool IsEligibleTarget(NPC npc)
            => npc?.active == true && npc.lifeMax > 0
            && (!npc.dontTakeDamage || KikasaMoonLordDrown.IsDefeatedPart(npc))
            && npc.type != NPCID.DD2LanePortal && npc.type != NPCID.DD2EterniaCrystal
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
        /// （冷却/湖未就绪的拒绝也算消费，玩家的意图明确是生物，不该误沉手中物）。
        /// 未击败的 BOSS 走鞭笞分流：单机直通 <see cref="KikasaScourge"/>，
        /// 联机仍发同一条请求，门槛真相由服务器重算分流
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
                || KikasaDrownFX.HasActiveShowFor(player.whoAmI)
                || KikasaScourgeFX.HasPressBlockingShowFor(player.whoAmI)
                || KikasaPlayerDrown.HasClientBindFor(player.whoAmI)
                || KikasaPlayerDrownFX.HasActiveShowFor(player.whoAmI)) {
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
                || hover.Center.Y < lakeY - GrabHeightFor(player)
                || hover.Center.Y > lakeY + MaxGrabDepth) {
                Refuse(player);
                return true;
            }

            //客户端可能铸不出 generation（§4.3），index+type 随行，服务器再盖自己的章
            NetworkNPCIdentity.TryCapture(hover, out NetworkNPCIdentity identity);

            //可丢弃的预兆预测；门槛目标是怒不是馋，水线炸一记沸腾
            bool gated = KikasaBossGate.DrownBlocked(hover);
            float omen = MathHelper.Clamp(
                MathF.Sqrt(hover.width * (float)hover.height) / 30f, 0.9f, 2.4f);
            if (gated) {
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(hover.Center.X, lakeY), 0.7f * omen);
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(hover.Center.X - 18f * omen, lakeY), 0.45f * omen);
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(hover.Center.X + 18f * omen, lakeY), 0.45f * omen);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.9f, MaxInstances = 2 }, hover.Center);
            }
            else {
                //湖面先应两圈涟漪，一声低水滴；圈随目标体型放大
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(hover.Center.X, lakeY), 0.5f * omen);
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(hover.Center.X + 24f * omen, lakeY), 0.35f * omen);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.55f, MaxInstances = 2 }, hover.Center);
            }

            //请求在途短锁，防连点；真限频在权威端。鞭笞的整段长锁在 Apply 到达起演时上
            LockLocal(60);

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                KikasaDrownNet.SendRequest(hover.whoAmI, hover.type, identity.Generation);
            }
            else if (gated) {
                KikasaScourge.StartPunishAuthoritative(player, hover);
            }
            else {
                StartAuthoritative(player, hover);
            }
            return true;
        }

        /// <summary>吸附半径：光标点到碰撞箱的距离在此之内就认指向，不必真戳中</summary>
        public const float SnapRadius = 96f;

        /// <summary>
        /// 光标选目标：精确命中（外扩 12px）优先；都没戳中时，吸附半径内
        /// 离光标最近的生物顶上。悬停预兆与按键共用本函数，预览恒等于按下结果；
        /// 沉玩家的吸附兜底也要先问它（生物优先于玩家吸附）
        /// </summary>
        internal static NPC FindCursorTarget() {
            Vector2 mouse = Main.MouseWorld;
            NPC best = null;
            float bestDistSq = float.MaxValue;
            NPC snapBest = null;
            float snapBestDist = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                //选中只留技术性过滤，资格与门槛在受理处
                if (npc?.active != true || npc.lifeMax <= 0) {
                    continue;
                }
                Rectangle hitbox = npc.Hitbox;
                hitbox.Inflate(12, 12);
                if (hitbox.Contains(mouse.ToPoint())) {
                    float distSq = Vector2.DistanceSquared(npc.Center, mouse);
                    if (distSq < bestDistSq) {
                        bestDistSq = distSq;
                        best = npc;
                    }
                    continue;
                }
                //点到碰撞箱最近点的距离，箱大箱小同一把尺
                Vector2 clamped = new(
                    MathHelper.Clamp(mouse.X, hitbox.Left, hitbox.Right),
                    MathHelper.Clamp(mouse.Y, hitbox.Top, hitbox.Bottom));
                float dist = Vector2.Distance(mouse, clamped);
                if (dist <= SnapRadius && dist < snapBestDist) {
                    snapBestDist = dist;
                    snapBest = npc;
                }
            }
            return best ?? snapBest;
        }

        //==================== 悬停预兆 ====================

        //悬停涟漪节拍与目标记忆：换目标时首圈快些应
        private static int hoverOmenTimer;
        private static int hoverOmenNpc = -1;

        /// <summary>
        /// 可沉暗示：湖就绪时光标悬着够得到的生物，它脚下的湖面先泛起极淡的涟漪
        /// 把按键后的涟漪预兆提前到悬停；门槛拦着的 BOSS 换沸腾预兆
        /// （密拍双涟漪+细血珠上跳），预告这一按是鞭笞不是拖拽。纯本机演出，
        /// 每帧由 <see cref="KikasaDrownSystem.PostUpdateEverything"/> 泵动
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
                || KikasaScourgeFX.HasPressBlockingShowFor(player.whoAmI)
                || KikasaPlayerDrown.HasClientBindFor(player.whoAmI)
                || KikasaPlayerDrownFX.HasActiveShowFor(player.whoAmI)
                || !player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                hoverOmenNpc = -1;
                return;
            }
            //精确指着够资格的敌对玩家：这一按走沉人分支，生物预兆让位（预览恒等于按下结果）
            if (KikasaPlayerDrown.HoverTargetsPlayer(player)) {
                hoverOmenNpc = -1;
                return;
            }
            NPC hover = FindCursorTarget();
            float lakeY = LakeYOf(player);
            bool reachable = hover != null && IsEligibleTarget(hover)
                && Vector2.Distance(hover.Center, player.Center) <= MaxRange
                && hover.Center.Y >= lakeY - GrabHeightFor(player)
                && hover.Center.Y <= lakeY + MaxGrabDepth;
            if (!reachable) {
                hoverOmenNpc = -1;
                return;
            }

            bool gated = KikasaBossGate.DrownBlocked(hover);
            if (hover.whoAmI != hoverOmenNpc) {
                hoverOmenNpc = hover.whoAmI;
                //换目标：首圈略等半拍，避免扫过一排怪时满屏起涟漪
                hoverOmenTimer = 8;
            }
            if (--hoverOmenTimer > 0) {
                return;
            }
            float omen = MathHelper.Clamp(
                MathF.Sqrt(hover.width * (float)hover.height) / 30f, 0.9f, 2.4f);
            if (gated) {
                //沸腾：更密的拍子、成对的小涟漪、偶发血珠跳出水线
                hoverOmenTimer = 12;
                float x = hover.Center.X;
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(x + Main.rand.NextFloat(-14f, 14f) * omen, lakeY),
                    MathF.Min(0.24f * omen, 0.28f));
                KikasaDomains.KikasaDomainDeco.RippleAt(
                    new Vector2(x + Main.rand.NextFloat(-14f, 14f) * omen, lakeY),
                    MathF.Min(0.18f * omen, 0.24f));
                if (Main.rand.NextBool(3)) {
                    InnoVault.PRT.PRTLoader.NewParticle<PRTTypes.PRT_GhostRainDrop>(
                        new Vector2(x + Main.rand.NextFloat(-10f, 10f) * omen, lakeY - 2f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.2f, 2.2f)),
                        KikasaDomains.KikasaDomain.CoolTint(new Color(237, 77, 69), new Color(126, 158, 164)) * 0.5f,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(10, 16), 0f);
                }
                return;
            }
            hoverOmenTimer = 22;
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

        /// <summary>
        /// 本机锁统一入口：沉溺与鞭笞共用同一把"手忙"锁（同一根键、同一双手），
        /// 各自按自己的时长上锁，HUD 冷却弧按锁总长归一
        /// </summary>
        internal static void LockLocal(int frames) {
            localLockUntil = Main.GameUpdateCount + (uint)Math.Max(frames, 1);
            localLockTotal = Math.Max(frames, 1);
        }

        /// <summary>本机演出谢幕（完成或取消）后的冷却，由 FX 层回调</summary>
        internal static void OnLocalShowEnded(int ownerWho) {
            if (ownerWho == Main.myPlayer) {
                LockLocal(CooldownFrames);
            }
        }

        /// <summary>本机沉溺手的锁剩余 0~1（1=刚上锁），HUD 冷却弧消费；无锁=0</summary>
        internal static float LocalCooldown01 {
            get {
                if (Main.GameUpdateCount >= localLockUntil) {
                    return 0f;
                }
                return MathHelper.Clamp(
                    (localLockUntil - Main.GameUpdateCount) / (float)localLockTotal, 0f, 1f);
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
            //门槛分流以服务器为准：未击败的 BOSS 转鞭笞，客户端预测只是演出口径
            if (KikasaBossGate.DrownBlocked(target)) {
                KikasaScourge.StartPunishAuthoritative(owner, target);
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
            if (cooldowns[ownerWho] > 0 || HasActivationFor(ownerWho)
                || KikasaScourge.HasPunishActivationFor(ownerWho)
                || KikasaPlayerDrown.HasBindFor(ownerWho)) {
                Reject(ownerWho, "cooldown-or-busy");
                return false;
            }
            if (!IsEligibleTarget(target)) {
                Reject(ownerWho, "ineligible");
                return false;
            }
            //双保险：门槛目标不该走到沉溺权威（分流在请求解析处），走到就按资格拒绝
            if (KikasaBossGate.DrownBlocked(target)) {
                Reject(ownerWho, "boss-gated");
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

            //蠕虫等整组一起封印；月总按核心索引收齐（含无敌残口），组员不再走资格过滤
            bool moonFamily = KikasaMoonLordDrown.IsPart(target.type);
            if (moonFamily) {
                KikasaMoonLordDrown.CollectFamily(target, groupBuffer);
            }
            else {
                NpcGroupHelper.CollectGroup(target, groupBuffer);
            }
            for (int i = 0; i < groupBuffer.Count; i++) {
                NPC member = groupBuffer[i];
                if (member == null || member == target) {
                    continue;
                }
                if (!moonFamily && !IsEligibleTarget(member)) {
                    continue;
                }
                if (moonFamily && IsDrowningAuthority(member.whoAmI)) {
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

        internal static bool HasActivationFor(int ownerWho) {
            for (int i = 0; i < activations.Count; i++) {
                if (activations[i].OwnerWho == ownerWho) {
                    return true;
                }
            }
            return false;
        }

        //同一双手一本冷却账：沉玩家（KikasaPlayerDrown）与沉溺共用，这两个口子给它读写

        internal static bool IsCoolingDown(int ownerWho)
            => ownerWho >= 0 && ownerWho < cooldowns.Length && cooldowns[ownerWho] > 0;

        internal static void SetCooldown(int ownerWho, int frames) {
            if (ownerWho >= 0 && ownerWho < cooldowns.Length) {
                cooldowns[ownerWho] = Math.Max(cooldowns[ownerWho], frames);
            }
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
            int moonCoreWho = -1;
            if (KikasaMoonLordDrown.IsPart(activation.Targets[0].Identity.Type)
                && activation.Targets[0].Identity.TryResolve(out NPC primaryNpc)) {
                moonCoreWho = KikasaMoonLordDrown.CoreIndexOf(primaryNpc);
            }

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
            if (moonCoreWho >= 0) {
                KikasaMoonLordDrown.SweepRemainders(moonCoreWho);
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
            localLockTotal = CooldownFrames;
        }
    }
}
