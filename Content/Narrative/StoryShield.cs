using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative
{
    /// <summary>
    /// 剧情执行保护：鬼雨初遇线（入雨→夺伞/被拖→沈幽初见→送客）与鬼伞首次教程期间，
    /// 本地玩家不受敌怪与敌对弹幕伤害，身边也不自然刷怪。<br/>
    /// 生命周期是「租约」不是开关：每帧由活谓词续租（<see cref="StoryShieldPlayer.PreUpdate"/>），
    /// 谓词一停，租约在 <see cref="GraceFrames"/> 内自然到期，没有任何地方需要「记得关掉」；
    /// 每个来源另有随存档只增不减的时长预算，预算烧完保护自行退场。
    /// 任何一环失守（谓词卡真、存档异常、包丢失）都只会让保护提前结束，不会让它多活一帧。<br/>
    /// 免伤只拦 NPC 与敌对弹幕来源：伞鬼走自定义伤害照常生效，被拖入深层的剧情不受影响；
    /// 也不写 <c>player.immune</c>，伞鬼的「免疫中不抓」判定不会被误挡。<br/>
    /// 服务器靠客户端心跳租约得知谁在受保护（<see cref="StoryShieldNet"/>），据此停掉该玩家周边的自然刷怪。
    /// </summary>
    internal static class StoryShield
    {
        /// <summary>谓词停止后保护再挂多少帧：接住演出尾巴与教程起步之间的空档</summary>
        internal const int GraceFrames = 120;

        /// <summary>鬼雨剧情线的保护总预算（随存档累计；正常走完约五分钟，留足九倍余量）</summary>
        internal const int StoryBudgetFrames = 60 * 60 * 45;

        /// <summary>首次教程的保护总预算（随存档累计；八步慢读也用不完）</summary>
        internal const int TutorialBudgetFrames = 60 * 60 * 12;

        /// <summary>服务器端租约：客户端心跳续租，心跳断流两秒内失效</summary>
        internal const int ServerLeaseFrames = 120;

        /// <summary>客户端受保护期间的心跳间隔（帧）</summary>
        internal const int HeartbeatInterval = 40;

        /// <summary>本地玩家此刻是否受保护；服务器与主菜单恒 false</summary>
        internal static bool LocalActive {
            get {
                if (Main.dedServ || Main.gameMenu) {
                    return false;
                }
                Player player = Main.LocalPlayer;
                return player?.active == true
                    && player.TryGetModPlayer(out StoryShieldPlayer shield)
                    && shield.Active;
            }
        }

        /// <summary>
        /// 鬼雨初遇线的活谓词：身处叠加层任意深度，或入雨/深潜/送出三段演出进行中。
        /// 伞一旦真正发放剧情即告终（门伞入子世界复用同一入雨演出，不算剧情）
        /// </summary>
        internal static bool StoryDemand() {
            if (ShenyoStorySync.KikasaGranted) {
                return false;
            }
            return OniRainWorldState.LocalIn
                || OniRainWorldTransition.Active
                || OniRainDescentTransition.Active
                || OniRainExitTransition.Active;
        }

        /// <summary>首次教程的活谓词：教学卡在讲，且这份存档的首次保护还没用掉</summary>
        internal static bool TutorialDemand() => KikasaHudLead.ShieldEligible;

        /// <summary>
        /// 受保护时要免掉的伤害来源：敌怪本体与敌对弹幕。
        /// PvP（来源带玩家槽位）与自定义来源（伞鬼抓人、环境）都放行
        /// </summary>
        internal static bool IsCreatureSource(PlayerDeathReason source) {
            if (source == null || source.SourcePlayerIndex >= 0) {
                return false;
            }
            return source.SourceNPCIndex >= 0 || source.SourceProjectileLocalIndex >= 0;
        }
    }

    /// <summary>
    /// 逐玩家的保护租约。本地端只有所有者自己续租；服务器端由心跳续租；
    /// 两份租约都是每帧递减的倒计时，不存在需要显式清零的开关
    /// </summary>
    internal sealed class StoryShieldPlayer : ModPlayer
    {
        //本地租约（帧）：所有者本机每帧按谓词续到 GraceFrames，否则递减
        private int lease;
        //服务器端租约（帧）：客户端心跳续到 ServerLeaseFrames，否则递减
        private int serverLease;
        //上次心跳报出的状态，边沿变化立即补发
        private bool lastReported;
        //预算烧尽的告警一档只记一次
        private bool storyBudgetWarned;
        private bool tutorialBudgetWarned;

        /// <summary>本端判定：租约未到期。只有所有者本机会续租，服务器与旁观端恒 false</summary>
        public bool Active => lease > 0;

        /// <summary>刷怪闸读它：单机与房主读本地租约，专用服务器读心跳租约</summary>
        public bool SuppressSpawns => lease > 0 || serverLease > 0;

        public override void Initialize() => ResetRuntime();

        public override void OnEnterWorld() => ResetRuntime();

        private void ResetRuntime() {
            lease = 0;
            serverLease = 0;
            lastReported = false;
            storyBudgetWarned = false;
            tutorialBudgetWarned = false;
        }

        /// <summary>
        /// 在玩家更新最前面续租/递减，同帧后续的碰撞与弹幕判定读到的就是本帧真值。
        /// PreUpdate 在死亡分支之前执行，人死了租约照样倒数
        /// </summary>
        public override void PreUpdate() {
            if (Main.netMode == NetmodeID.Server && serverLease > 0) {
                serverLease--;
            }
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            TickLocal();
        }

        private void TickLocal() {
            bool granted = false;

            if (StoryShield.StoryDemand()) {
                ShenyoStoryData story = ShenyoStorySync.Story;
                if (story.ShieldFramesUsed < StoryShield.StoryBudgetFrames) {
                    story.ShieldFramesUsed++;
                    granted = true;
                }
                else {
                    WarnBudgetOnce(ref storyBudgetWarned, "鬼雨剧情线");
                }
            }

            if (StoryShield.TutorialDemand()) {
                KikasaGuideData guide = Player.GetModPlayer<StoryPlayer>().Get<KikasaGuideData>();
                if (guide.ShieldFramesUsed < StoryShield.TutorialBudgetFrames) {
                    guide.ShieldFramesUsed++;
                    granted = true;
                }
                else {
                    WarnBudgetOnce(ref tutorialBudgetWarned, "鬼伞首次教程");
                }
            }

            if (granted) {
                lease = StoryShield.GraceFrames;
            }
            else if (lease > 0) {
                lease--;
            }

            Heartbeat();
        }

        private void WarnBudgetOnce(ref bool warned, string source) {
            if (warned) {
                return;
            }
            warned = true;
            CWRMod.Instance.Logger.Warn(
                $"StoryShield: {source} 的执行保护预算已耗尽，保护提前退场（玩家 {Player.name}）。" +
                "谓词仍为真说明该阶段停留异常久，请检查剧情/教程是否卡住");
        }

        /// <summary>联机客户端：受保护期间定期心跳，状态边沿立刻补发；单机与服务器静默</summary>
        private void Heartbeat() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            bool active = lease > 0;
            if (active == lastReported
                && !(active && Main.GameUpdateCount % StoryShield.HeartbeatInterval == 0)) {
                return;
            }
            lastReported = active;
            StoryShieldNet.Send(active);
        }

        /// <summary>服务器收到心跳：续到满租或即时释放，不信任更长的自报值</summary>
        internal void ApplyHeartbeat(bool active)
            => serverLease = active ? StoryShield.ServerLeaseFrames : 0;

        //==================== 免伤：只拦生物来源，PvP 与自定义来源放行 ====================

        public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
            => Active && StoryShield.IsCreatureSource(damageSource);

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !Active;

        public override bool CanBeHitByProjectile(Projectile proj) => !(Active && proj.hostile);

        /// <summary>免伤语义覆盖减益 DoT：immune 类判定只挡碰撞与弹幕，中毒/灼烧走 lifeRegen</summary>
        public override void UpdateBadLifeRegen() {
            if (Active && Player.lifeRegen < 0) {
                Player.lifeRegen = 0;
            }
        }
    }

    /// <summary>自然刷怪双闸：受保护玩家周边不刷（镜像 KiameSpawnGate 的写法，按玩家生效）</summary>
    internal sealed class StoryShieldNPC : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!Suppressed(player)) {
                return;
            }
            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (Suppressed(spawnInfo.Player)) {
                pool.Clear();
            }
        }

        private static bool Suppressed(Player player)
            => player?.active == true
            && player.TryGetModPlayer(out StoryShieldPlayer shield)
            && shield.SuppressSpawns;
    }

    /// <summary>
    /// 保护心跳信道（客户端→服务器，一字节）：服务器只为发送者的连接槽位续租，
    /// 不转播、不信包内自报的槽位。心跳断了两秒租约自然到期，掉线不会留下永久停刷
    /// </summary>
    internal sealed class StoryShieldNet : CWRNetChannel
    {
        internal static void Send(bool active) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<StoryShieldNet>();
            packet.Write((byte)(active ? 1 : 0));
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净载荷再判端，保流对齐
            bool active = reader.ReadByte() != 0;
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[whoAmI];
            if (player?.active == true && player.TryGetModPlayer(out StoryShieldPlayer shield)) {
                shield.ApplyHeartbeat(active);
            }
        }
    }
}
