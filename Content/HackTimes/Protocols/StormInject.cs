using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 雷暴注入：招来雷暴，落雷只砸身上挂着任意骇入效果的敌人。<br/>
    /// 天气写入权威端专属（F9）：直接置 <c>Main.raining / maxRaining / rainTime</c>
    /// 并广播 <see cref="MessageID.WorldData"/>；伤害与硬直由权威端结算，
    /// 落雷视觉每端自绘——选靶不发包，靠"打表序 + 种子哈希"在每个端上
    /// 确定性地选出同一只（候选集按 NPC 槽位升序收集，NPC 槽位跨端一致）。<br/>
    /// 没有任何挂效果的敌人时不落雷，协议空转是明说的设计
    /// </summary>
    internal class StormInject : QuickHackDef
    {
        private const int StrikeInterval = 45;
        //候选范围：任意在线玩家周身半径。不用"屏幕内"（每个观察者的屏幕不同，
        //选靶会不确定），也不用施术者定位（多风暴并存时 OnTick 拿不到自己的施术者）
        private const float CandidateRange = 2000f;

        //雷暴记账：世界级状态，权威端读写。startedRain 记的是"雨是我们下的"，
        //到期只收自己下的雨，不掐玩家赶上的自然雨
        private static int activeStorms;
        private static bool startedRain;

        private static readonly Color BoltCore = new(160, 230, 255);
        private static readonly List<NPC> candidateBuffer = [];
        private static readonly HashSet<int> candidateSeen = [];

        public override void SetDefaults() {
            UploadTime = 220;
            RamCost = 8;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.World;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 20;

        public override void Unload() {
            base.Unload();
            ClearLedger();
        }

        /// <summary>切世界清账：风暴计数属于上一个世界</summary>
        internal static void ClearLedger() {
            activeStorms = 0;
            startedRain = false;
            candidateBuffer.Clear();
            candidateSeen.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            return target is WorldScannable;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not WorldScannable) return false;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                activeStorms++;
                if (activeStorms == 1) {
                    startedRain = !Main.raining;
                    Main.raining = true;
                    if (Main.maxRaining < 0.85f) Main.maxRaining = 0.85f;
                    //雨量计时器压到略长于效果，OnRemove 漏跑时它自己会停
                    Main.rainTime = Math.Max(Main.rainTime, GetDuration() + 120);
                    SyncWorldWeather();
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                EmitStormOpenCue(caster?.Center ?? target.WorldCenter);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (Main.LocalPlayer?.active == true) {
                EmitStormOpenCue(Main.LocalPlayer.Center);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            //时停期间追踪器整表冻结，雨量却在原版通道里继续流失——
            //低频回填一次，效果活着雨就不停
            if (elapsed % 300 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int remaining = GetDuration() - elapsed;
                if (remaining > 0 && Main.rainTime < remaining) {
                    Main.rainTime = remaining + 120;
                }
            }

            if (elapsed <= 0 || elapsed % StrikeInterval != 0) return true;

            CollectHackedCandidates();
            if (candidateBuffer.Count == 0) return true;
            NPC victim = candidateBuffer[
                DeterministicPick(elapsed, candidateBuffer.Count)];

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int dmg = 200 + (int)(victim.lifeMax * 0.02f);
                victim.SimpleStrikeNPC(dmg, 0, false, 0f, null, false, 0f, true);
                //半秒硬直：一次性 30 帧租约
                TimeFreezeSystem.RefreshNPC<StormInject>(victim, 30);
            }
            //单人/自托管主机在这里播视觉；纯客户端走 OnReplicatatedTick 的同款调用
            if (!Main.dedServ) PlayBoltCue(victim);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            //与权威端同一节拍、同一种子、同一候选序——各端独立算出同一只。
            //进度包把 Elapsed 往前校正跨过 45 的倍数时会漏掉一记视觉，
            //伤害不受影响，纯表现层的已知取舍
            if (elapsed <= 0 || elapsed % StrikeInterval != 0) return;
            CollectHackedCandidates();
            if (candidateBuffer.Count == 0) return;
            PlayBoltCue(candidateBuffer[
                DeterministicPick(elapsed, candidateBuffer.Count)]);
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                activeStorms = Math.Max(0, activeStorms - 1);
                if (activeStorms == 0) {
                    if (startedRain) Main.StopRain();
                    startedRain = false;
                    SyncWorldWeather();
                }
            }
        }

        //雨停由 WorldData 同步，远端无需动作

        #region 选靶

        /// <summary>
        /// 收集"身上挂着任意骇入效果"的敌怪。为保证跨端确定性：
        /// 先把效果表烧进槽位标记，再按槽位升序回收——效果表的插入序
        /// 每个端不同，直接遍历它得到的候选序会不一致
        /// </summary>
        private static void CollectHackedCandidates() {
            candidateBuffer.Clear();
            candidateSeen.Clear();
            IReadOnlyList<ActiveHackEffect> effects
                = HackEffectTracker.AllActiveEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Active) continue;
                int index = effect.TargetIndex;
                if (index >= 0 && index < Main.maxNPCs) candidateSeen.Add(index);
            }
            if (candidateSeen.Count == 0) return;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (!candidateSeen.Contains(i)) continue;
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage
                    || npc.life <= 0) {
                    continue;
                }
                if (!AnyPlayerNear(npc.Center)) continue;
                candidateBuffer.Add(npc);
            }
        }

        private static bool AnyPlayerNear(Vector2 center) {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true || player.dead) continue;
                if (Vector2.DistanceSquared(player.Center, center)
                    <= CandidateRange * CandidateRange) {
                    return true;
                }
            }
            return false;
        }

        //种子只吃"第几记雷"，不碰 Main.rand——离散选靶必须每端同值
        private static int DeterministicPick(int elapsed, int count) {
            uint ordinal = (uint)(elapsed / StrikeInterval);
            uint hash = ordinal * 2654435761u ^ 0x9E3779B9u;
            return (int)(hash % (uint)count);
        }

        #endregion

        #region 表现

        //落雷：天幕到目标的一道 ThunderTrail 光柱 + 落点炸开 + 就地雷声
        private static void PlayBoltCue(NPC victim) {
            if (Main.dedServ || victim?.active != true) return;
            Vector2 to = victim.Center;
            Vector2 from = to - new Vector2(
                Main.rand.NextFloat(-120f, 120f), 900f);
            PRTLoader.NewParticle<PRT_SkyBolt>(to, Vector2.Zero, BoltCore, 1f)
                ?.Configure(from, to);

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 2.4f)
                    - new Vector2(0f, 1.6f);
                PRTLoader.NewParticle<PRT_Spark>(to, vel, BoltCore, 1f)
                    ?.Configure(true, 22);
            }
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.8f }, to);
        }

        private static void EmitStormOpenCue(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                Vector2 pos = center + new Vector2(
                    Main.rand.NextFloat(-360f, 360f),
                    Main.rand.NextFloat(-260f, -80f));
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.8f)),
                    BoltCore * 0.8f, 0.6f)?.Configure(false, 30);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Volume = 0.5f,
                    Pitch = -0.5f,
                }, center);
            }
        }

        #endregion

        private static void SyncWorldWeather() {
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }
        }
    }
}
