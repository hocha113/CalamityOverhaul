using InnoVault;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core
{
    /// <summary>宏观阶段，躯干 npc.ai[0]</summary>
    internal static class GolemPhase
    {
        /// <summary>刚生成，尚未初始化</summary>
        public const int Uninit = 0;
        /// <summary>祭坛启动演出</summary>
        public const int Intro = 1;
        /// <summary>一阶段，头部附着</summary>
        public const int Armed = 2;
        /// <summary>二阶段，头部分离</summary>
        public const int Sundered = 3;
        /// <summary>死亡演出</summary>
        public const int DeathShow = 4;
    }

    /// <summary>石巨人 ai[] 槽位契约</summary>
    internal static class GolemAiSlots
    {
        /// <summary>躯干 ai[0] 宏观阶段 <see cref="GolemPhase"/></summary>
        public const int BodyPhase = 0;
        /// <summary>躯干 ai[1] 战术广播（当前攻击节拍，部件表现参考）</summary>
        public const int BodyBeat = 1;
        /// <summary>躯干 ai[2] 状态机槽 <see cref="GolemStateIndex"/></summary>
        public const int BodyStateSlot = 2;

        /// <summary>躯干 Override ai[0] 大招已释放标记</summary>
        public const int OverrideUltFired = 0;
        /// <summary>躯干 Override ai[1] 锁定落点 X（陨落重压/大招）</summary>
        public const int OverrideLockX = 1;
        /// <summary>躯干 Override ai[2] 锁定落点 Y</summary>
        public const int OverrideLockY = 2;
        /// <summary>躯干 Override ai[3] 演出通用时钟（各端本地推进，仅表现）</summary>
        public const int OverrideShowClock = 3;

        /// <summary>部件 ai[1] 躯干 whoAmI</summary>
        public const int PartBodyIndex = 1;
        /// <summary>部件 ai[2] 状态机槽（拳用）</summary>
        public const int PartStateSlot = 2;

        /// <summary>拳 Override ai[0] 指令序号（变化即新指令）</summary>
        public const int FistCmdSeq = 0;
        /// <summary>拳 Override ai[1] 指令类型 <see cref="GolemFistCommand"/></summary>
        public const int FistCmdKind = 1;
        /// <summary>拳 Override ai[2] 指令目标 X</summary>
        public const int FistCmdX = 2;
        /// <summary>拳 Override ai[3] 指令目标 Y</summary>
        public const int FistCmdY = 3;
        /// <summary>拳 Override ai[4] 反弹预算</summary>
        public const int FistBounce = 4;
        /// <summary>拳 Override ai[5] 蓄力帧数</summary>
        public const int FistWindup = 5;
        /// <summary>拳 Override ai[6] 出拳速度</summary>
        public const int FistSpeed = 6;
        /// <summary>拳 Override ai[7] 横扫起点X（0=按目标镜像推算）</summary>
        public const int FistSweepStartX = 7;

        /// <summary>拳 Override ai[8] 投技抓取目标 whoAmI+1（0=无）</summary>
        public const int FistGrabTarget = 8;
        /// <summary>拳 Override ai[9] 钉压点 X</summary>
        public const int FistPinX = 9;
        /// <summary>拳 Override ai[10] 钉压点 Y</summary>
        public const int FistPinY = 10;
        /// <summary>拳 Override ai[11] 钉面类型 <see cref="GolemPinKind"/></summary>
        public const int FistPinKind = 11;
    }

    /// <summary>投技钉面类型（写拳 Override ai[11] 同步）</summary>
    internal enum GolemPinKind : int
    {
        /// <summary>无钉面（空中放弃投掷）</summary>
        None = 0,
        /// <summary>墙在玩家左侧（法线 +X）</summary>
        WallLeft = 1,
        /// <summary>墙在玩家右侧（法线 -X）</summary>
        WallRight = 2,
        /// <summary>地面钉压，研磨朝右</summary>
        FloorRight = 3,
        /// <summary>地面钉压，研磨朝左</summary>
        FloorLeft = 4,
    }

    /// <summary>拳指令类型，写入拳 Override ai</summary>
    internal enum GolemFistCommand : int
    {
        /// <summary>无</summary>
        None = 0,
        /// <summary>蓄力直拳（直线，撞墙反弹）</summary>
        StraightPunch = 1,
        /// <summary>回旋勾拳（弧线轨迹）</summary>
        HookSwing = 2,
        /// <summary>低位横扫（贴地掠过）</summary>
        LowSweep = 3,
        /// <summary>护卫环绕（大招/仪式期）</summary>
        GuardOrbit = 4,
        /// <summary>坠地崩解（死亡演出）</summary>
        DeathFall = 5,
        /// <summary>超级直拳（慢蓄力，命中即抓取投技）</summary>
        SuperPunch = 6,
    }

    /// <summary>部件存活快照</summary>
    internal readonly struct GolemLimbStatus
    {
        public readonly int HeadIndex;
        public readonly int FreeHeadIndex;
        public readonly int LeftFistIndex;
        public readonly int RightFistIndex;

        public GolemLimbStatus(int head, int freeHead, int leftFist, int rightFist) {
            HeadIndex = head;
            FreeHeadIndex = freeHead;
            LeftFistIndex = leftFist;
            RightFistIndex = rightFist;
        }

        public bool HeadAlive => HeadIndex >= 0;
        public bool FreeHeadAlive => FreeHeadIndex >= 0;
        public bool LeftFistAlive => LeftFistIndex >= 0;
        public bool RightFistAlive => RightFistIndex >= 0;
        public int FistCount => (LeftFistAlive ? 1 : 0) + (RightFistAlive ? 1 : 0);
    }

    /// <summary>跨类共享的石巨人事实查询</summary>
    internal static class GolemFacts
    {
        /// <summary>扫描全场部件，无状态查询</summary>
        public static GolemLimbStatus ScanLimbs(int bodyWhoAmI) {
            int head = -1, freeHead = -1, leftFist = -1, rightFist = -1;
            foreach (NPC n in Main.ActiveNPCs) {
                if ((int)n.ai[GolemAiSlots.PartBodyIndex] != bodyWhoAmI) {
                    continue;
                }
                switch (n.type) {
                    case NPCID.GolemHead:
                        head = n.whoAmI;
                        break;
                    case NPCID.GolemHeadFree:
                        freeHead = n.whoAmI;
                        break;
                    case NPCID.GolemFistLeft:
                        leftFist = n.whoAmI;
                        break;
                    case NPCID.GolemFistRight:
                        rightFist = n.whoAmI;
                        break;
                }
            }
            return new GolemLimbStatus(head, freeHead, leftFist, rightFist);
        }

        /// <summary>
        /// 按继承关系安全取 Override。<see cref="VaultUtils.GetOverride{T}(NPC)"/> 是精确类型索引，
        /// 用基类查（如 <see cref="GolemFistAI"/>）或 NPC 槽位被复用时会抛 KeyNotFound，此处扫描字典永不抛出
        /// </summary>
        public static T FindOverride<T>(NPC npc) where T : NPCOverride {
            if (npc == null || !npc.active
                || !npc.TryGetOverride(out Dictionary<Type, NPCOverride> values) || values == null) {
                return null;
            }
            foreach (NPCOverride value in values.Values) {
                if (value is T match) {
                    return match;
                }
            }
            return null;
        }

        /// <summary>躯干是否有效存活</summary>
        public static bool BodyValid(NPC body) {
            return body != null && body.active && body.type == NPCID.Golem;
        }

        /// <summary>读取躯干当前同步状态索引</summary>
        public static GolemStateIndex GetStateIndex(NPC body) {
            return (GolemStateIndex)(int)body.ai[GolemAiSlots.BodyStateSlot];
        }

        /// <summary>是否处于死亡演出</summary>
        public static bool IsDeathPerformance(NPC body) {
            return body != null && body.active && body.ai[GolemAiSlots.BodyPhase] == GolemPhase.DeathShow;
        }

        /// <summary>拳锚点：肩位 + 躯干速度前馈</summary>
        public static Vector2 FistAnchor(NPC body, int side) {
            float x = side < 0 ? -84f : 78f;
            return body.Center + body.velocity + new Vector2(x * body.scale, -9f * body.scale);
        }

        /// <summary>附着头锚点</summary>
        public static Vector2 HeadAnchor(NPC body) {
            return body.Center + new Vector2(-3f * body.scale, -57f * body.scale);
        }

        /// <summary>钉面法线：由墙面/地面指向开阔侧</summary>
        public static Vector2 PinNormal(GolemPinKind kind) {
            return kind switch {
                GolemPinKind.WallLeft => Vector2.UnitX,
                GolemPinKind.WallRight => -Vector2.UnitX,
                GolemPinKind.FloorRight or GolemPinKind.FloorLeft => -Vector2.UnitY,
                _ => Vector2.Zero,
            };
        }

        /// <summary>研磨切线：墙面向下磨到底，地面沿拳向碾</summary>
        public static Vector2 GrindTangent(GolemPinKind kind) {
            return kind switch {
                GolemPinKind.WallLeft or GolemPinKind.WallRight => Vector2.UnitY,
                GolemPinKind.FloorRight => Vector2.UnitX,
                GolemPinKind.FloorLeft => -Vector2.UnitX,
                _ => Vector2.Zero,
            };
        }

        /// <summary>正处于投技抓取的拳，无则 null</summary>
        public static NPC FindGrabbingFist(GolemLimbStatus limbs) {
            NPC fist = FistInGrab(limbs.LeftFistIndex);
            return fist ?? FistInGrab(limbs.RightFistIndex);
        }

        private static NPC FistInGrab(int index) {
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC fist = Main.npc[index];
            if (!fist.active || (int)fist.ai[GolemAiSlots.PartStateSlot] != (int)GolemFistStateIndex.Grab) {
                return null;
            }
            return fist;
        }
    }
}
