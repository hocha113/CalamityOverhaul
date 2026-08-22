using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities.CrimsonBrides
{
    /// <summary>
    /// 绯嫁「迎亲」主动重启：四段仪式（轿至/迎入/合卺/散场）的时刻表与权威流程。<br/>
    /// 仪式开始不扣费；只有合卺帧由权威端 <see cref="WraithAbilityService.TryCommitUse(Player, string)"/>
    /// 结算成功后才执行重启，失格则化作散场。
    /// </summary>
    internal static class CrimsonBrideRestart
    {
        //四段相位边界（帧）：轿至(1..22] 迎入(22..50] 合卺(50..68] 散场(68..96]
        public const int PhaseArriveEnd = 22;
        public const int PhaseWelcomeEnd = 50;
        public const int PhaseUnionEnd = 68;
        public const int TotalFrames = 96;
        /// <summary>合卺执行帧，落在合卺段中点附近</summary>
        public const int RestoreFrame = 56;
        //迎入合拢后钉住行动；散场开帘时归还操作
        public const int FreezeStart = 40;
        public const int FreezeEnd = 70;
        //玩家被轿帘罩住的短隐窗口
        public const int HideStart = 46;
        public const int HideEnd = 70;

        internal static bool IsRiteActive(Player player)
            => player != null
                && player.TryGetModPlayer(out CrimsonBrideRitePlayer rite)
                && rite.RiteTimer > 0;

        /// <summary>本地按键入口：单机直接权威执行，多人客户端只发请求等广播。</summary>
        internal static void TryStart(Player owner) {
            if (owner == null || owner.whoAmI != Main.myPlayer || !owner.Alives()
                || IsRiteActive(owner)) {
                return;
            }
            if (!WraithAbilityService.TryResolve(owner,
                    WraithPlayer.CrimsonBrideKey, out _)) {
                PlayFailureCue(owner);
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                WraithNet.SendBrideRiteRequest();
                return;
            }
            ExecuteAuthority(owner);
        }

        /// <summary>服务器/单机权威开始迎亲；重验资格后启动并广播。</summary>
        internal static void ExecuteAuthority(Player owner) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || owner?.active != true || !owner.Alives()
                || !owner.TryGetModPlayer(out CrimsonBrideRitePlayer rite)
                || rite.RiteTimer > 0
                || !WraithAbilityService.TryResolve(owner,
                    WraithPlayer.CrimsonBrideKey, out _)) {
                return;
            }
            rite.BeginRite((byte)Main.rand.Next(256));
        }

        /// <summary>客户端应用服务器广播的仪式快照。</summary>
        internal static void ApplyReplicatedState(Player player, uint revision,
            int timer, bool restoreFired, byte seed) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player?.active != true
                || !player.TryGetModPlayer(out CrimsonBrideRitePlayer rite)) {
                return;
            }
            rite.ApplyReplicated(revision, timer, restoreFired, seed);
        }

        /// <summary>
        /// 合卺重启，对齐大比目鱼重启的机械效果：满血、清全部 Buff、短无敌。<br/>
        /// 不触碰替死复苏与倍率，与 ScapeGhost 完全正交。
        /// </summary>
        internal static void ApplyRestore(Player owner) {
            if (owner?.active != true || owner.dead) {
                return;
            }
            owner.statLife = owner.statLifeMax2;
            //倒序删除，避免 DelBuff 前移导致漏项
            for (int i = Player.MaxBuffs - 1; i >= 0; i--) {
                if (owner.buffType[i] > 0) {
                    owner.DelBuff(i);
                }
            }
            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 60);
            owner.fallStart = (int)(owner.position.Y / 16f);
        }

        /// <summary>喜堂半径：合卺帧圈进这一圈的都算被请来的客</summary>
        private const float HallRadius = 520f;

        /// <summary>
        /// 合卺帧给喜堂里的宾客系上缚印。喜堂里时间是停住的
        /// 缚在身上时，同一施加者的其他印记不走表
        /// </summary>
        internal static void BindHallGuests(Player owner) {
            if (Main.netMode == NetmodeID.MultiplayerClient || owner?.active != true) {
                return;
            }
            float revival = owner.TryGetModPlayer(out WraithPlayer wraith)
                ? wraith.GetRevival(WraithPlayer.CrimsonBrideKey) : 0f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy()
                    && Vector2.DistanceSquared(npc.Center, owner.Center)
                        <= HallRadius * HallRadius) {
                    Marks.WraithMarks.Apply(npc, Marks.WraithMark.Betrothed,
                        Marks.WraithMarks.BetrothedTicks, revival, owner.whoAmI,
                        WraithPlayer.CrimsonBrideKey);
                }
            }
        }

        private static void PlayFailureCue(Player owner) {
            if (Main.dedServ || owner?.whoAmI != Main.myPlayer) {
                return;
            }
            //资格不足只给一声闷响，不播大特效
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.9f,
                Volume = 0.18f,
            }, owner.Center);
        }
    }

    /// <summary>
    /// 迎亲仪式的每玩家状态与推进：输入路由、计时、冻结/免疫窗口与合卺权威结算。<br/>
    /// 状态放在 ModPlayer 上按玩家持有，不入存档；服务器经 <see cref="WraithNet"/> 广播快照。
    /// </summary>
    internal sealed class CrimsonBrideRitePlayer : ModPlayer
    {
        internal int RiteTimer { get; private set; }
        internal bool RestoreFired { get; private set; }
        internal uint RiteRevision { get; private set; }
        internal byte RiteSeed { get; private set; }

        internal bool RiteActive => RiteTimer > 0;
        internal bool InFreezeWindow => RiteTimer >= CrimsonBrideRestart.FreezeStart
            && RiteTimer <= CrimsonBrideRestart.FreezeEnd;
        internal bool InHideWindow => RiteTimer >= CrimsonBrideRestart.HideStart
            && RiteTimer <= CrimsonBrideRestart.HideEnd;

        internal void BeginRite(byte seed) {
            RiteTimer = 1;
            RestoreFired = false;
            RiteSeed = seed;
            RiteRevision++;
            if (RiteRevision == 0) {
                RiteRevision = 1;
            }
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendBrideRiteState(Player.whoAmI);
            }
            else {
                BrideHallRenderer.OnRiteStarted(Player, this);
            }
        }

        internal void ApplyReplicated(uint revision, int timer, bool restoreFired, byte seed) {
            //按修订号拒绝旧仪式的迟到包；同修订内按到达顺序应用
            if (RiteRevision != 0 && unchecked((int)(revision - RiteRevision)) < 0) {
                return;
            }
            bool freshRite = revision != RiteRevision;
            bool restoreJustConfirmed = restoreFired && !RestoreFired;
            RiteRevision = revision;
            RiteSeed = seed;
            RestoreFired = restoreFired;
            RiteTimer = Math.Clamp(timer, 0, CrimsonBrideRestart.TotalFrames);
            if (RiteTimer > 0 && freshRite && !Main.dedServ) {
                BrideHallRenderer.OnRiteStarted(Player, this);
            }
            if (restoreJustConfirmed && RiteTimer > 0) {
                CrimsonBrideRestart.ApplyRestore(Player);
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer
                || CWRKeySystem.Legend_Restart == null
                || !CWRKeySystem.Legend_Restart.JustPressed) {
                return;
            }
            //骇入时间内禁用；赛博领域激活时让位给赛博重启
            if (HackTime.Active || Cyberspace.Active) {
                return;
            }
            //不持鬼切或未役使绯嫁时完全不抢键
            if (!WraithAbilityService.HasAbilityChannel(Player,
                    WraithPlayer.CrimsonBrideKey)) {
                return;
            }
            CrimsonBrideRestart.TryStart(Player);
        }

        public override void PreUpdateMovement() {
            if (RiteTimer <= 0) {
                return;
            }

            //迎入前失去通道（收刀/换役鬼）则不再合卺，化作散场
            if (RiteTimer < CrimsonBrideRestart.PhaseUnionEnd && !ChannelHolds()) {
                AbortToDepart();
            }

            if (InFreezeWindow && !Player.dead) {
                //被"抬着走"：先急减速再钉死，同时封锁物品使用
                Player.velocity *= 0.5f;
                if (RiteTimer >= CrimsonBrideRestart.FreezeStart + 4) {
                    Player.velocity = Vector2.Zero;
                }
                Player.noItems = true;
                Player.noKnockback = true;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
            if (RiteTimer >= CrimsonBrideRestart.HideStart && !Player.dead) {
                //迎入合拢直到散场收尾，喜堂内不受伤
                Player.immune = true;
                if (Player.immuneTime < 2) {
                    Player.immuneTime = 2;
                }
            }

            //合卺帧：仅权威端结算；多人客户端等待服务器确认广播
            if (!RestoreFired && RiteTimer == CrimsonBrideRestart.RestoreFrame
                && Main.netMode != NetmodeID.MultiplayerClient) {
                if (WraithAbilityService.TryCommitUse(Player,
                        WraithPlayer.CrimsonBrideKey)) {
                    RestoreFired = true;
                    CrimsonBrideRestart.ApplyRestore(Player);
                    CrimsonBrideRestart.BindHallGuests(Player);
                    if (Main.netMode == NetmodeID.Server) {
                        WraithNet.SendBrideRiteState(Player.whoAmI);
                    }
                }
                else {
                    AbortToDepart();
                }
            }

            if (!Main.dedServ) {
                BrideHallRenderer.OnRiteTick(Player, this);
            }

            RiteTimer++;
            if (RiteTimer > CrimsonBrideRestart.TotalFrames) {
                EndRite(broadcast: true);
            }
        }

        /// <summary>仪式续行只要求通道成立（活着、手持鬼切、役鬼位仍是绯嫁）；资源在合卺帧才复验。</summary>
        private bool ChannelHolds()
            => !Player.dead && WraithAbilityService.HasAbilityChannel(Player,
                WraithPlayer.CrimsonBrideKey);

        /// <summary>失格中止：跳到散场起点。计时越过合卺帧后自然不再结算，
        /// 不得置 <see cref="RestoreFired"/>：那会让客户端把中止误读成合卺确认而错误满血。</summary>
        private void AbortToDepart() {
            if (RiteTimer < CrimsonBrideRestart.PhaseUnionEnd) {
                RiteTimer = CrimsonBrideRestart.PhaseUnionEnd;
            }
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendBrideRiteState(Player.whoAmI);
            }
        }

        private void EndRite(bool broadcast) {
            RiteTimer = 0;
            RestoreFired = false;
            if (broadcast && Main.netMode == NetmodeID.Server) {
                WraithNet.SendBrideRiteState(Player.whoAmI);
            }
        }

        public override void UpdateDead() {
            if (RiteTimer > 0) {
                EndRite(broadcast: true);
            }
        }

        public override void OnEnterWorld() => EndRite(broadcast: false);

        public override void PlayerDisconnect() => EndRite(broadcast: false);

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && RiteTimer > 0) {
                WraithNet.SendBrideRiteState(Player.whoAmI, toWho);
            }
        }
    }

    /// <summary>迎入合拢至散场开帘之间隐藏仪式主，进了喜堂的人不该被看见。</summary>
    internal class CrimsonBrideHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            if (!Player.TryGetModPlayer(out CrimsonBrideRitePlayer rite)
                || !rite.InHideWindow) {
                return true;
            }
            int hidden = Player.whoAmI;
            players = players.Where(p => p.whoAmI != hidden);
            return true;
        }
    }
}
