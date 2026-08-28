using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 鬼伞·鞭笞权威。未击败的 BOSS 沉不下去，湖改为出手抽打：
    /// 权威端按确定性节拍对目标 <see cref="NPC.SimpleStrikeNPC"/>（服务器调用自带全端广播），
    /// 节拍帧与演出层共用一份常量，命中数字恰落在鞭中的那一帧。
    /// 同一套管线还带自动鞭击：湖域开启时所有者本机定频索敌上行请求，
    /// 服务器只做限频与资格/距离复检（服务器没有领域状态是既定契约）
    /// </summary>
    internal static class KikasaScourge
    {
        public const byte KindPunish = 0;
        public const byte KindAmbient = 1;

        //==================== 节拍表（权威与演出共用一份真相） ====================

        /// <summary>鞭笞打击帧：左抽、右抽、合掌下砸</summary>
        public static readonly int[] PunishBeats = [38, 62, 96];

        /// <summary>鞭笞演出总长</summary>
        public const int PunishLengthFrames = 128;

        /// <summary>鞭笞完成后的冷却（演出结束起算）</summary>
        public const int PunishCooldownFrames = 480;

        /// <summary>自动鞭击打击帧：单记</summary>
        public static readonly int[] AmbientBeats = [24];

        /// <summary>自动鞭击演出总长</summary>
        public const int AmbientLengthFrames = 56;

        /// <summary>自动鞭击的本机节奏</summary>
        public const int AmbientIntervalFrames = 180;

        /// <summary>服务器限频最小间隔，略短于本机节奏容忍网络抖动</summary>
        public const int AmbientMinGapFrames = 150;

        /// <summary>自动鞭击横向索敌半径（以玩家为心）</summary>
        public const float AmbientSeekRangeX = 1000f;

        internal static int[] BeatsOf(byte kind) => kind == KindAmbient ? AmbientBeats : PunishBeats;

        internal static int LengthOf(byte kind) => kind == KindAmbient ? AmbientLengthFrames : PunishLengthFrames;

        /// <summary>
        /// 第 k 记的出手侧 ±1（0=合掌下砸无侧向）：首记随种子，次记换边。
        /// 演出的手从这一侧抽出，权威按它定击退方向，两层必须同源
        /// </summary>
        internal static int StrikeSide(float seed, byte kind, int strikeIndex) {
            if (kind == KindPunish && strikeIndex >= 2) {
                return 0;
            }
            float h = MathF.Sin(seed * 12.9898f) * 43758.547f;
            int first = h - MathF.Floor(h) >= 0.5f ? 1 : -1;
            return strikeIndex % 2 == 0 ? first : -first;
        }

        //鞭笞三记的伤害倍率（×鬼伞面板），终结拍加倍
        private static readonly float[] punishMuls = [4f, 4f, 8f];

        /// <summary>自动鞭击伤害倍率，低于主动鞭笞</summary>
        private const float AmbientMul = 2f;

        //==================== 权威记录 ====================

        internal sealed class ScourgeActivation
        {
            public int OwnerWho;
            public int ScourgeId;
            public byte Kind;
            public float Seed;
            public NetworkNPCIdentity Target;
            public int Timer;
            public int BeatCursor;
        }

        //权威记录（服务器/单机）；客户端不持有
        private static readonly List<ScourgeActivation> activations = [];
        private static readonly int[] punishCooldowns = new int[Main.maxPlayers];
        private static readonly int[] ambientGaps = new int[Main.maxPlayers];
        private static int nextScourgeId;

        //所有者本机的自动鞭击节拍
        private static int ambientLocalTimer;

        internal static bool HasPunishActivationFor(int ownerWho) {
            for (int i = 0; i < activations.Count; i++) {
                if (activations[i].OwnerWho == ownerWho && activations[i].Kind == KindPunish) {
                    return true;
                }
            }
            return false;
        }

        private static bool HasAmbientActivationFor(int ownerWho) {
            for (int i = 0; i < activations.Count; i++) {
                if (activations[i].OwnerWho == ownerWho && activations[i].Kind == KindAmbient) {
                    return true;
                }
            }
            return false;
        }

        //==================== 鞭笞权威路径 ====================

        /// <summary>单机直通与服务器请求共用；由 KikasaDrown 的请求解析在门槛命中时转来</summary>
        internal static bool StartPunishAuthoritative(Player owner, NPC target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (owner?.active != true || owner.dead) {
                Reject(owner?.whoAmI ?? -1, "owner-dead");
                return false;
            }
            int ownerWho = owner.whoAmI;
            if (punishCooldowns[ownerWho] > 0 || HasPunishActivationFor(ownerWho)
                || KikasaDrown.HasActivationFor(ownerWho)
                || KikasaPlayerDrown.HasBindFor(ownerWho)) {
                Reject(ownerWho, "cooldown-or-busy");
                return false;
            }
            if (!KikasaDrown.IsEligibleTarget(target)) {
                Reject(ownerWho, "ineligible");
                return false;
            }
            if (Vector2.Distance(target.Center, owner.Center) > KikasaDrown.MaxRange) {
                Reject(ownerWho, "out-of-range");
                return false;
            }
            if (!NetworkNPCIdentity.TryCapture(target, out NetworkNPCIdentity identity)) {
                Reject(ownerWho, "identity-mint");
                return false;
            }

            //冷却从起演计入总账：目标中途死了也不白嫖手速
            punishCooldowns[ownerWho] = PunishLengthFrames + PunishCooldownFrames;
            StartActivation(owner, target, identity, KindPunish);
            return true;
        }

        //==================== 自动鞭击 ====================

        /// <summary>
        /// 所有者本机定频索敌：湖就绪且无任何鬼手演出在场时，抓最近的可追敌上行请求。
        /// 由 <see cref="KikasaDrownSystem.PostUpdateEverything"/> 逐帧泵动（仅客户端侧）
        /// </summary>
        internal static void UpdateLocalAmbient() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead || Main.gameMenu) {
                ambientLocalTimer = AmbientIntervalFrames;
                return;
            }
            if (HackTime.Active) {
                return;
            }
            if (!player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                //湖没就绪别攒拍，就绪后略候再出手
                ambientLocalTimer = Math.Max(ambientLocalTimer, 40);
                return;
            }
            if (KikasaDrownFX.HasActiveShowFor(player.whoAmI)
                || KikasaScourgeFX.HasActiveShowFor(player.whoAmI)
                || KikasaPlayerDrown.HasClientBindFor(player.whoAmI)) {
                return;
            }
            if (--ambientLocalTimer > 0) {
                return;
            }
            NPC target = FindAmbientTarget(player);
            if (target == null) {
                //没敌人，半秒后再看
                ambientLocalTimer = 30;
                return;
            }
            ambientLocalTimer = AmbientIntervalFrames;
            NetworkNPCIdentity.TryCapture(target, out NetworkNPCIdentity identity);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                KikasaDrownNet.SendAmbientRequest(target.whoAmI, target.type, identity.Generation);
            }
            else {
                StartAmbientAuthoritative(player, target);
            }
        }

        /// <summary>最近的可追敌：横向以玩家为心、纵向限湖面臂展窗</summary>
        private static NPC FindAmbientTarget(Player player) {
            float lakeY = player.GetModPlayer<KikasaDomains.KikasaDomainPlayer>().LakeWorldY;
            NPC best = null;
            float bestDistSq = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || !KikasaDrown.IsEligibleTarget(npc)) {
                    continue;
                }
                if (MathF.Abs(npc.Center.X - player.Center.X) > AmbientSeekRangeX
                    || npc.Center.Y < lakeY - KikasaDrown.MaxGrabHeight
                    || npc.Center.Y > lakeY + KikasaDrown.MaxGrabDepth) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(npc.Center, player.Center);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>服务器收到自动鞭击请求：解析同沉溺（generation 缺省按 index+type 回退）</summary>
        internal static void HandleAmbientRequest(int ownerWho, int npcIndex, int npcType, ulong generation) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            Player owner = ownerWho >= 0 && ownerWho < Main.maxPlayers ? Main.player[ownerWho] : null;
            if (owner?.active != true) {
                Reject(ownerWho, "ambient-owner-invalid");
                return;
            }
            NPC target = null;
            if (generation != 0) {
                NetworkNPCIdentity requested = new(npcIndex, npcType, generation);
                requested.TryResolve(out target);
            }
            if (target == null && npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                NPC candidate = Main.npc[npcIndex];
                if (candidate?.active == true && candidate.type == npcType) {
                    target = candidate;
                }
            }
            if (target == null) {
                Reject(ownerWho, "ambient-target-missing");
                return;
            }
            StartAmbientAuthoritative(owner, target);
        }

        internal static bool StartAmbientAuthoritative(Player owner, NPC target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (owner?.active != true || owner.dead) {
                return false;
            }
            int ownerWho = owner.whoAmI;
            //限频静默拒绝：自动节拍的抖动不值一条日志
            if (ambientGaps[ownerWho] > 0 || HasAmbientActivationFor(ownerWho)
                || HasPunishActivationFor(ownerWho) || KikasaDrown.HasActivationFor(ownerWho)
                || KikasaPlayerDrown.HasBindFor(ownerWho)) {
                return false;
            }
            if (!KikasaDrown.IsEligibleTarget(target) || !target.CanBeChasedBy()) {
                return false;
            }
            if (Vector2.Distance(target.Center, owner.Center) > KikasaDrown.MaxRange) {
                return false;
            }
            if (!NetworkNPCIdentity.TryCapture(target, out NetworkNPCIdentity identity)) {
                return false;
            }
            ambientGaps[ownerWho] = AmbientMinGapFrames;
            StartActivation(owner, target, identity, KindAmbient);
            return true;
        }

        //==================== 共同起演 ====================

        private static void StartActivation(Player owner, NPC target,
            NetworkNPCIdentity identity, byte kind) {
            ScourgeActivation activation = new() {
                OwnerWho = owner.whoAmI,
                ScourgeId = ++nextScourgeId,
                Kind = kind,
                Seed = Main.rand.NextFloat(1000f),
                Target = identity,
            };
            activations.Add(activation);

            if (Main.netMode == NetmodeID.Server) {
                KikasaDrownNet.SendScourgeApply(activation);
            }
            else {
                //单机：权威与演出同机同帧
                KikasaScourgeFX.StartShow(activation.OwnerWho, activation.ScourgeId,
                    activation.Seed, kind, identity);
            }
        }

        //==================== 权威推进 ====================

        /// <summary>由 KikasaDrownSystem 逐帧驱动；多人客户端无事可做</summary>
        internal static void UpdateAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            for (int i = 0; i < punishCooldowns.Length; i++) {
                if (punishCooldowns[i] > 0) {
                    punishCooldowns[i]--;
                }
                if (ambientGaps[i] > 0) {
                    ambientGaps[i]--;
                }
            }

            for (int i = activations.Count - 1; i >= 0; i--) {
                ScourgeActivation activation = activations[i];
                Player owner = Main.player[activation.OwnerWho];
                if (owner?.active != true) {
                    activations.RemoveAt(i);
                    continue;
                }

                activation.Timer++;
                int[] beats = BeatsOf(activation.Kind);
                while (activation.BeatCursor < beats.Length
                    && activation.Timer == beats[activation.BeatCursor]) {
                    TryStrike(activation, owner);
                    activation.BeatCursor++;
                }

                if (activation.Timer >= LengthOf(activation.Kind)) {
                    activations.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 打击拍：目标在场且够得着才结算，飞出臂展这一拍空挥不取消。
        /// 服务器上 SimpleStrikeNPC 自带 SendStrikeNPC 广播，命中数字与音效全端自然出现
        /// </summary>
        private static void TryStrike(ScourgeActivation activation, Player owner) {
            if (!activation.Target.TryResolve(out NPC target) || target.life <= 0) {
                return;
            }
            if (Vector2.Distance(target.Center, owner.Center) > KikasaDrown.MaxRange) {
                return;
            }

            int side = StrikeSide(activation.Seed, activation.Kind, activation.BeatCursor);
            float mul = activation.Kind == KindAmbient
                ? AmbientMul
                : punishMuls[Math.Min(activation.BeatCursor, punishMuls.Length - 1)];
            int damage = Math.Max((int)(KikasaOverride.GetPanelDamage(owner) * mul), 1);

            //手从 side 侧抽来，目标被拍向对侧；合掌下砸无横向击退
            int hitDirection = -side;
            float knockback = side == 0 ? 0f : activation.Kind == KindAmbient ? 3f : 5f;
            target.SimpleStrikeNPC(damage, hitDirection, false, knockback,
                DamageClass.Summon, damageVariation: true);
        }

        //被拒的请求写日志：静默拒绝没法诊断（自动鞭击的限频除外）
        private static void Reject(int ownerWho, string clause) {
            CWRMod.Instance?.Logger?.Info($"[KikasaScourge] reject owner={ownerWho} clause={clause}");
        }

        internal static void Reset() {
            activations.Clear();
            for (int i = 0; i < punishCooldowns.Length; i++) {
                punishCooldowns[i] = 0;
                ambientGaps[i] = 0;
            }
            ambientLocalTimer = 0;
        }
    }
}
