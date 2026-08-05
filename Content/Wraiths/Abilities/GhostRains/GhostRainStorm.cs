using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities.GhostRains
{
    /// <summary>
    /// 鬼雨「召雨」主动小域：五段相位（阴叠/落雨/雨峰/残雨/散场）的时刻表与权威流程。<br/>
    /// 阴叠不扣费；入雨帧由权威端 <see cref="WraithAbilityService.TryCommitUse(in WraithAbilityContext)"/>
    /// 结算成功后雨才真正落下，失格则阴幕化作散场。
    /// </summary>
    internal static class GhostRainStorm
    {
        //五段相位边界（帧）：阴叠(1..54] 落雨(54..144] 雨峰(144..270] 残雨(270..324] 散场(324..360]
        public const int GloomEnd = 54;
        public const int RainfallEnd = 144;
        public const int PeakEnd = 270;
        public const int LingerEnd = 324;
        public const int TotalFrames = 360;
        /// <summary>入雨结算帧，阴叠尽头</summary>
        public const int CommitFrame = GloomEnd;
        /// <summary>域水平半径（世界像素），竖直覆盖天到地</summary>
        public const float Radius = 560f;
        //雨蚀与拽入节拍（帧）
        public const int ErodeInterval = 30;
        public const int YankInterval = 50;

        internal static bool IsStormActive(Player player)
            => player != null
                && player.TryGetModPlayer(out GhostRainStormPlayer storm)
                && storm.StormTimer > 0;

        /// <summary>本地按键入口：单机直接权威执行，多人客户端只发请求等广播。</summary>
        internal static void TryStart(Player owner) {
            if (owner == null || owner.whoAmI != Main.myPlayer || !owner.Alives()
                || IsStormActive(owner)) {
                return;
            }
            if (!WraithAbilityService.TryResolve(owner,
                    WraithPlayer.GhostRainKey, out _)) {
                PlayFailureCue(owner);
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                WraithNet.SendGhostRainRiteRequest();
                return;
            }
            ExecuteAuthority(owner);
        }

        /// <summary>服务器/单机权威召雨；重验资格后启动并广播。</summary>
        internal static void ExecuteAuthority(Player owner) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || owner?.active != true || !owner.Alives()
                || !owner.TryGetModPlayer(out GhostRainStormPlayer storm)
                || storm.StormTimer > 0
                || !WraithAbilityService.TryResolve(owner,
                    WraithPlayer.GhostRainKey, out _)) {
                return;
            }
            storm.BeginStorm((byte)Main.rand.Next(256));
        }

        /// <summary>客户端应用服务器广播的风暴快照。</summary>
        internal static void ApplyReplicatedState(Player player, uint revision,
            int timer, bool paid, byte seed) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player?.active != true
                || !player.TryGetModPlayer(out GhostRainStormPlayer storm)) {
                return;
            }
            storm.ApplyReplicated(revision, timer, paid, seed);
        }

        /// <summary>阴幕在场强度包络 0~1，驱动天幕/滤镜/日光</summary>
        internal static float Envelope(int timer) {
            if (timer <= 0) {
                return 0f;
            }
            if (timer <= GloomEnd) {
                return 0.6f * timer / GloomEnd;
            }
            if (timer <= RainfallEnd) {
                return MathHelper.Lerp(0.6f, 0.85f,
                    (timer - GloomEnd) / (float)(RainfallEnd - GloomEnd));
            }
            if (timer <= PeakEnd) {
                return MathHelper.Lerp(0.85f, 1f, Math.Min(1f, (timer - RainfallEnd) / 30f));
            }
            if (timer <= LingerEnd) {
                return MathHelper.Lerp(1f, 0.7f,
                    (timer - PeakEnd) / (float)(LingerEnd - PeakEnd));
            }
            float k = (timer - LingerEnd) / (float)(TotalFrames - LingerEnd);
            return MathHelper.Clamp(0.7f * (1f - k), 0f, 1f);
        }

        /// <summary>雨密度 0~1，驱动雨滴生成率；阴叠尾梢只漏几丝前兆雨</summary>
        internal static float RainDensity(int timer) {
            if (timer <= 0) {
                return 0f;
            }
            if (timer <= GloomEnd) {
                float pre = (timer - (GloomEnd - 16)) / 16f;
                return MathHelper.Clamp(pre, 0f, 1f) * 0.12f;
            }
            if (timer <= RainfallEnd) {
                return MathHelper.Lerp(0.12f, 0.8f,
                    (timer - GloomEnd) / (float)(RainfallEnd - GloomEnd));
            }
            if (timer <= PeakEnd) {
                return 1f;
            }
            if (timer <= LingerEnd) {
                return MathHelper.Lerp(1f, 0.45f,
                    (timer - PeakEnd) / (float)(LingerEnd - PeakEnd));
            }
            float k = (timer - LingerEnd) / (float)(TotalFrames - LingerEnd);
            return MathHelper.Clamp(0.45f * (1f - k), 0f, 1f);
        }

        /// <summary>入雨确认时的雨批文字，仅风暴主的客户端显示。</summary>
        internal static void ShowRainText(Player owner) {
            if (!VaultUtils.isServer && owner?.whoAmI == Main.myPlayer) {
                CombatText.NewText(owner.Hitbox, new Color(150, 170, 175),
                    WraithSystemText.GhostRainRiteText.Value, true);
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
    /// 鬼雨风暴的每玩家状态与推进：输入路由、计时、雨蚀与拽入的权威结算。<br/>
    /// 状态放在 ModPlayer 上按玩家持有，不入存档；服务器经 <see cref="WraithNet"/> 广播快照。
    /// </summary>
    internal sealed class GhostRainStormPlayer : ModPlayer
    {
        internal int StormTimer { get; private set; }
        internal bool Paid { get; private set; }
        internal uint StormRevision { get; private set; }
        internal byte StormSeed { get; private set; }

        //入雨帧冻结的权威快照，仅服务器/单机使用
        private float masterySnapshot;
        private int weaponDamageSnapshot;

        internal bool StormActive => StormTimer > 0;

        internal void BeginStorm(byte seed) {
            StormTimer = 1;
            Paid = false;
            StormSeed = seed;
            masterySnapshot = 0f;
            weaponDamageSnapshot = 0;
            StormRevision++;
            if (StormRevision == 0) {
                StormRevision = 1;
            }
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendGhostRainStormState(Player.whoAmI);
            }
        }

        internal void ApplyReplicated(uint revision, int timer, bool paid, byte seed) {
            //按修订号拒绝旧风暴的迟到包；同修订内按到达顺序应用
            if (StormRevision != 0 && unchecked((int)(revision - StormRevision)) < 0) {
                return;
            }
            bool paidJustConfirmed = paid && !Paid;
            StormRevision = revision;
            StormSeed = seed;
            Paid = paid;
            StormTimer = Math.Clamp(timer, 0, GhostRainStorm.TotalFrames);
            if (paidJustConfirmed && StormTimer > 0) {
                GhostRainStorm.ShowRainText(Player);
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
            //不持鬼切或未役使鬼雨时完全不抢键
            if (!WraithAbilityService.HasAbilityChannel(Player,
                    WraithPlayer.GhostRainKey)) {
                return;
            }
            GhostRainStorm.TryStart(Player);
        }

        public override void PreUpdateMovement() {
            if (StormTimer <= 0) {
                return;
            }

            //散场前失去通道（收刀/换役鬼/死亡）则阴幕排干，未入雨则分文不取
            if (StormTimer < GhostRainStorm.LingerEnd && !ChannelHolds()) {
                AbortToFade();
            }

            //入雨帧：仅权威端结算；多人客户端等待服务器确认广播
            if (!Paid && StormTimer == GhostRainStorm.CommitFrame
                && Main.netMode != NetmodeID.MultiplayerClient) {
                if (WraithAbilityService.TryResolve(Player, WraithPlayer.GhostRainKey,
                        out WraithAbilityContext context)
                    && WraithAbilityService.TryCommitUse(in context)) {
                    Paid = true;
                    masterySnapshot = MathHelper.Clamp(context.Mastery, 0f, 1f);
                    weaponDamageSnapshot = Math.Max(
                        Player.GetWeaponDamage(context.VesselItem), 1);
                    GhostRainStorm.ShowRainText(Player);
                    if (Main.netMode == NetmodeID.Server) {
                        WraithNet.SendGhostRainStormState(Player.whoAmI);
                    }
                }
                else {
                    AbortToFade();
                }
            }

            if (Paid && Main.netMode != NetmodeID.MultiplayerClient) {
                UpdateAuthorityCombat();
            }

            if (!Main.dedServ) {
                GhostRainFx.OnStormTick(Player, this);
            }

            StormTimer++;
            if (StormTimer > GhostRainStorm.TotalFrames) {
                EndStorm(broadcast: true);
            }
        }

        /// <summary>雨蚀与拽入：权威端按节拍结算，全程使用入雨帧快照。</summary>
        private void UpdateAuthorityCombat() {
            int t = StormTimer;
            if (t <= GhostRainStorm.CommitFrame || t > GhostRainStorm.LingerEnd) {
                return;
            }

            //雨蚀 DoT：残雨段衰减一半
            if (t % GhostRainStorm.ErodeInterval == 0) {
                float fade = t > GhostRainStorm.PeakEnd ? 0.5f : 1f;
                int damage = Math.Max(1, (int)(weaponDamageSnapshot
                    * MathHelper.Lerp(0.10f, 0.18f, masterySnapshot) * fade));
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!IsErodable(npc)) {
                        continue;
                    }
                    int direction = npc.Center.X >= Player.Center.X ? 1 : -1;
                    Player.ApplyDamageToNPC(npc, damage, 0f, direction, false,
                        CWRRef.GetTrueMeleeDamageClass());
                }
            }

            //雨峰拽入：每拍从域内抽一个合法目标拽进雨里；Boss 与不受击退者改吃加额雨蚀
            if (t > GhostRainStorm.RainfallEnd && t <= GhostRainStorm.PeakEnd
                && t % GhostRainStorm.YankInterval == 0) {
                YankOne();
            }
        }

        private void YankOne() {
            int count = 0;
            NPC picked = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!IsErodable(npc)) {
                    continue;
                }
                //水塘抽样等概率取一
                count++;
                if (Main.rand.Next(count) == 0) {
                    picked = npc;
                }
            }
            if (picked == null) {
                return;
            }

            int bonus = Math.Max(1, (int)(weaponDamageSnapshot
                * MathHelper.Lerp(0.10f, 0.18f, masterySnapshot)));
            if (picked.boss || picked.knockBackResist <= 0f) {
                //拽不动的目标改吃双倍雨蚀
                int direction = picked.Center.X >= Player.Center.X ? 1 : -1;
                Player.ApplyDamageToNPC(picked, bonus * 2, 0f, direction, false,
                    CWRRef.GetTrueMeleeDamageClass());
            }
            else {
                //短促上抽脉冲，往域心上方带
                float toCenterX = MathHelper.Clamp(
                    (Player.Center.X - picked.Center.X) * 0.02f, -3f, 3f);
                picked.velocity = new Vector2(toCenterX, -8.5f * picked.knockBackResist - 2f);
                picked.netUpdate = true;
            }

            Vector2 throat = picked.Center - new Vector2(0f, 180f);
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendGhostRainYankFx(picked.whoAmI, throat);
            }
            else {
                GhostRainFx.TriggerYank(picked.Center, throat);
            }
        }

        private bool IsErodable(NPC npc)
            => npc.CanBeChasedBy()
                && Vector2.DistanceSquared(npc.Center, Player.Center)
                    <= GhostRainStorm.Radius * GhostRainStorm.Radius;

        /// <summary>风暴续行只要求通道成立（活着、手持鬼切、役鬼位仍是鬼雨）；资源在入雨帧才复验。</summary>
        private bool ChannelHolds()
            => !Player.dead && WraithAbilityService.HasAbilityChannel(Player,
                WraithPlayer.GhostRainKey);

        /// <summary>失格中止：跳到散场起点。计时越过入雨帧后自然不再结算，
        /// 不得置 <see cref="Paid"/>——那会让客户端把中止误读成入雨确认。</summary>
        private void AbortToFade() {
            if (StormTimer < GhostRainStorm.LingerEnd) {
                StormTimer = GhostRainStorm.LingerEnd;
            }
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendGhostRainStormState(Player.whoAmI);
            }
        }

        private void EndStorm(bool broadcast) {
            StormTimer = 0;
            Paid = false;
            masterySnapshot = 0f;
            weaponDamageSnapshot = 0;
            if (broadcast && Main.netMode == NetmodeID.Server) {
                WraithNet.SendGhostRainStormState(Player.whoAmI);
            }
        }

        public override void UpdateDead() {
            if (StormTimer > 0) {
                EndStorm(broadcast: true);
            }
        }

        public override void OnEnterWorld() => EndStorm(broadcast: false);

        public override void PlayerDisconnect() => EndStorm(broadcast: false);

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && StormTimer > 0) {
                WraithNet.SendGhostRainStormState(Player.whoAmI, toWho);
            }
        }
    }
}
