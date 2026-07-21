using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>规则相位，据点与反噬共用</summary>
    internal enum GhostHandPhase : byte
    {
        /// <summary>潜壁 0~179t</summary>
        InWall,
        /// <summary>破壁 180~254t</summary>
        Emerge,
        /// <summary>爬行</summary>
        Stalk,
        /// <summary>攥住拖拽</summary>
        Grip,
        /// <summary>烫退 40t</summary>
        Scorch,
        /// <summary>扑锁</summary>
        Covet,
        /// <summary>蜷缩 45t→死机</summary>
        Clutch,
        /// <summary>反噬蛰伏</summary>
        Burrowed,
        /// <summary>反噬裂纹预告 45t</summary>
        CrackTelegraph,
        /// <summary>反噬破土 8t</summary>
        Erupt,
        /// <summary>反噬攥主拖拽</summary>
        EruptGrip,
        /// <summary>反噬扑空暴露 30t</summary>
        Exposed,
    }

    /// <summary>
    /// 焦黑枯手实体。相位/目标/判死/锁=服；操控压制=受害者本端；音景本地
    /// </summary>
    internal sealed class GhostHandActor : WraithActor, HackTimes.Targets.IWraithHoverConcealed
    {
        //====调参位（§9.2 首测速查）====
        private const int InWallTicks = 180;
        private const float StalkSpeedBase = 2.2f;
        private const float StalkSpeedCap = 3.4f;
        private const float StalkAccelPerMinute = 0.02f;   //每持续爬行 60t 提速
        private const float StalkGravity = 0.22f;
        private const float StalkMaxFall = 8f;
        private const float StalkSeekRange = 1200f;
        private const int StarveTicks = 600;               //目标断供收束
        private const int GazeLingerTicks = 15;            //断视迟滞,防眨眼级抖动
        private const float DragSpeed = 1.6f;
        private const float KillRadius = 48f;              //拖入裂隙判死半径
        private const float GripBreakRange = 260f;         //传送/回城逃逸断链
        private const int GripStagnationTicks = 240;       //拖拽无净进展松手(地形卡死/钉住耗尽耐心)
        private const int ReGrabImmunityTicks = 90;
        private const float ScorchRange = 96f;
        private const int ScorchStunTicks = 40;
        private const float ScorchKnockback = 9f;
        private const float ScorchDecel = 0.86f;
        private const float CovetWorldRange = 400f;        //Stalk 扑物判距(世界锁)
        private const float CovetHolderRange = 140f;       //持锁者判距(两态同值)
        private const float CovetGripWorldRange = 120f;    //Grip 收窄判距,防拖拽路过被动触发
        private const float CovetSpeed = 3.6f;             //全状态机最快=对比即渴望
        private const float CovetTouchRange = 40f;
        private const int CovetTimeoutTicks = 300;
        private const int ClutchTicks = 45;
        private const float RetreatSpeed = 3.0f;           //窗口尽携锁缩回
        //反噬缠附循环
        private const int BurrowMinTicks = 240;
        private const int BurrowMaxTicks = 420;
        private const int TelegraphTicks = 45;             //固定预告=危险层级常数,不许砍
        private const int TelegraphFreezeLimit = 240;      //凝视冻结累计上限,到量换点重来
        private const int EruptWindowTicks = 8;
        private const float EruptGrabRange = 90f;
        private const float EruptDragSpeed = 1.4f;
        private const float EruptKillRadius = 32f;
        private const float EruptWallSearchRange = 220f;
        private const int EruptDragTimeoutTicks = 300;
        private const int ExposedTicks = 30;
        private const float CrackSearchRange = 120f;

        //====同步字段（权威写，各端读）====
        [SyncVar]
        private byte phaseRaw = (byte)GhostHandPhase.InWall;
        [SyncVar]
        private int victimWho = -1;
        [SyncVar]
        private bool scorchSpent;
        [SyncVar]
        private int covetItemWho = -1;
        [SyncVar]
        private int covetHolderWho = -1;
        [SyncVar]
        private Vector2 dragIntent;
        [SyncVar]
        private Vector2 crackPoint;
        //预告被凝视冻结
        [SyncVar]
        private bool telegraphHeld;
        //预告世代，重选爆点自增
        [SyncVar]
        private byte telegraphRepick;

        //====权威侧状态====
        private int phaseTimer;
        private int burrowDuration;
        private int gazeLinger;
        private float crawlBonus;
        private int crawlAccelTimer;
        private int starveTimer;
        private Vector2 dragTargetPos;
        private float bestDragDist;
        private int stagnationTimer;
        private int omenElapsed;
        private int omenSentTicks;
        private int telegraphFrozen;
        private Point eruptWallTile;

        //====本端演出状态====
        private GhostHandPhase lastSeenPhase = GhostHandPhase.InWall;
        private int localPhaseTimer;
        private int telegraphAnim;
        private byte lastSeenRepick;
        private float crawlAnim;
        private int scratchTimer;
        private int dragSoundTimer;
        private int spentHintTimer;
        private int visualFacing = 1;
        private int emergeDir;

        /// <summary>当前规则相位</summary>
        public GhostHandPhase Phase => (GhostHandPhase)phaseRaw;

        /// <summary>被攥玩家 whoAmI，-1=无</summary>
        public int VictimWho => victimWho;

        /// <summary>本帧拖拽意图</summary>
        public Vector2 DragIntent => dragIntent;

        /// <summary>持锁玩家 whoAmI，-1=无</summary>
        public int CovetHolderWho => covetHolderWho;

        /// <summary>正处攥握相位</summary>
        public bool IsGripping => Phase is GhostHandPhase.Grip or GhostHandPhase.EruptGrip;

        /// <summary>本端相位内计时</summary>
        public int LocalPhaseTimer => localPhaseTimer;

        /// <summary>潜壁隐身，扫描器不受理</summary>
        public bool HoverConcealed => Phase == GhostHandPhase.InWall;

        public override void OnSpawn(params object[] args) {
            base.OnSpawn(args);
            phaseRaw = (byte)GhostHandPhase.InWall;
            victimWho = -1;
            scorchSpent = false;
            covetItemWho = -1;
            covetHolderWho = -1;
            dragIntent = Vector2.Zero;
            phaseTimer = 0;
            gazeLinger = 0;
            crawlBonus = 0f;
            crawlAccelTimer = 0;
            starveTimer = 0;
            stagnationTimer = 0;
            telegraphFrozen = 0;
            lastSeenPhase = Phase;
            localPhaseTimer = 0;
            crawlAnim = 0f;
            scratchTimer = 0;
            emergeDir = 0;
        }

        public override void SendExtraData(BinaryWriter writer) {
            base.SendExtraData(writer);
            writer.Write(phaseTimer);
            writer.Write(crawlBonus);
        }

        public override void ReceiveExtraData(BinaryReader reader) {
            base.ReceiveExtraData(reader);
            phaseTimer = reader.ReadInt32();
            crawlBonus = reader.ReadSingle();
            //晚加入对齐相位计时
            lastSeenPhase = Phase;
            localPhaseTimer = phaseTimer;
        }

        public override void AI() {
            base.AI();
            if (!Main.dedServ) {
                UpdateLocalPresentation();
            }
        }

        //====相位机（权威端）====

        private void SetPhase(GhostHandPhase next) {
            phaseRaw = (byte)next;
            phaseTimer = 0;
            NetUpdate = true;
        }

        protected override void OnFullyMaterialized() {
            if (!IsEscaped) {
                SetPhase(GhostHandPhase.Stalk);
            }
        }

        protected override void OnBacklashEscape(Player owner) => EnterBurrow();

        protected override void OnHaltBegin() {
            //死机入场兜底:任何残余攥握态就地清算(常规路径蜷缩前已松手)
            ReleaseVictim(grantImmunity: true);
            Velocity = Vector2.Zero;
        }

        protected override void OnHaltExpired() {
            if (IsEscaped) {
                base.OnHaltExpired();
                return;
            }
            //携锁缩回:面向锚点退行漂移并即刻消散;锁已消耗不返还,据点冷却由调度器落账
            Vector2 anchor = ResolveSiteAnchor();
            Velocity = (anchor - Center).SafeNormalize(Vector2.Zero) * RetreatSpeed;
            BeginDematerialize();
        }

        protected override void OnBeginDematerialize() => ReleaseVictim(grantImmunity: true);

        protected override void OnAuthorityUpdate() {
            if (IsEscaped) {
                //反噬循环由 UpdateEscaped 驱动
                return;
            }
            switch (Presence) {
                case WraithPresence.Materializing:
                    UpdateMaterializing();
                    break;
                case WraithPresence.Present:
                    UpdatePresent();
                    break;
            }
        }

        private void UpdateMaterializing() {
            Velocity = Vector2.Zero;
            phaseTimer++;
            if (phaseTimer >= InWallTicks && Phase == GhostHandPhase.InWall) {
                SetPhase(GhostHandPhase.Emerge);
                GhostHandSite.ThrowLockOnEmerge(this);
            }
        }

        private void UpdatePresent() {
            phaseTimer++;
            switch (Phase) {
                case GhostHandPhase.Stalk:
                    UpdateStalk();
                    break;
                case GhostHandPhase.Grip:
                    UpdateGrip(DragSpeed, KillRadius, escapedMode: false);
                    break;
                case GhostHandPhase.Scorch:
                    UpdateScorch();
                    break;
                case GhostHandPhase.Covet:
                    UpdateCovet();
                    break;
                case GhostHandPhase.Clutch:
                    UpdateClutch();
                    break;
                default:
                    //显形晚拍竞态兜底
                    SetPhase(GhostHandPhase.Stalk);
                    break;
            }
        }

        private void UpdateStalk() {
            //触到玩家=攥住(逐帧重叠轮询,免抓期内的贴身重叠不重新触发)
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !HitBox.Intersects(player.Hitbox)) {
                    continue;
                }
                if (player.GetModPlayer<GhostHandVictim>().GripImmune) {
                    continue;
                }
                EnterGrip(player, escapedMode: false);
                return;
            }

            if (TryEnterCovet(CovetWorldRange, CovetHolderRange, Center)) {
                return;
            }

            Player target = WraithSensors.NearestPlayer(Center, out float targetDist);
            if (target == null || targetDist > StalkSeekRange) {
                //目标断供:悬置计时,期满收束消散(事件自然落幕)
                if (++starveTimer >= StarveTicks) {
                    BeginDematerialize();
                    return;
                }
                Velocity *= 0.9f;
                return;
            }
            starveTimer = 0;

            //核心规则:被任意存活玩家凝视=真停(速度清零+步态冻结),断视 15t 迟滞恢复
            if (AnyAliveGaze()) {
                gazeLinger = GazeLingerTicks;
            }
            else if (gazeLinger > 0) {
                gazeLinger--;
            }
            if (gazeLinger > 0) {
                Velocity = Vector2.Zero;
                return;
            }

            //持续爬行提速:烫退后清零(施压升级+波谷复位)
            if (++crawlAccelTimer >= 60) {
                crawlAccelTimer = 0;
                crawlBonus = MathF.Min(crawlBonus + StalkAccelPerMinute, StalkSpeedCap - StalkSpeedBase);
            }
            float speed = StalkSpeedBase + crawlBonus;
            int dir = Math.Sign(target.Center.X - Center.X);
            if (dir == 0) {
                dir = 1;
            }

            Vector2 desired = Velocity;
            desired.X = dir * speed;
            desired.Y = MathF.Min(desired.Y + StalkGravity, StalkMaxFall);
            bool fallThrough = target.Center.Y > Center.Y + 40f;
            Vector2 moved = Collision.TileCollision(Position, desired, Width, Height, fallThrough, fallThrough);
            //一格台阶助攀:水平被挡且抬升一格后可走,直接翻上去(它是爬的)
            if (MathF.Abs(moved.X) < 0.05f && MathF.Abs(desired.X) > 0.05f && moved.Y >= -0.01f) {
                Vector2 lifted = Collision.TileCollision(Position - new Vector2(0f, 18f), desired, Width, Height, fallThrough, fallThrough);
                if (MathF.Abs(lifted.X) > 0.05f) {
                    Position -= new Vector2(0f, 18f);
                    moved = lifted;
                }
            }
            Velocity = moved;
        }

        private void EnterGrip(Player victim, bool escapedMode) {
            victimWho = victim.whoAmI;
            dragTargetPos = escapedMode ? eruptWallTile.ToWorldCoordinates() : ResolveSiteAnchor();
            bestDragDist = Vector2.Distance(victim.Center, dragTargetPos);
            stagnationTimer = 0;
            float speed = escapedMode ? EruptDragSpeed : DragSpeed;
            omenSentTicks = (int)MathF.Ceiling(bestDragDist / speed) + 30;
            omenElapsed = 0;
            StartOmenMirrorFor(victim, omenSentTicks);
            SetPhase(escapedMode ? GhostHandPhase.EruptGrip : GhostHandPhase.Grip);
        }

        private void UpdateGrip(float dragSpeed, float killRadius, bool escapedMode) {
            Player victim = victimWho >= 0 && victimWho < Main.maxPlayers ? Main.player[victimWho] : null;
            if (victim == null || !victim.active) {
                //强退/失联:松手回巡,无卡死
                ReleaseVictim(grantImmunity: false);
                ExitToNeutral(escapedMode);
                return;
            }
            if (victim.dead) {
                ReleaseVictim(grantImmunity: false);
                if (escapedMode) {
                    ExitToNeutral(true);
                }
                else {
                    //事件落幕(依鬼律 7)
                    BeginDematerialize();
                }
                return;
            }
            //传送/回城逃逸断链
            if (Vector2.Distance(victim.Center, Center) > GripBreakRange) {
                ReleaseVictim(grantImmunity: true);
                ExitToNeutral(escapedMode);
                return;
            }

            //火的裁定:一场只肯被烫一次(反噬模式每次扑攥独立,由 Erupt 入场重置)
            if (!scorchSpent && TryFindFlameBearer(out Vector2 firePos)) {
                scorchSpent = true;
                ReleaseVictim(grantImmunity: true);
                Velocity = (Center - firePos).SafeNormalize(-Vector2.UnitX) * ScorchKnockback;
                SetPhase(GhostHandPhase.Scorch);
                NetUpdate = true;
                return;
            }

            //喂锁自救:攥握期判距收窄且以受害者位置为准
            if (TryEnterCovet(CovetGripWorldRange, CovetHolderRange, victim.Center)) {
                return;
            }

            //拖拽意图:任意非受害者存活玩家凝视本体=本帧钉住(多人救援节拍,落刀是位置判定,钉住即续命)
            Vector2 toTarget = dragTargetPos - victim.Center;
            float dist = toTarget.Length();
            dragIntent = AnyNonVictimGaze() ? Vector2.Zero : toTarget.SafeNormalize(Vector2.Zero) * dragSpeed;

            //位置判死:唯一落刀(Omen 纯演出,不绑死)
            if (dist <= killRadius) {
                //反噬壁点被挖穿=挣脱成功,不在空气里判死
                if (escapedMode && !WorldGen.SolidTile(eruptWallTile.X, eruptWallTile.Y)) {
                    ReleaseVictim(grantImmunity: true);
                    ExitToNeutral(true);
                    return;
                }
                WraithLethality.Kill(victim, Definition);
                ReleaseVictim(grantImmunity: false);
                if (escapedMode) {
                    ExitToNeutral(true);
                }
                else {
                    BeginDematerialize();
                }
                return;
            }

            //保证退出:净进展停滞(地形卡死/凝视钉死)耗尽耐心;反噬版按总时长断
            if (dist < bestDragDist - 1f) {
                bestDragDist = dist;
                stagnationTimer = 0;
            }
            else {
                stagnationTimer++;
            }
            bool patienceOut = escapedMode ? phaseTimer >= EruptDragTimeoutTicks : stagnationTimer >= GripStagnationTicks;
            if (patienceOut) {
                ReleaseVictim(grantImmunity: true);
                ExitToNeutral(escapedMode);
                return;
            }

            //预警拍到期而人未死(被钉住/挣扎),按剩余路程续拍(纯演出,心跳不断档)
            if (++omenElapsed >= omenSentTicks) {
                omenSentTicks = (int)MathF.Ceiling(dist / dragSpeed) + 30;
                omenElapsed = 0;
                StartOmenMirrorFor(victim, omenSentTicks);
            }

            //钳附受害者躯干:凝视/烫退/喂锁判距全以受害者位置为准
            Position = victim.Center + new Vector2(0f, -6f) - Size * 0.5f;
            Velocity = Vector2.Zero;
        }

        private void UpdateScorch() {
            Velocity *= ScorchDecel;
            if (phaseTimer >= ScorchStunTicks) {
                crawlBonus = 0f;
                crawlAccelTimer = 0;
                SetPhase(GhostHandPhase.Stalk);
            }
        }

        private bool TryEnterCovet(float worldRange, float holderRange, Vector2 aroundPos) {
            int lockType = ModContent.ItemType<CharredLock>();
            int bestItem = -1;
            float bestItemSq = worldRange * worldRange;
            foreach (Item item in Main.ActiveItems) {
                if (item.type != lockType || item.stack <= 0) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(item.Center, aroundPos);
                if (distSq < bestItemSq) {
                    bestItemSq = distSq;
                    bestItem = item.whoAmI;
                }
            }
            int bestHolder = -1;
            float bestHolderSq = holderRange * holderRange;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !HasCharredLock(player)) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(player.Center, aroundPos);
                if (distSq < bestHolderSq) {
                    bestHolderSq = distSq;
                    bestHolder = player.whoAmI;
                }
            }
            if (bestItem < 0 && bestHolder < 0) {
                return false;
            }
            //两路都在:扑更近的那个
            if (bestItem >= 0 && bestHolder >= 0) {
                if (bestItemSq <= bestHolderSq) {
                    bestHolder = -1;
                }
                else {
                    bestItem = -1;
                }
            }
            ReleaseVictim(grantImmunity: true);
            covetItemWho = bestItem;
            covetHolderWho = bestHolder;
            SetPhase(GhostHandPhase.Covet);
            return true;
        }

        private void UpdateCovet() {
            Vector2? lockPos = ResolveCovetPos();
            if (lockPos == null || phaseTimer >= CovetTimeoutTicks) {
                //锁被捡走/物理卡死:执念扑空,回爬行
                covetItemWho = -1;
                covetHolderWho = -1;
                ExitToNeutral(IsEscaped);
                return;
            }
            //无视凝视:执念压过规则(相位跃变的可读信号)
            if (Vector2.Distance(lockPos.Value, Center) <= CovetTouchRange) {
                ConsumeLock();
                Velocity = Vector2.Zero;
                SetPhase(GhostHandPhase.Clutch);
                return;
            }
            Vector2 desired = (lockPos.Value - Center).SafeNormalize(Vector2.Zero) * CovetSpeed;
            Velocity = Collision.TileCollision(Position, desired, Width, Height, true, true);
        }

        private void UpdateClutch() {
            Velocity = Vector2.Zero;
            if (phaseTimer >= ClutchTicks) {
                covetItemWho = -1;
                covetHolderWho = -1;
                BeginHalt();
            }
        }

        //====反噬缠附循环（据点制唯一合法例外，依鬼律 5/11）====

        protected override void UpdateEscaped() {
            Player owner = EscapedOwnerPlayer;
            if (owner == null || owner.dead) {
                ReleaseVictim(grantImmunity: false);
                BeginDematerialize();
                return;
            }
            phaseTimer++;
            switch (Phase) {
                case GhostHandPhase.Burrowed: {
                    //贴随原主:拉拽保底 + 地下缓漂
                    float dist = Vector2.Distance(owner.Center, Center);
                    if (dist > 520f) {
                        Vector2 pull = (owner.Center - Center).SafeNormalize(Vector2.Zero) * MathF.Min((dist - 420f) * 0.03f, 8f);
                        Velocity = Vector2.Lerp(Velocity, pull, 0.10f);
                    }
                    else {
                        Vector2 drift = (owner.Center + new Vector2(0f, 90f) - Center).SafeNormalize(Vector2.Zero) * 1.1f;
                        Velocity = Vector2.Lerp(Velocity, drift, 0.04f);
                    }
                    if (phaseTimer >= burrowDuration) {
                        EnterCrackTelegraph(owner);
                    }
                    break;
                }
                case GhostHandPhase.CrackTelegraph: {
                    //预告期本体潜到爆点边上(绘制外扩范围内),倒计时被凝视冻结
                    Velocity = (crackPoint - Center).SafeNormalize(Vector2.Zero) * MathF.Min(Vector2.Distance(crackPoint, Center) * 0.08f, 6f);
                    telegraphHeld = AnyAliveGazeAtPoint(crackPoint);
                    if (telegraphHeld) {
                        phaseTimer--;
                        if (++telegraphFrozen >= TelegraphFreezeLimit) {
                            //被盯穿了:换点重来
                            EnterCrackTelegraph(owner);
                        }
                        break;
                    }
                    if (phaseTimer >= TelegraphTicks) {
                        telegraphHeld = false;
                        SetPhase(GhostHandPhase.Erupt);
                        Position = crackPoint - Size * 0.5f;
                        Velocity = Vector2.Zero;
                        //烫退每次扑攥独立可用
                        scorchSpent = false;
                    }
                    break;
                }
                case GhostHandPhase.Erupt: {
                    Position = crackPoint - Size * 0.5f;
                    Velocity = Vector2.Zero;
                    if (Vector2.Distance(owner.Center, crackPoint) <= EruptGrabRange
                        && !owner.GetModPlayer<GhostHandVictim>().GripImmune
                        && TryPickWallTile(owner.Center, out eruptWallTile)) {
                        EnterGrip(owner, escapedMode: true);
                        break;
                    }
                    if (phaseTimer >= EruptWindowTicks) {
                        SetPhase(GhostHandPhase.Exposed);
                    }
                    break;
                }
                case GhostHandPhase.EruptGrip:
                    UpdateGrip(EruptDragSpeed, EruptKillRadius, escapedMode: true);
                    break;
                case GhostHandPhase.Scorch:
                    //反噬版烫退:硬刹僵直后直接回蛰(下次扑攥烫退重新可用)
                    Velocity *= ScorchDecel;
                    if (phaseTimer >= ScorchStunTicks) {
                        EnterBurrow();
                    }
                    break;
                case GhostHandPhase.Covet:
                    UpdateCovet();
                    break;
                case GhostHandPhase.Clutch:
                    UpdateClutch();
                    break;
                case GhostHandPhase.Exposed: {
                    Velocity *= 0.8f;
                    if (TryEnterCovet(CovetWorldRange, CovetHolderRange, Center)) {
                        break;
                    }
                    if (phaseTimer >= ExposedTicks) {
                        EnterBurrow();
                    }
                    break;
                }
                default:
                    //挣脱体不走据点相位,任何异态收回蛰伏
                    EnterBurrow();
                    break;
            }
        }

        private void EnterBurrow() {
            burrowDuration = Main.rand.Next(BurrowMinTicks, BurrowMaxTicks + 1);
            SetPhase(GhostHandPhase.Burrowed);
        }

        private void EnterCrackTelegraph(Player owner) {
            telegraphFrozen = 0;
            //换点重来(§2.6 公平阀门):惩罚现用爆点,备选中随机取近点;确无备选才允许回退原点
            if (!TryPickCrackPoint(owner, crackPoint, out Vector2 point)) {
                //周身无固体面(深渊坠落级场景):回蛰再等
                EnterBurrow();
                return;
            }
            crackPoint = point;
            //世代自增:同相位重设也让各端裂纹"消失重长"必然可见
            unchecked {
                telegraphRepick++;
            }
            SetPhase(GhostHandPhase.CrackTelegraph);
        }

        /// <summary>被盯穿换点时对原爆点的排除半径（像素）</summary>
        private const float CrackRepickExclusion = 32f;
        /// <summary>候选面与最近面的并列容差（像素），并列集内随机取点=方位偏置</summary>
        private const float CrackNearTieSlack = 40f;

        /// <summary>
        /// 原主 120px 内固体面选点，排除 exclude 近旁，
        /// 距离并列集内随机取一（重选不总回同一面）；排除后无候选再放开原点
        /// </summary>
        private static bool TryPickCrackPoint(Player owner, Vector2 exclude, out Vector2 point) {
            point = default;
            List<Vector2> faces = CollectCrackFaces(owner);
            if (faces.Count == 0) {
                return false;
            }
            //换点惩罚:剔掉原爆点近旁的面;剔空则允许原点回退
            List<Vector2> pool = faces;
            if (faces.Count > 1) {
                List<Vector2> filtered = [];
                foreach (Vector2 face in faces) {
                    if (Vector2.DistanceSquared(face, exclude) > CrackRepickExclusion * CrackRepickExclusion) {
                        filtered.Add(face);
                    }
                }
                if (filtered.Count > 0) {
                    pool = filtered;
                }
            }
            //最近距离的并列集内随机:近身威胁不变,方位不可预判
            float bestSq = float.MaxValue;
            foreach (Vector2 face in pool) {
                bestSq = MathF.Min(bestSq, Vector2.DistanceSquared(face, owner.Center));
            }
            float tieLimit = MathF.Sqrt(bestSq) + CrackNearTieSlack;
            float tieLimitSq = tieLimit * tieLimit;
            int picked = -1;
            int seen = 0;
            for (int i = 0; i < pool.Count; i++) {
                if (Vector2.DistanceSquared(pool[i], owner.Center) > tieLimitSq) {
                    continue;
                }
                //蓄水池抽样,免建第二张表
                seen++;
                if (Main.rand.NextBool(seen)) {
                    picked = i;
                }
            }
            if (picked < 0) {
                return false;
            }
            point = pool[picked];
            return true;
        }

        /// <summary>收集原主 120px 内全部固体面（固体瓦贴邻空气的面中点）</summary>
        private static List<Vector2> CollectCrackFaces(Player owner) {
            List<Vector2> faces = [];
            Point center = owner.Center.ToTileCoordinates();
            int radius = (int)(CrackSearchRange / 16f) + 1;
            float rangeSq = CrackSearchRange * CrackSearchRange;
            for (int x = center.X - radius; x <= center.X + radius; x++) {
                for (int y = center.Y - radius; y <= center.Y + radius; y++) {
                    if (!WorldGen.InWorld(x, y, 40) || !WorldGen.SolidTile(x, y)) {
                        continue;
                    }
                    //面=固体瓦的任一空气邻侧
                    Vector2 face;
                    if (!WorldGen.SolidTile(x, y - 1)) {
                        face = new Vector2(x * 16f + 8f, y * 16f);
                    }
                    else if (!WorldGen.SolidTile(x, y + 1)) {
                        face = new Vector2(x * 16f + 8f, y * 16f + 16f);
                    }
                    else if (!WorldGen.SolidTile(x - 1, y)) {
                        face = new Vector2(x * 16f, y * 16f + 8f);
                    }
                    else if (!WorldGen.SolidTile(x + 1, y)) {
                        face = new Vector2(x * 16f + 16f, y * 16f + 8f);
                    }
                    else {
                        continue;
                    }
                    if (Vector2.DistanceSquared(face, owner.Center) <= rangeSq) {
                        faces.Add(face);
                    }
                }
            }
            return faces;
        }

        /// <summary>攥点 220px 内最近固体壁瓦（拖拽终点，判死时复验其仍为固体）</summary>
        private static bool TryPickWallTile(Vector2 from, out Point tile) {
            tile = default;
            Point center = from.ToTileCoordinates();
            int radius = (int)(EruptWallSearchRange / 16f) + 1;
            float bestSq = EruptWallSearchRange * EruptWallSearchRange;
            bool found = false;
            for (int x = center.X - radius; x <= center.X + radius; x++) {
                for (int y = center.Y - radius; y <= center.Y + radius; y++) {
                    if (!WorldGen.InWorld(x, y, 40) || !WorldGen.SolidTile(x, y)) {
                        continue;
                    }
                    float distSq = Vector2.DistanceSquared(new Vector2(x * 16f + 8f, y * 16f + 8f), from);
                    if (distSq < bestSq) {
                        bestSq = distSq;
                        tile = new Point(x, y);
                        found = true;
                    }
                }
            }
            return found;
        }

        //====公用判定====

        private bool AnyAliveGaze() {
            WraithDefinition def = Definition;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && WraithSensors.IsGazedBy(player, this, def.GazeRange)) {
                    return true;
                }
            }
            return false;
        }

        private bool AnyNonVictimGaze() {
            WraithDefinition def = Definition;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.whoAmI == victimWho) {
                    continue;
                }
                if (WraithSensors.IsGazedBy(player, this, def.GazeRange)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>对世界点凝视判定，同 IsGazedBy</summary>
        private bool AnyAliveGazeAtPoint(Vector2 point) {
            float range = Definition.GazeRange;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                Vector2 toPoint = point - player.Center;
                if (toPoint.LengthSquared() > range * range) {
                    continue;
                }
                if (MathF.Abs(toPoint.X) > 40f && Math.Sign(toPoint.X) != player.direction) {
                    continue;
                }
                if (Collision.CanHitLine(player.Center, 1, 1, point, 1, 1)) {
                    return true;
                }
            }
            return false;
        }

        private bool TryFindFlameBearer(out Vector2 firePos) {
            firePos = default;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.HeldItem == null || player.HeldItem.IsAir || !player.HeldItem.flame) {
                    continue;
                }
                if (Vector2.DistanceSquared(player.Center, Center) <= ScorchRange * ScorchRange) {
                    firePos = player.Center;
                    return true;
                }
            }
            return false;
        }

        internal static bool HasCharredLock(Player player) {
            int lockType = ModContent.ItemType<CharredLock>();
            foreach (Item item in player.inventory) {
                if (item != null && !item.IsAir && item.type == lockType && item.stack > 0) {
                    return true;
                }
            }
            return false;
        }

        private Vector2? ResolveCovetPos() {
            if (covetItemWho >= 0 && covetItemWho < Main.maxItems) {
                Item item = Main.item[covetItemWho];
                if (item.active && item.type == ModContent.ItemType<CharredLock>() && item.stack > 0) {
                    return item.Center;
                }
            }
            if (covetHolderWho >= 0 && covetHolderWho < Main.maxPlayers) {
                Player holder = Main.player[covetHolderWho];
                if (holder != null && holder.active && !holder.dead && HasCharredLock(holder)) {
                    return holder.Center;
                }
            }
            return null;
        }

        /// <summary>
        /// 锁消耗，服权威；持有者手中的锁由持有者本端
        /// 观测到 Clutch 相位后自行扣减（<see cref="GhostHandVictim"/>，玩家背包客户端权威）
        /// </summary>
        private void ConsumeLock() {
            if (covetItemWho >= 0 && covetItemWho < Main.maxItems) {
                Item item = Main.item[covetItemWho];
                if (item.active && item.type == ModContent.ItemType<CharredLock>()) {
                    item.TurnToAir();
                    item.active = false;
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, covetItemWho);
                    }
                }
                covetItemWho = -1;
            }
            //covetHolderWho 保留到 Clutch 结束:它就是持有者本端的扣锁信号
        }

        private void ExitToNeutral(bool escapedMode) {
            if (escapedMode) {
                EnterBurrow();
            }
            else {
                SetPhase(GhostHandPhase.Stalk);
            }
        }

        private void ReleaseVictim(bool grantImmunity) {
            if (victimWho < 0) {
                dragIntent = Vector2.Zero;
                return;
            }
            Player victim = victimWho < Main.maxPlayers ? Main.player[victimWho] : null;
            if (victim != null && victim.active) {
                if (grantImmunity) {
                    victim.GetModPlayer<GhostHandVictim>().GrantGripImmunity(ReGrabImmunityTicks);
                }
                CancelOmenMirrorFor(victim);
            }
            victimWho = -1;
            dragIntent = Vector2.Zero;
        }

        private Vector2 ResolveSiteAnchor() {
            if (WraithSiteSystem.TryGet(Definition.Key, out WraithSiteRecord record) && record.Anchored) {
                return record.Anchor;
            }
            return SpawnAnchor;
        }

        //====预警拍镜像====

        private void StartOmenMirrorFor(Player victim, int ticks) {
            if (VaultUtils.isServer) {
                WraithNet.SendOmenStart(victim.whoAmI, Definition, ticks);
            }
            else if (victim.whoAmI == Main.myPlayer) {
                victim.GetModPlayer<WraithPlayer>().BeginOmenMirror(Definition, ticks);
            }
        }

        private void CancelOmenMirrorFor(Player victim) {
            if (VaultUtils.isServer) {
                WraithNet.SendOmenCancel(victim.whoAmI);
            }
            else if (victim.whoAmI == Main.myPlayer) {
                victim.GetModPlayer<WraithPlayer>().ClearOmenMirror();
            }
        }

        //====本端演出（音景/粒子/震屏，各端本地）====

        private void UpdateLocalPresentation() {
            GhostHandPhase phase = Phase;
            if (phase != lastSeenPhase) {
                PlayPhaseFlipCue(lastSeenPhase, phase);
                lastSeenPhase = phase;
                localPhaseTimer = 0;
                telegraphAnim = 0;
            }
            else {
                localPhaseTimer++;
                //裂纹生长随凝视冻结停摆:玩家看得见"盯住有效";盯穿换点经世代号重置,
                //回退原点的重选同样"消失重长"(公平阀门必然可见)
                if (phase == GhostHandPhase.CrackTelegraph) {
                    if (telegraphRepick != lastSeenRepick) {
                        lastSeenRepick = telegraphRepick;
                        telegraphAnim = 0;
                        localPhaseTimer = 0;
                    }
                    else if (!telegraphHeld) {
                        telegraphAnim++;
                    }
                }
            }

            //步态相位随实际位移推进(真停时速度为零,步态自然冻结)
            crawlAnim += MathF.Abs(Velocity.X) * 0.085f;
            if (MathF.Abs(Velocity.X) > 0.1f) {
                visualFacing = Math.Sign(Velocity.X);
            }
            else if (IsGripping && dragIntent.X != 0f) {
                visualFacing = Math.Sign(dragIntent.X);
            }

            switch (phase) {
                case GhostHandPhase.InWall:
                    UpdateInWallCue();
                    break;
                case GhostHandPhase.Stalk:
                    if (++scratchTimer >= 300) {
                        scratchTimer = 0;
                        SoundEngine.PlaySound(SoundID.Dig with { Pitch = Main.rand.NextFloat(-0.5f, -0.2f), Volume = 0.4f, MaxInstances = 2 }, Center);
                    }
                    break;
                case GhostHandPhase.Grip:
                case GhostHandPhase.EruptGrip:
                    UpdateGripCue();
                    break;
                case GhostHandPhase.Burrowed:
                    if (localPhaseTimer % 120 == 60) {
                        SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.6f, Volume = 0.2f, MaxInstances = 2 }, Center);
                    }
                    break;
                case GhostHandPhase.CrackTelegraph:
                    UpdateTelegraphCue();
                    break;
            }

            if (Presence == WraithPresence.Dematerializing && localPhaseTimer % 5 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(Center + Main.rand.NextVector2Circular(14f, 10f),
                    -Velocity.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.4f, 1.1f),
                    GhostHandDrawHelper.Charcoal * 0.6f, Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(22, 34), 0.4f);
            }
        }

        /// <summary>潜壁音景</summary>
        private void UpdateInWallCue() {
            if (localPhaseTimer == 1 || localPhaseTimer == 60 || localPhaseTimer == 130) {
                float vol = 0.25f + localPhaseTimer / 130f * 0.25f;
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.5f, Volume = vol, MaxInstances = 2 }, Center);
            }
            if (localPhaseTimer == 150) {
                SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Pitch = -0.8f, Volume = 0.3f, MaxInstances = 2 }, Center);
                ShakeNear(2f);
            }
            if (localPhaseTimer % 20 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(Center + Main.rand.NextVector2Circular(20f, 14f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.5f),
                    GhostHandDrawHelper.Charcoal * 0.45f, Main.rand.NextFloat(0.10f, 0.16f))
                    ?.Configure(Main.rand.Next(20, 32), 0.35f);
            }
        }

        /// <summary>攥拖音景</summary>
        private void UpdateGripCue() {
            if (dragIntent.LengthSquared() > 0.01f && ++dragSoundTimer >= 40) {
                dragSoundTimer = 0;
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 2 }, Center);
            }
            if (scorchSpent && ++spentHintTimer >= 90) {
                spentHintTimer = 0;
                bool fireNear = false;
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && player.HeldItem?.flame == true
                        && Vector2.DistanceSquared(player.Center, Center) <= ScorchRange * ScorchRange) {
                        fireNear = true;
                        break;
                    }
                }
                if (fireNear) {
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_CampfireSteam>(Center + Main.rand.NextVector2Circular(12f, 8f),
                            -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.2f), default, Main.rand.NextFloat(0.16f, 0.24f))
                            ?.Configure(Main.rand.Next(30, 45));
                    }
                    if (GhostHand.ScorchSpent != null) {
                        CombatText.NewText(HitBox, GhostHandDrawHelper.Ember, GhostHand.ScorchSpent.Value, true);
                    }
                }
            }
        }

        /// <summary>反噬预告音景</summary>
        private void UpdateTelegraphCue() {
            if (localPhaseTimer == 2 || localPhaseTimer == 24) {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = localPhaseTimer < 10 ? -0.2f : 0.2f, Volume = 0.5f, MaxInstances = 2 }, crackPoint);
            }
            if (localPhaseTimer % 6 == 0) {
                Vector2 from = crackPoint + Main.rand.NextVector2CircularEdge(26f, 26f);
                PRTLoader.NewParticle<PRT_Smoke>(from, (crackPoint - from) * 0.05f,
                    GhostHandDrawHelper.Charcoal * 0.55f, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(14, 22), 0.4f);
            }
        }

        /// <summary>相位翻转拍</summary>
        private void PlayPhaseFlipCue(GhostHandPhase from, GhostHandPhase to) {
            switch (to) {
                case GhostHandPhase.Emerge: {
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.3f, Volume = 0.8f, MaxInstances = 2 }, Center);
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.1f, Volume = 0.7f, MaxInstances = 2 }, Center);
                    ShakeNear(4f);
                    for (int i = 0; i < 14; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(Center + Main.rand.NextVector2Circular(18f, 14f),
                            Main.rand.NextVector2Circular(2.2f, 1.6f) - Vector2.UnitY * 0.6f,
                            GhostHandDrawHelper.Charcoal * 0.6f, Main.rand.NextFloat(0.16f, 0.28f))
                            ?.Configure(Main.rand.Next(24, 40), 0.45f);
                    }
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(Center + Main.rand.NextVector2Circular(14f, 10f),
                            new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-5f, -2f)),
                            new Color(120, 112, 104), Main.rand.NextFloat(0.7f, 1.1f))
                            ?.Configure(Main.rand.Next(26, 40));
                    }
                    break;
                }
                case GhostHandPhase.Scorch: {
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 2 }, Center);
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_CampfireSteam>(Center + Main.rand.NextVector2Circular(16f, 10f),
                            -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.8f), default, Main.rand.NextFloat(0.18f, 0.30f))
                            ?.Configure(Main.rand.Next(35, 55));
                    }
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Center, Vector2.Zero, GhostHandDrawHelper.Ember, 0.1f)
                        ?.Configure(0.1f, 0.9f, 24);
                    if (GhostHand.ScorchRelease != null) {
                        CombatText.NewText(HitBox, GhostHandDrawHelper.Ember, GhostHand.ScorchRelease.Value, true);
                    }
                    spentHintTimer = 0;
                    break;
                }
                case GhostHandPhase.Clutch: {
                    //触锁瞬间一声攥响;其后的收势拍静默不设闸门:蜷缩相位本就无任何本端音源/粒子
                    //调度(见 UpdateLocalPresentation 的相位分支),无声由相位天然成立
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 2 }, Center);
                    break;
                }
                case GhostHandPhase.Erupt: {
                    ShakeNear(3f);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(crackPoint + Main.rand.NextVector2Circular(8f, 6f),
                            new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4.5f, -1.5f)),
                            new Color(120, 112, 104), Main.rand.NextFloat(0.6f, 1f))
                            ?.Configure(Main.rand.Next(22, 34));
                    }
                    break;
                }
                case GhostHandPhase.EruptGrip:
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.85f, Pitch = -0.35f, MaxInstances = 2 }, Center);
                    break;
                case GhostHandPhase.Exposed:
                    if (from == GhostHandPhase.Erupt) {
                        //抓空差分音
                        SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, Center);
                    }
                    break;
            }
            dragSoundTimer = 0;
            scratchTimer = 0;
        }

        private void ShakeNear(float strength, float range = 1100f) {
            Player local = Main.LocalPlayer;
            if (local != null && local.active && Vector2.DistanceSquared(local.Center, Center) < range * range) {
                local.CWR()?.GetScreenShake(strength);
            }
        }

        //====绘制====

        /// <summary>破口法线 ±1</summary>
        internal int ResolveEmergeDir() {
            if (emergeDir != 0) {
                return emergeDir;
            }
            Point tile = Center.ToTileCoordinates();
            int solidLeft = 0, solidRight = 0;
            for (int dx = 1; dx <= 3; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (WorldGen.InWorld(tile.X - dx, tile.Y + dy, 40) && WorldGen.SolidTile(tile.X - dx, tile.Y + dy)) {
                        solidLeft++;
                    }
                    if (WorldGen.InWorld(tile.X + dx, tile.Y + dy, 40) && WorldGen.SolidTile(tile.X + dx, tile.Y + dy)) {
                        solidRight++;
                    }
                }
            }
            emergeDir = solidLeft >= solidRight ? 1 : -1;
            return emergeDir;
        }

        public override void DrawBody(SpriteBatch spriteBatch, Color lightColor) {
            GhostHandPhase phase = Phase;
            //潜壁不画:可见性不依赖 PresenceStrength;蛰伏/预告=0.15 幽影
            if (phase == GhostHandPhase.InWall) {
                return;
            }

            float alpha = PresenceStrength;
            float curl = 0f;
            float ember = 0.35f;
            Vector2 screenCenter = Center - Main.screenPosition;
            int facing = visualFacing;
            float crawl = crawlAnim;
            float flicker = VisualPhase;

            switch (phase) {
                case GhostHandPhase.Emerge: {
                    float p = MathHelper.Clamp(localPhaseTimer / 75f, 0f, 1f);
                    int dir = ResolveEmergeDir();
                    facing = dir;
                    //沿破口伸出:自壁内滑出 34px,自带可见度斜坡
                    screenCenter -= new Vector2(dir * (1f - p) * 34f, 0f);
                    alpha = MathF.Min(p * 2f, 1f);
                    crawl = p * 4f;
                    ember = 0.5f;
                    break;
                }
                case GhostHandPhase.Stalk:
                    //余烬裂纹随速度增亮(速度门控修饰)
                    ember = 0.3f + MathF.Min(MathF.Abs(Velocity.X) / StalkSpeedCap, 1f) * 0.6f;
                    break;
                case GhostHandPhase.Grip:
                case GhostHandPhase.EruptGrip:
                    curl = 0.72f;
                    ember = 0.85f;
                    crawl = VisualPhase * 2.4f;
                    break;
                case GhostHandPhase.Scorch: {
                    //烫退僵直:高频抖动+裂纹爆亮
                    float shudder = MathF.Sin(localPhaseTimer * 1.9f) * 2.2f * (1f - localPhaseTimer / (float)ScorchStunTicks);
                    screenCenter += new Vector2(shudder, 0f);
                    curl = 0.30f;
                    ember = 1f;
                    break;
                }
                case GhostHandPhase.Covet:
                    //扑行中指节急促开合
                    curl = 0.28f + 0.22f * MathF.Sin(localPhaseTimer * 0.55f);
                    ember = 0.9f;
                    crawl += localPhaseTimer * 0.12f;
                    break;
                case GhostHandPhase.Clutch: {
                    //五指裹锁收拢:poly(8) 急合,收拢后微颤
                    curl = GhostHandDrawHelper.CurlEase(localPhaseTimer / (float)ClutchTicks);
                    if (curl >= 0.99f) {
                        screenCenter += new Vector2(MathF.Sin(localPhaseTimer * 1.3f) * 0.8f, 0f);
                    }
                    ember = 0.6f;
                    break;
                }
                case GhostHandPhase.Burrowed:
                case GhostHandPhase.CrackTelegraph:
                    alpha = MathF.Min(alpha, 0.15f);
                    ember = 0.2f;
                    break;
                case GhostHandPhase.Erupt: {
                    float p = MathHelper.Clamp(localPhaseTimer / (float)EruptWindowTicks, 0f, 1f);
                    screenCenter += new Vector2(0f, (1f - p) * 22f);
                    curl = 0.15f;
                    ember = 1f;
                    break;
                }
                case GhostHandPhase.Exposed:
                    //指节抽搐、余烬明灭
                    curl = 0.18f + 0.1f * MathF.Sin(localPhaseTimer * 0.9f);
                    ember = 0.4f + 0.4f * MathF.Abs(MathF.Sin(localPhaseTimer * 0.23f));
                    break;
            }

            if (IsHalted) {
                //死机:蜷缩定格,窗口后半随心跳式明暗催促
                curl = 1f;
                float remain01 = HaltRemaining / MathF.Max(Definition.HaltWindowTicks, 1f);
                if (remain01 < 0.5f) {
                    float urge = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.16f);
                    ember = 0.4f + urge * 0.6f;
                    alpha *= 0.82f + 0.18f * urge;
                }
                else {
                    ember = 0.5f;
                }
                screenCenter += new Vector2(MathF.Sin((float)Main.timeForVisualEffects * 0.9f) * 0.6f, 0f);
            }

            GhostHandDrawHelper.DrawHand(spriteBatch, screenCenter, facing, crawl, curl, alpha, 1f, ember, flicker);

            //反噬裂纹预告贴饰:爆点裂缝渐显(凝视冻结期依旧可见,公平阀门)
            if (phase == GhostHandPhase.CrackTelegraph) {
                DrawCrackTelegraph(spriteBatch);
            }
        }

        /// <summary>爆点裂纹贴饰</summary>
        private void DrawCrackTelegraph(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            float progress = MathHelper.Clamp(telegraphAnim / (float)TelegraphTicks, 0f, 1f);
            Vector2 screen = crackPoint - Main.screenPosition;
            int seed = crackPoint.GetHashCode();
            int segments = 6 + (seed & 3);
            float baseAngle = ((seed >> 4 & 15) / 15f - 0.5f) * MathHelper.Pi;
            Vector2 cursor = screen;
            float angle = baseAngle;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                if (t > progress) {
                    break;
                }
                int h = seed * 31 + i * 197;
                angle += ((h & 63) / 63f - 0.5f) * 1.2f;
                float len = 5f + (h >> 6 & 7);
                Vector2 dir = angle.ToRotationVector2();
                Vector2 segCenter = cursor + dir * len * 0.5f;
                float flick = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.3f + i);
                spriteBatch.Draw(pixel, segCenter, src, Color.Black * (0.8f * progress), angle, half, new Vector2(len, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, segCenter, src, GhostHandDrawHelper.Ember * (0.5f * progress * flick), angle, half, new Vector2(len, 1.2f), SpriteEffects.None, 0f);
                cursor += dir * len;
            }
        }
    }
}
