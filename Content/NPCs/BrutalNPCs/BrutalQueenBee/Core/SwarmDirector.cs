using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core
{
    /// <summary>编队类型</summary>
    internal enum SwarmFormation : int
    {
        /// <summary>默认光环护卫</summary>
        Halo = 0,
        /// <summary>箭矢楔形</summary>
        Arrow = 1,
        /// <summary>竖直蜂墙(带缝)</summary>
        Wall = 2,
        /// <summary>围笼漩涡(带旋转缺口)</summary>
        Vortex = 3,
        /// <summary>双环蜂盾</summary>
        Shield = 4,
        /// <summary>头顶伞幕</summary>
        Umbrella = 5,
        /// <summary>冲锋长矛</summary>
        Lance = 6,
        /// <summary>失控四散</summary>
        Scatter = 7,
        /// <summary>回巢吸收</summary>
        Absorb = 8,
    }

    /// <summary>
    /// 蜂群编队控制器，随女王 override 实例存在，每端各持一份<br/>
    /// 所有输入均来自同步数据(女王 ai / 玩家位置 / 编队时钟)，各端确定性推演同一阵型；<br/>
    /// 服务端周期 netUpdate 兜底纠偏，客户端不得引入本地随机
    /// </summary>
    internal class SwarmDirector
    {
        /// <summary>编队蜂 ai[3] 标记，避开灾厄的 1f 女王蜂标记</summary>
        internal const float BeeMarker = 2f;
        /// <summary>最大编队蜂数</summary>
        internal const int MaxBees = 32;
        /// <summary>公平阀：漩涡围笼缺口宽(弧度，约62°)，VortexOffset 排位时跳过该弧</summary>
        internal const float VortexGapWidth = 1.08f;
        /// <summary>公平阀：蜂墙缝高(行数)，WallOffset 排位时在 gapA/gapB 处空出</summary>
        internal const float WallGapRows = 1.7f;

        /// <summary>所属女王</summary>
        public NPC Queen;

        /// <summary>编队时钟，镜像 override.ai[0]，各端本地自增+服务端纠偏</summary>
        public float Clock;

        #region 每帧声明的编队数据
        public SwarmFormation Formation { get; private set; } = SwarmFormation.Halo;
        /// <summary>编队锚点(世界坐标)</summary>
        public Vector2 Anchor { get; private set; }
        /// <summary>编队朝向(单位向量)</summary>
        public Vector2 AimDir { get; private set; } = Vector2.UnitX;
        /// <summary>形状参数A(半径/间距倍率等)</summary>
        public float ParamA { get; private set; }
        /// <summary>形状参数B(缺口相位速度等)</summary>
        public float ParamB { get; private set; }
        /// <summary>指令信号强度 0~1，状态推高控制器衰减；驱动蜂身信号波与花粉痕(替代旧辉光带)</summary>
        public float SignalIntensity { get; private set; }
        /// <summary>就位加速倍率，阅兵式收拢用，自然衰减回1</summary>
        public float SnapBoost { get; private set; } = 1f;
        /// <summary>本帧已声明过编队</summary>
        private bool declaredThisFrame;
        #endregion

        #region 蜂群名册
        /// <summary>本队存活编队蜂，按持久槽位排序，每帧刷新</summary>
        public readonly List<NPC> Bees = [];
        //whoAmI→有效槽位
        private readonly int[] effectiveSlotByWho = new int[Main.maxNPCs];
        #endregion

        #region 掷镖指令(各端由状态代码在同一节拍发出)
        private struct DartOrder
        {
            public float IssueClock;
            public float DirRotation;
            public float Speed;
            public int SteerTime;
        }

        private readonly DartOrder[] dartOrders = new DartOrder[MaxBees];
        #endregion

        public SwarmDirector(NPC queen) {
            Queen = queen;
            Anchor = queen.Center;
            for (int i = 0; i < effectiveSlotByWho.Length; i++) {
                effectiveSlotByWho[i] = -1;
            }
        }

        #region 每帧维护(女王AI调用)

        /// <summary>帧首复位：默认光环、信号衰减、时钟自增</summary>
        public void FrameReset() {
            Clock += 1f;
            declaredThisFrame = false;
            Formation = SwarmFormation.Halo;
            Anchor = Queen.Center;
            AimDir = Queen.direction >= 0 ? Vector2.UnitX : -Vector2.UnitX;
            ParamA = 1f;
            ParamB = 0f;
            SignalIntensity *= 0.9f;
            if (SignalIntensity < 0.01f) {
                SignalIntensity = 0f;
            }
            SnapBoost = MathHelper.Lerp(SnapBoost, 1f, 0.06f);
        }

        /// <summary>状态每帧声明编队</summary>
        public void Declare(SwarmFormation formation, Vector2 anchor, Vector2 aimDir, float paramA = 1f, float paramB = 0f) {
            Formation = formation;
            Anchor = anchor;
            AimDir = aimDir.SafeNormalize(Vector2.UnitX);
            ParamA = paramA;
            ParamB = paramB;
            declaredThisFrame = true;
        }

        /// <summary>推高指令信号强度(不叠加只取峰值)</summary>
        public void PushSignal(float intensity) {
            if (intensity > SignalIntensity) {
                SignalIntensity = intensity;
            }
        }

        /// <summary>阅兵式收拢加速</summary>
        public void PushSnap(float boost) {
            if (boost > SnapBoost) {
                SnapBoost = boost;
            }
        }

        /// <summary>本帧是否有状态声明过编队(未声明时女王AI落回光环)</summary>
        public bool DeclaredThisFrame => declaredThisFrame;

        /// <summary>刷新存活蜂名册与有效槽位(全端确定性：按持久槽位+whoAmI排序)</summary>
        public void RefreshBees() {
            Bees.Clear();
            for (int i = 0; i < effectiveSlotByWho.Length; i++) {
                effectiveSlotByWho[i] = -1;
            }

            foreach (var n in Main.ActiveNPCs) {
                if (!IsFormationBeeOf(n, Queen.whoAmI)) {
                    continue;
                }
                Bees.Add(n);
            }

            Bees.Sort(static (a, b) => {
                int c = a.ai[0].CompareTo(b.ai[0]);
                return c != 0 ? c : a.whoAmI.CompareTo(b.whoAmI);
            });

            for (int i = 0; i < Bees.Count; i++) {
                effectiveSlotByWho[Bees[i].whoAmI] = i;
            }
        }

        /// <summary>是否是指定女王的编队蜂</summary>
        internal static bool IsFormationBeeOf(NPC npc, int queenWho) {
            return npc.active
                && (npc.type == NPCID.Bee || npc.type == NPCID.BeeSmall)
                && npc.ai[3] == BeeMarker
                && (int)npc.ai[2] == queenWho;
        }

        /// <summary>取有效槽位，非本队蜂返回-1</summary>
        public int GetEffectiveSlot(int whoAmI) {
            if (whoAmI < 0 || whoAmI >= effectiveSlotByWho.Length) {
                return -1;
            }
            return effectiveSlotByWho[whoAmI];
        }

        #endregion

        #region 编队槽位数学(纯函数，全端一致)

        /// <summary>取槽位目标点(世界坐标)</summary>
        public Vector2 GetSlotTarget(int slot, int count) {
            if (count <= 0) {
                return Anchor;
            }
            return Formation switch {
                SwarmFormation.Arrow => Anchor + ArrowOffset(slot),
                SwarmFormation.Wall => Anchor + WallOffset(slot, count),
                SwarmFormation.Vortex => Anchor + VortexOffset(slot, count),
                SwarmFormation.Shield => Anchor + ShieldOffset(slot, count),
                SwarmFormation.Umbrella => Anchor + UmbrellaOffset(slot, count),
                SwarmFormation.Lance => Anchor + LanceOffset(slot),
                SwarmFormation.Scatter => Anchor + ScatterOffset(slot),
                SwarmFormation.Absorb => Anchor,
                _ => Anchor + HaloOffset(slot, count),
            };
        }

        private Vector2 HaloOffset(int slot, int count) {
            float angle = MathHelper.TwoPi * slot / count + Clock * 0.017f;
            float bob = (float)Math.Sin(Clock * 0.11f + slot * 2.37f) * 7f;
            return new Vector2(
                (float)Math.Cos(angle) * 156f * ParamA,
                (float)Math.Sin(angle) * 98f * ParamA + bob);
        }

        private Vector2 ArrowOffset(int slot) {
            Vector2 perp = AimDir.RotatedBy(MathHelper.PiOver2);
            if (slot == 0) {
                //箭头尖
                return AimDir * 44f;
            }
            int row = (slot + 1) / 2;
            int side = slot % 2 == 1 ? -1 : 1;
            float flutter = (float)Math.Sin(Clock * 0.13f + slot * 1.9f) * 5f;
            return AimDir * (44f - row * 32f * ParamA)
                + perp * side * row * 25f
                + perp * flutter;
        }

        private Vector2 WallOffset(int slot, int count) {
            //ParamA=间距倍率；ParamB 打包两个缝位: gapA + gapB*100
            int gapA = (int)ParamB % 100;
            int gapB = (int)ParamB / 100;

            float row = slot;
            if (gapA > 0 && slot >= gapA) {
                row += WallGapRows;
            }
            if (gapB > 0 && slot >= gapB) {
                row += WallGapRows;
            }
            float totalRows = count - 1
                + (gapA > 0 ? WallGapRows : 0f)
                + (gapB > 0 ? WallGapRows : 0f);

            float y = (row - totalRows * 0.5f) * 52f * ParamA;
            float flutterX = (float)Math.Sin(Clock * 0.16f + slot * 2.1f) * 6f;
            return new Vector2(flutterX, y);
        }

        private Vector2 VortexOffset(int slot, int count) {
            //ParamA=半径 ParamB=缺口角速度(带符号)
            float gapCenter = Clock * ParamB;
            float usable = MathHelper.TwoPi - VortexGapWidth;
            float angle = gapCenter + VortexGapWidth * 0.5f + usable * (slot + 0.5f) / count;
            return angle.ToRotationVector2() * ParamA;
        }

        private Vector2 ShieldOffset(int slot, int count) {
            bool inner = slot % 2 == 0;
            int ringIdx = slot / 2;
            int ringCount = inner ? (count + 1) / 2 : count / 2;
            if (ringCount <= 0) {
                ringCount = 1;
            }
            float radius = (inner ? 88f : 150f) * ParamA;
            float spin = inner ? 0.031f : -0.023f;
            float angle = MathHelper.TwoPi * ringIdx / ringCount + Clock * spin;
            return angle.ToRotationVector2() * radius;
        }

        private Vector2 UmbrellaOffset(int slot, int count) {
            //头顶150°弧
            float spread = MathHelper.ToRadians(150f);
            float t = count <= 1 ? 0.5f : slot / (float)(count - 1);
            float angle = -MathHelper.PiOver2 - spread * 0.5f + spread * t;
            float breathe = 1f + 0.06f * (float)Math.Sin(Clock * 0.09f + slot * 1.3f);
            return angle.ToRotationVector2() * 122f * ParamA * breathe;
        }

        private Vector2 LanceOffset(int slot) {
            Vector2 perp = AimDir.RotatedBy(MathHelper.PiOver2);
            float wave = (float)Math.Sin(slot * 1.35f + Clock * 0.25f);
            return -AimDir * (30f + slot * 23f)
                + perp * wave * (9f + slot * 0.9f);
        }

        private Vector2 ScatterOffset(int slot) {
            //每槽定向乱飞，哈希方向确定性一致
            float baseAngle = Hash01(slot * 7 + 3) * MathHelper.TwoPi;
            float wob = (float)Math.Sin(Clock * 0.21f + slot * 3.1f) * 0.7f;
            return (baseAngle + wob).ToRotationVector2() * (340f + Hash01(slot * 13 + 1) * 260f);
        }

        /// <summary>整型→0~1确定性哈希</summary>
        internal static float Hash01(int n) {
            unchecked {
                uint x = (uint)n * 2654435761u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return (x & 0xFFFFFF) / 16777215f;
            }
        }

        #endregion

        #region 掷镖指令

        /// <summary>对槽位区间发掷镖令(各端状态代码同一节拍调用)</summary>
        public void LaunchDarts(int fromSlot, int toSlot, Vector2 dir, float speed, int steerTime = 10) {
            float rot = dir.SafeNormalize(Vector2.UnitX).ToRotation();
            for (int s = fromSlot; s <= toSlot && s < MaxBees; s++) {
                if (s < 0) {
                    continue;
                }
                dartOrders[s] = new DartOrder {
                    IssueClock = Clock,
                    DirRotation = rot,
                    Speed = speed,
                    SteerTime = steerTime,
                };
            }
        }

        /// <summary>径向散射令：各槽位以"槽位目标-中心"方向出射(阵型释放拍)</summary>
        public void LaunchRadial(int fromSlot, int toSlot, Vector2 center, float speed, int steerTime = 0) {
            int count = Math.Max(Bees.Count, 1);
            for (int s = fromSlot; s <= toSlot && s < MaxBees; s++) {
                if (s < 0) {
                    continue;
                }
                Vector2 dir = (GetSlotTarget(s, count) - center).SafeNormalize(Vector2.UnitY);
                dartOrders[s] = new DartOrder {
                    IssueClock = Clock,
                    DirRotation = dir.ToRotation(),
                    Speed = speed,
                    SteerTime = steerTime,
                };
            }
        }

        /// <summary>蜂查询本槽位是否有新鲜掷镖令(4帧内)</summary>
        public bool TryGetDartOrder(int slot, out float dirRotation, out float speed, out int steerTime) {
            dirRotation = 0f;
            speed = 0f;
            steerTime = 0;
            if (slot < 0 || slot >= MaxBees) {
                return false;
            }
            DartOrder order = dartOrders[slot];
            if (order.Speed <= 0f || Clock - order.IssueClock > 4f) {
                return false;
            }
            dirRotation = order.DirRotation;
            speed = order.Speed;
            steerTime = order.SteerTime;
            return true;
        }

        #endregion

        #region 服务端蜂群管理

        /// <summary>服务端补蜂到目标数，单次至多 perCall 只，从屏外侧缘飞入</summary>
        public void ServerTopUp(int targetCount, int perCall = 2) {
            if (VaultUtils.isClient || !Queen.active) {
                return;
            }
            targetCount = Math.Min(targetCount, MaxBees);
            int alive = Bees.Count;
            int need = Math.Min(targetCount - alive, perCall);
            for (int i = 0; i < need; i++) {
                SpawnFormationBee();
            }
        }

        /// <summary>服务端生成一只编队蜂(女王腹部吐出)</summary>
        public NPC SpawnFormationBee(Vector2? spawnPos = null) {
            if (VaultUtils.isClient) {
                return null;
            }
            //持久槽位单调递增，服务端本地计数即可(经ai[0]随生成包同步)
            float nextSlot = 0f;
            foreach (var b in Bees) {
                if (b.ai[0] >= nextSlot) {
                    nextSlot = b.ai[0] + 1f;
                }
            }

            Vector2 pos = spawnPos ?? Queen.Center + new Vector2(0f, Queen.height * 0.3f);
            int type = (int)nextSlot % 3 == 2 ? NPCID.BeeSmall : NPCID.Bee;
            int index = NPC.NewNPC(Queen.GetSource_FromAI(), (int)pos.X, (int)pos.Y, type,
                ai0: nextSlot, ai1: 0f, ai2: Queen.whoAmI, ai3: BeeMarker);
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC bee = Main.npc[index];
            bee.velocity = Main.rand.NextVector2Circular(3f, 3f);
            bee.timeLeft = NPC.activeTime * 5;
            bee.netUpdate = true;
            return bee;
        }

        #endregion

        #region 信号波数学(渲染消费，纯函数，全端一致)

        /// <summary>
        /// 摇摆舞信号波：指令沿槽位链行进的波前，返回该槽位当前的闪光强度 0~1<br/>
        /// 波前从槽位0(箭尖/矛头/墙顶)出发扫向队尾，刚被扫过的蜂最亮后指数熄灭——<br/>
        /// 读作"命令正在传遍蜂群"；强度越高波越急，替代旧的连环辉光带
        /// </summary>
        public float GetSignalFlash(int slot, int count) {
            if (count <= 0 || slot < 0 || SignalIntensity <= 0.05f) {
                return 0f;
            }
            //信号越强传令越急：周期 96→34 帧
            float period = MathHelper.Lerp(96f, 34f, SignalIntensity);
            float front = Clock % period / period * count;
            //波前驶过本槽位后的距离(环回)
            float behind = slot <= front ? front - slot : front - slot + count;
            return (float)Math.Exp(-behind * 0.85f) * SignalIntensity;
        }

        /// <summary>
        /// 结构常亮高亮：让阵型的"门框"与"矛尖"在乱战里读得出来，返回 0~1<br/>
        /// 蜂墙缝沿蜂 / 漩涡缺口两沿蜂 / 箭矢与长矛的领航蜂常亮琥珀色
        /// </summary>
        public float GetEdgeHighlight(int slot, int count) {
            if (count <= 0 || slot < 0) {
                return 0f;
            }
            switch (Formation) {
                case SwarmFormation.Wall: {
                    int gapA = (int)ParamB % 100;
                    int gapB = (int)ParamB / 100;
                    if (gapA > 0 && (slot == gapA - 1 || slot == gapA)) {
                        return 1f;
                    }
                    if (gapB > 0 && (slot == gapB - 1 || slot == gapB)) {
                        return 1f;
                    }
                    return 0f;
                }
                case SwarmFormation.Vortex:
                    //槽位0与末位分居旋转缺口两侧
                    return slot == 0 || slot == count - 1 ? 1f : 0f;
                case SwarmFormation.Arrow:
                case SwarmFormation.Lance:
                    //领航蜂(箭尖/矛头)
                    return slot == 0 ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        #endregion
    }
}
