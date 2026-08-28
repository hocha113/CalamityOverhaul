using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 鬼伞·沉玩家（联机 PvP 专属）。鬼湖把敌对玩家拖入水下，鬼手束缚约十秒无法移动，
    /// 期满松手；物品照常可用（束缚的是身体不是手），镜子类瞬移会被鬼手拽回。
    /// 准入镜像 PvP 骇入口径：双方 hostile 且非同一支非零队伍，距离用 PvP 短界（跨屏沉人没有反制窗口）。
    /// 权威模型同沉溺：服务器只验资格/距离/频率，不验领域（服务器没有领域状态是既定契约）；
    /// 玩家移动是客户端权威，钉身只在受害者本机执行（远端强钉会与原版位置同步互搏），
    /// 各端从 Apply 载荷推同一条确定性轨迹，服务器定期重播 Apply 自愈丢包与中途加入。
    /// 领域中途塌掉（收域/入梦/掉线）由各端从已同步的领域快照自察放人，杀死施术者也会松手。
    /// </summary>
    internal static class KikasaPlayerDrown
    {
        //==================== 时间轴（60fps，与 FX 共用一份真相）====================

        public const int ConvergeEnd = 16;
        public const int TenseBeat = 44;
        public const int StruggleStart = 46;
        public const int StruggleEnd = 64;
        public const int DragEnd = 88;

        /// <summary>没入水线后的束缚长押（用户口径"大约十秒"）</summary>
        public const int BindHoldFrames = 600;

        /// <summary>束缚全程（受害者钉身与权威计时共用）</summary>
        public const int TotalFrames = DragEnd + BindHoldFrames;

        //==================== 准入数值 ====================

        /// <summary>PvP 沉人最大距离：镜像 HackPvPRules.MaxDistance 的口径，
        /// 刻意小于 PvE 沉溺的 4000，跨屏沉人没有反制窗口</summary>
        public const float MaxPlayerRange = 2400f;

        /// <summary>束缚点在水线下的深度（人整个没入，手还包得住）</summary>
        public const float BindDepth = 110f;

        /// <summary>同一受害者释放后的再沉保护（服务器账），堵住轮流锁人的死循环</summary>
        public const int VictimRebindFrames = 600;

        //==================== 钉身参数 ====================

        //转向式钉身：每帧把速度写成"朝轨迹点的位移"，原版碰撞随后生效，不穿地不摔伤。
        //步长上限只在拖入段的加速末尾与瞬移拽回时触顶，长押段位移近零

        private const float MaxSteerStep = 64f;

        /// <summary>横向大位移视作瞬移逃逸（镜子/回忆），直接按回束缚点；
        /// 拖拽轨迹本身只走竖直，横向永远差不出这么多</summary>
        private const float TeleportSnapPx = 400f;

        private const float TenseJolt = 3f;

        //==================== 权威记录（服务器）====================

        internal sealed class BindActivation
        {
            public int OwnerWho;
            public int VictimWho;
            /// <summary>槽位复用第二重校验（§4.2）：掉线后新人顶槽不能继承束缚</summary>
            public string VictimName;
            public int BindId;
            public float Seed;
            public float LakeY;
            public int Timer;
            public int ResendTimer;
        }

        private static readonly List<BindActivation> activations = [];
        private static readonly int[] victimRebind = new int[Main.maxPlayers];
        private static int nextBindId;

        /// <summary>Apply 重播间隔：丢包、迟到与中途加入靠它自愈（收端幂等）</summary>
        private const int ResendIntervalFrames = 120;

        //==================== 客户端镜像（每端一份）====================

        internal sealed class ClientBind
        {
            public int BindId;
            public int OwnerWho;
            public int VictimWho;
            public int Timer;
            public float Seed;
            /// <summary>Apply 载荷的水线，施术者领域快照还没到时的兜底</summary>
            public float LakeYFallback;
            /// <summary>受害者本机盖章的轨迹起点（玩家位置本机权威，不用服务器的旧值防回弹）</summary>
            public Vector2 StartCenter;
            public bool StartStamped;
        }

        private static readonly List<ClientBind> binds = [];

        //==================== 资格 ====================

        /// <summary>
        /// 共享资格谓词：施术者预检与服务器复检同一份。hostile 双向与队伍子句
        /// 镜像原版 PvP 弹幕命中（同 HackPvPRules.CanTarget 前两条）；
        /// "已被束缚"查本端持有的表（服务器查权威账、客户端查镜像），缺数据端自动放行
        /// </summary>
        internal static bool IsEligibleVictim(Player caster, Player victim, out string clause) {
            if (caster?.active != true || caster.dead
                || victim?.active != true || victim.dead || victim.ghost
                || caster.whoAmI == victim.whoAmI) {
                clause = "invalid-target";
                return false;
            }
            if (!caster.hostile || !victim.hostile) {
                clause = "not-hostile";
                return false;
            }
            if (caster.team != 0 && caster.team == victim.team) {
                clause = "same-team";
                return false;
            }
            if (victim.shimmering || victim.shimmerWet) {
                clause = "shimmering";
                return false;
            }
            if (Vector2.Distance(victim.Center, caster.Center) > MaxPlayerRange) {
                clause = "out-of-range";
                return false;
            }
            if (IsBoundAny(victim.whoAmI)) {
                clause = "already-bound";
                return false;
            }
            clause = "ok";
            return true;
        }

        private static bool IsBoundAny(int victimWho) {
            for (int i = 0; i < activations.Count; i++) {
                if (activations[i].VictimWho == victimWho) {
                    return true;
                }
            }
            for (int i = 0; i < binds.Count; i++) {
                if (binds[i].VictimWho == victimWho) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>权威端施术者是否在押（同一双手：沉溺/鞭笞开场前也要问它）</summary>
        internal static bool HasBindFor(int ownerWho) {
            for (int i = 0; i < activations.Count; i++) {
                if (activations[i].OwnerWho == ownerWho) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>本机镜像里施术者是否在押（客户端预检口径）</summary>
        internal static bool HasClientBindFor(int ownerWho) {
            for (int i = 0; i < binds.Count; i++) {
                if (binds[i].OwnerWho == ownerWho) {
                    return true;
                }
            }
            return false;
        }

        internal static ClientBind GetClientBind(int bindId) {
            for (int i = 0; i < binds.Count; i++) {
                if (binds[i].BindId == bindId) {
                    return binds[i];
                }
            }
            return null;
        }

        //==================== 客户端入口 ====================

        /// <summary>
        /// 光标下有敌对玩家时受理沉人，返回是否消费了这次按键。
        /// precise=true 只认精确命中（优先级压过沉生物的吸附），false 走吸附兜底。
        /// 消费语义与沉生物不同：资格不成立（队友/未开 PvP/超距）一律静默放行落空，
        /// 悬着队友按沉入不能吞掉沉生物与沉手中物；资格成立后的湖况/冷却拒绝才算消费。
        /// 只存在于联机：单机没有第二个玩家
        /// </summary>
        internal static bool TryDrownAtCursor(Player player, bool precise) {
            if (player == null || player.whoAmI != Main.myPlayer
                || Main.netMode != NetmodeID.MultiplayerClient) {
                return false;
            }
            Player victim = FindCursorPlayer(player, precise);
            if (victim == null || !IsEligibleVictim(player, victim, out _)) {
                return false;
            }

            //意图已明确指向敌对玩家，以下拒绝均消费按键
            if (!player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                Refuse(player);
                return true;
            }
            if (KikasaDrown.LocalCooldown01 > 0f
                || KikasaDrownFX.HasActiveShowFor(player.whoAmI)
                || KikasaScourgeFX.HasPressBlockingShowFor(player.whoAmI)
                || HasClientBindFor(player.whoAmI)
                || KikasaPlayerDrownFX.HasActiveShowFor(player.whoAmI)) {
                Refuse(player);
                return true;
            }
            float lakeY = LakeYOf(player);
            if (victim.Center.Y < lakeY - KikasaDrown.GrabHeightFor(player)
                || victim.Center.Y > lakeY + KikasaDrown.MaxGrabDepth) {
                Refuse(player);
                return true;
            }

            //受理预兆：沸腾双涟漪，预告这一按是拖人不是拖怪
            KikasaDomainDeco.RippleAt(new Vector2(victim.Center.X, lakeY), 0.7f);
            KikasaDomainDeco.RippleAt(new Vector2(victim.Center.X - 16f, lakeY), 0.45f);
            KikasaDomainDeco.RippleAt(new Vector2(victim.Center.X + 16f, lakeY), 0.45f);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.85f, MaxInstances = 2 }, victim.Center);

            //请求在途短锁，防连点；真限频在权威端
            KikasaDrown.LockLocal(60);
            KikasaPlayerDrownNet.SendRequest(victim.whoAmI, lakeY);
            return true;
        }

        /// <summary>光标选玩家：精确命中（外扩 12px）或吸附半径内最近者，尺子同沉生物</summary>
        private static Player FindCursorPlayer(Player caster, bool precise) {
            Vector2 mouse = Main.MouseWorld;
            Player best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player victim = Main.player[i];
                //选中只留技术性过滤，资格与队伍在受理处
                if (victim?.active != true || victim.dead || victim.ghost
                    || i == caster.whoAmI) {
                    continue;
                }
                Rectangle hitbox = victim.Hitbox;
                hitbox.Inflate(12, 12);
                if (precise) {
                    if (!hitbox.Contains(mouse.ToPoint())) {
                        continue;
                    }
                    float distSq = Vector2.DistanceSquared(victim.Center, mouse);
                    if (distSq < bestDist) {
                        bestDist = distSq;
                        best = victim;
                    }
                    continue;
                }
                Vector2 clamped = new(
                    MathHelper.Clamp(mouse.X, hitbox.Left, hitbox.Right),
                    MathHelper.Clamp(mouse.Y, hitbox.Top, hitbox.Bottom));
                float dist = Vector2.Distance(mouse, clamped);
                if (dist <= KikasaDrown.SnapRadius && dist < bestDist) {
                    bestDist = dist;
                    best = victim;
                }
            }
            return best;
        }

        private static float LakeYOf(Player player)
            => player.GetModPlayer<KikasaDomainPlayer>().LakeWorldY;

        private static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, player.Center);
        }

        //==================== 悬停预兆 ====================

        private static int hoverOmenTimer;
        private static int hoverOmenPlr = -1;

        /// <summary>精确指着够资格的敌对玩家：按下会走沉人分支，沉生物的悬停预兆让位</summary>
        internal static bool HoverTargetsPlayer(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return false;
            }
            Player victim = FindCursorPlayer(player, precise: true);
            return victim != null && IsEligibleVictim(player, victim, out _);
        }

        /// <summary>
        /// 沉人悬停预兆：沸腾式（密拍双涟漪+血珠上跳），预告这一按是 PvP 拖人。
        /// 精确命中优先；无生物目标时吸附命中也应声，与按键分流恒等
        /// </summary>
        internal static void UpdateHoverOmen() {
            Player player = Main.LocalPlayer;
            if (Main.netMode != NetmodeID.MultiplayerClient
                || player == null || !player.active || player.dead || Main.gameMenu) {
                hoverOmenPlr = -1;
                return;
            }
            if (KikasaDrown.LocalCooldown01 > 0f
                || KikasaDrownFX.HasActiveShowFor(player.whoAmI)
                || KikasaScourgeFX.HasPressBlockingShowFor(player.whoAmI)
                || HasClientBindFor(player.whoAmI)
                || KikasaPlayerDrownFX.HasActiveShowFor(player.whoAmI)
                || !player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                hoverOmenPlr = -1;
                return;
            }
            Player victim = FindCursorPlayer(player, precise: true);
            if (victim == null && KikasaDrown.FindCursorTarget() == null) {
                victim = FindCursorPlayer(player, precise: false);
            }
            float lakeY = LakeYOf(player);
            bool reachable = victim != null && IsEligibleVictim(player, victim, out _)
                && victim.Center.Y >= lakeY - KikasaDrown.GrabHeightFor(player)
                && victim.Center.Y <= lakeY + KikasaDrown.MaxGrabDepth;
            if (!reachable) {
                hoverOmenPlr = -1;
                return;
            }
            if (victim.whoAmI != hoverOmenPlr) {
                hoverOmenPlr = victim.whoAmI;
                hoverOmenTimer = 8;
            }
            if (--hoverOmenTimer > 0) {
                return;
            }
            //沸腾：更密的拍子、成对的小涟漪、偶发血珠跳出水线
            hoverOmenTimer = 12;
            float x = victim.Center.X;
            KikasaDomainDeco.RippleAt(
                new Vector2(x + Main.rand.NextFloat(-12f, 12f), lakeY), 0.24f);
            KikasaDomainDeco.RippleAt(
                new Vector2(x + Main.rand.NextFloat(-12f, 12f), lakeY), 0.18f);
            if (Main.rand.NextBool(3)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRTTypes.PRT_GhostRainDrop>(
                    new Vector2(x + Main.rand.NextFloat(-9f, 9f), lakeY - 2f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.2f, 2.2f)),
                    KikasaDomain.CoolTint(new Color(237, 77, 69), new Color(126, 158, 164)) * 0.5f,
                    Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(10, 16), 0f);
            }
        }

        //==================== 权威路径（服务器）====================

        /// <summary>服务器收到请求：来源以连接为准，水线是施术者报的骰，只做幅度钳制（服务器没有领域）</summary>
        internal static void HandleRequest(int casterWho, int victimWho, float lakeY) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            Player caster = casterWho >= 0 && casterWho < Main.maxPlayers ? Main.player[casterWho] : null;
            if (caster?.active != true) {
                Reject(casterWho, "owner-invalid");
                return;
            }
            Player victim = victimWho >= 0 && victimWho < Main.maxPlayers ? Main.player[victimWho] : null;
            if (victim?.active != true) {
                Reject(casterWho, "victim-invalid");
                return;
            }
            if (!float.IsFinite(lakeY)
                || MathF.Abs(lakeY - victim.Center.Y)
                    > KikasaDrown.MaxGrabHeightHardCap + KikasaDrown.MaxGrabDepth) {
                Reject(casterWho, "lakey-insane");
                return;
            }
            StartAuthoritative(caster, victim, lakeY);
        }

        internal static bool StartAuthoritative(Player caster, Player victim, float lakeY) {
            if (Main.netMode != NetmodeID.Server) {
                return false;
            }
            if (caster?.active != true || caster.dead) {
                Reject(caster?.whoAmI ?? -1, "owner-dead");
                return false;
            }
            int ownerWho = caster.whoAmI;
            //同一双手：NPC 沉溺/鞭笞/沉人共用一本忙账与冷却账
            if (KikasaDrown.IsCoolingDown(ownerWho) || KikasaDrown.HasActivationFor(ownerWho)
                || KikasaScourge.HasPunishActivationFor(ownerWho) || HasBindFor(ownerWho)) {
                Reject(ownerWho, "cooldown-or-busy");
                return false;
            }
            if (victimRebind[victim.whoAmI] > 0) {
                Reject(ownerWho, "victim-rebind-cd");
                return false;
            }
            if (!IsEligibleVictim(caster, victim, out string clause)) {
                Reject(ownerWho, clause);
                return false;
            }

            BindActivation activation = new() {
                OwnerWho = ownerWho,
                VictimWho = victim.whoAmI,
                VictimName = victim.name,
                BindId = ++nextBindId,
                Seed = Main.rand.NextFloat(1000f),
                LakeY = lakeY,
                ResendTimer = ResendIntervalFrames,
            };
            activations.Add(activation);
            KikasaPlayerDrownNet.SendApply(activation);
            return true;
        }

        /// <summary>由 KikasaDrownSystem 逐帧驱动（服务器）；到点完成无需广播，各端确定性同拍结束</summary>
        internal static void UpdateAuthority() {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            for (int i = 0; i < victimRebind.Length; i++) {
                if (victimRebind[i] > 0) {
                    victimRebind[i]--;
                }
            }

            for (int i = activations.Count - 1; i >= 0; i--) {
                BindActivation activation = activations[i];
                Player caster = Main.player[activation.OwnerWho];
                Player victim = Main.player[activation.VictimWho];
                //杀死施术者即松手：十秒硬束缚的反制窗口
                if (caster?.active != true || caster.dead) {
                    CancelActivation(activation, i, "owner-lost");
                    continue;
                }
                if (victim?.active != true || victim.name != activation.VictimName) {
                    CancelActivation(activation, i, "victim-lost");
                    continue;
                }
                if (victim.dead) {
                    CancelActivation(activation, i, "victim-dead");
                    continue;
                }

                activation.Timer++;
                if (--activation.ResendTimer <= 0) {
                    activation.ResendTimer = ResendIntervalFrames;
                    KikasaPlayerDrownNet.SendApply(activation);
                }
                if (activation.Timer >= TotalFrames) {
                    CompleteActivation(activation);
                    activations.RemoveAt(i);
                }
            }
        }

        private static void CompleteActivation(BindActivation activation) {
            KikasaDrown.SetCooldown(activation.OwnerWho, KikasaDrown.CooldownFrames);
            victimRebind[activation.VictimWho] = VictimRebindFrames;
        }

        private static void CancelActivation(BindActivation activation, int index, string clause) {
            activations.RemoveAt(index);
            KikasaDrown.SetCooldown(activation.OwnerWho, KikasaDrown.CooldownFrames / 2);
            victimRebind[activation.VictimWho] = VictimRebindFrames / 2;
            Reject(activation.OwnerWho, $"cancel:{clause}");
            KikasaPlayerDrownNet.SendCancel(activation.BindId);
        }

        //被拒的请求写日志：静默拒绝没法诊断（§2.8）
        private static void Reject(int ownerWho, string clause) {
            CWRMod.Instance?.Logger?.Info($"[KikasaPlayerDrown] reject owner={ownerWho} clause={clause}");
        }

        //==================== 客户端镜像推进 ====================

        /// <summary>Apply 落地：幂等去重，计时只快进不回拨（§7.5）；
        /// 受害者本机盖轨迹起点章（玩家位置本机权威，不吃服务器旧值的回弹）</summary>
        internal static void ApplyFromNet(int ownerWho, int victimWho, int bindId,
            float seed, float lakeY, int elapsed) {
            if (Main.dedServ || elapsed >= TotalFrames) {
                return;
            }
            ClientBind existing = GetClientBind(bindId);
            if (existing != null) {
                if (elapsed > existing.Timer + 12) {
                    existing.Timer = elapsed;
                }
                return;
            }
            Player caster = Main.player[ownerWho];
            Player victim = Main.player[victimWho];
            if (caster?.active != true || victim?.active != true) {
                return;
            }
            //本机视角湖已不在（收域/入梦）就不接：接了下一帧也会被放掉，防重播抖动
            if (!LakeAliveFor(ownerWho)) {
                return;
            }

            ClientBind bind = new() {
                BindId = bindId,
                OwnerWho = ownerWho,
                VictimWho = victimWho,
                Timer = elapsed,
                Seed = seed,
                LakeYFallback = lakeY,
                StartCenter = victim.Center,
                StartStamped = true,
            };
            binds.Add(bind);
            KikasaPlayerDrownFX.StartShow(bind);
        }

        internal static void CancelFromNet(int bindId) {
            for (int i = 0; i < binds.Count; i++) {
                if (binds[i].BindId == bindId) {
                    ReleaseBind(i, cancelled: true);
                    return;
                }
            }
        }

        /// <summary>由 KikasaDrownSystem 逐帧驱动（仅客户端）：计时、湖况自察、到点释放</summary>
        internal static void UpdateClient() {
            for (int i = binds.Count - 1; i >= 0; i--) {
                ClientBind bind = binds[i];
                Player victim = Main.player[bind.VictimWho];
                if (victim?.active != true || victim.dead || victim.ghost) {
                    ReleaseBind(i, cancelled: true);
                    continue;
                }
                //湖塌了束缚没有依托：本地立即放人，不等服务器（服务器没有领域状态）
                if (!LakeAliveFor(bind.OwnerWho)) {
                    ReleaseBind(i, cancelled: true);
                    continue;
                }
                bind.Timer++;
                if (bind.Timer >= TotalFrames) {
                    ReleaseBind(i, cancelled: false);
                }
            }
        }

        private static void ReleaseBind(int index, bool cancelled) {
            ClientBind bind = binds[index];
            binds.RemoveAt(index);
            //受害者本机清掉残余转向速度，松手帧不带滑行
            if (bind.VictimWho == Main.myPlayer) {
                Player victim = Main.player[bind.VictimWho];
                if (victim?.active == true) {
                    victim.velocity = Vector2.Zero;
                    victim.fallStart = victim.fallStart2 = (int)(victim.position.Y / 16f);
                }
            }
            KikasaPlayerDrownFX.OnBindEnd(bind.BindId, cancelled);
        }

        /// <summary>施术者的湖此刻是否成立（本机视角，各端从同步快照自算同一答案）</summary>
        internal static bool LakeAliveFor(int ownerWho) {
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers) {
                return false;
            }
            Player owner = Main.player[ownerWho];
            return owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT >= 0.9f
                && !domain.DreamWorldVisual;
        }

        /// <summary>束缚参照的活水线：领域快照在手用实时值（引潮会动），否则用 Apply 载荷兜底</summary>
        internal static float LiveLakeYFor(int ownerWho, float fallback) {
            Player owner = ownerWho >= 0 && ownerWho < Main.maxPlayers ? Main.player[ownerWho] : null;
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive) {
                return domain.LakeWorldY;
            }
            return fallback;
        }

        //==================== 钉身（仅受害者本机）====================

        /// <summary>
        /// 移动应用前调用（<see cref="KikasaDomainPlayer.PreUpdateMovement"/>）。
        /// 只在受害者本机执行：速度改写为"朝轨迹点的位移"，原版碰撞随后裁决，
        /// 不穿地、无摔伤；钩爪即断，横向瞬移被按回束缚点
        /// </summary>
        internal static void ApplyBindMovement(Player player) {
            if (Main.dedServ || player.whoAmI != Main.myPlayer || binds.Count == 0) {
                return;
            }
            ClientBind bind = null;
            for (int i = 0; i < binds.Count; i++) {
                if (binds[i].VictimWho == player.whoAmI) {
                    bind = binds[i];
                    break;
                }
            }
            if (bind == null || !bind.StartStamped) {
                return;
            }

            //钩爪在位移前拽人，束缚期直接斩断；再射的新钩下一帧同样断
            if (player.grapCount > 0) {
                player.RemoveAllGrapplingHooks();
            }
            player.pulley = false;

            Vector2 target = PinCenterAt(bind);
            Vector2 delta = target - player.Center;
            if (MathF.Abs(delta.X) > TeleportSnapPx) {
                //镜子/回忆的瞬移被鬼手拽回
                player.Center = target;
                player.velocity = Vector2.Zero;
            }
            else {
                player.velocity = new Vector2(
                    MathHelper.Clamp(delta.X, -MaxSteerStep, MaxSteerStep),
                    MathHelper.Clamp(delta.Y, -MaxSteerStep, MaxSteerStep));
            }
            //血湖抓人，不结算摔落伤害
            player.fallStart = player.fallStart2 = (int)(player.position.Y / 16f);
        }

        /// <summary>确定性轨迹：抓握钉位 → 绷紧一挫 → 两轮衰减挣扎 → p² 加速拖入 → 水下定点长押</summary>
        private static Vector2 PinCenterAt(ClientBind bind) {
            float lakeY = LiveLakeYFor(bind.OwnerWho, bind.LakeYFallback);
            float bindY = lakeY + BindDepth;
            Vector2 start = bind.StartCenter;
            int t = bind.Timer;

            if (t <= TenseBeat) {
                return start;
            }
            if (t <= StruggleStart) {
                //绷紧拍：两帧内被拽下一小截，手赢了第一口气
                return new Vector2(start.X, start.Y
                    + TenseJolt * (t - TenseBeat) / (float)(StruggleStart - TenseBeat));
            }
            if (t <= StruggleEnd) {
                float st = t - StruggleStart;
                float decay = 1f - st / (StruggleEnd - StruggleStart);
                //上挣与被拽回的拉锯，振幅衰减、重心渐沉
                float osc = MathF.Sin(st * 0.52f) * 5f * decay;
                return new Vector2(start.X, start.Y + TenseJolt - osc + st * 0.30f);
            }
            if (t <= DragEnd) {
                float p = (t - StruggleEnd) / (float)(DragEnd - StruggleEnd);
                float startY = start.Y + TenseJolt + (StruggleEnd - StruggleStart) * 0.30f;
                //加速下拉，禁匀速
                return new Vector2(start.X, MathHelper.Lerp(startY, bindY, p * p));
            }
            //长押定点：纹丝不动，"无法移动"就是字面意思
            return new Vector2(start.X, bindY);
        }

        internal static void Reset() {
            activations.Clear();
            binds.Clear();
            for (int i = 0; i < victimRebind.Length; i++) {
                victimRebind[i] = 0;
            }
        }
    }
}
