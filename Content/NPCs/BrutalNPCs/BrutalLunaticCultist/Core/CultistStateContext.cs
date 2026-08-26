using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>
    /// 状态机共享上下文<br/>
    /// 同步槽位约定:ai[0]=阶段 ai[1]=浑天仪形态(0随身 1离体) ai[2]=状态索引 ai[3]=合相充能 0~300
    /// </summary>
    internal class CultistStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 常量
        /// <summary>合相充能满格值(ai[3])</summary>
        public const float AlignMax = 300f;
        /// <summary>转阶段血量比:P0 星旋→P1 星云→P2 星尘→P3 日耀→P4 月明</summary>
        public static readonly float[] PhaseRatios = [0.85f, 0.65f, 0.45f, 0.25f];
        /// <summary>黄道环半径(世界px),战场边界</summary>
        public const float ArenaRadius = 1800f;
        /// <summary>合相蓄力窗内打断所需伤害占比</summary>
        public const float ConjunctionBreakRatio = 0.05f;
        #endregion

        #region 阶段与模式
        public bool IsDeathMode { get; set; }
        /// <summary>阶段 0星旋 1星云 2星尘 3日耀 4月明,镜像 ai[0]</summary>
        public int Phase { get; set; }
        /// <summary>浑天仪形态 0随身 1离体(掷环),镜像 ai[1]</summary>
        public int OrreryMode { get; set; }
        /// <summary>合相充能,镜像 ai[3],权威端写</summary>
        public float AlignCharge { get; set; }
        /// <summary>转阶段演出进行中</summary>
        public bool IsInPhaseTransition { get; set; }
        /// <summary>死亡演出完,CheckDead 放行</summary>
        public bool DeathPerformanceFinished { get; set; }

        /// <summary>失衡时长(帧),触发方设置</summary>
        public int StaggerDuration { get; set; } = 84;
        /// <summary>合相蓄力起点血量(打断判定基准,权威端)</summary>
        public int ConjunctionLifeStart { get; set; }

        /// <summary>竞技场圆心(入场时定桩,全场不动)</summary>
        public Vector2 ArenaCenter { get; set; }
        /// <summary>黄道环已生成</summary>
        public bool ArenaSpawned { get; set; }
        /// <summary>星球开火闸:本体收手时才放行(轮流出手的公平阀)</summary>
        public bool PlanetVolleyGate { get; set; }
        /// <summary>月明竖瞳开度 0~1,凝视态推高,自然回落</summary>
        public float PupilOpen { get; set; }
        #endregion

        #region 本体视觉数据(各端本地驱动)
        /// <summary>施法辉光 0~1,状态推高,控制器衰减</summary>
        public float CastAura { get; set; }
        /// <summary>施法辉光颜色</summary>
        public Color AuraColor { get; set; } = new(120, 210, 230);
        /// <summary>身体缩放脉冲</summary>
        public float ScalePulse { get; set; } = 1f;
        /// <summary>浑天仪显形环数 0~3(入场描绘/死亡崩碎)</summary>
        public float OrreryReveal { get; set; }
        /// <summary>合相视觉 0~1(充能比缓动,驱动三环收拢共面)</summary>
        public float OrreryAlignVis { get; set; }
        /// <summary>环辉 0~1,状态推高(蚀刻纹透光)</summary>
        public float OrreryGlow { get; set; }
        /// <summary>失衡晃动 0~1,环姿态抖乱</summary>
        public float StaggerWobble { get; set; }
        /// <summary>炽体 0~1,同帧加色复写强度</summary>
        public float BodyHot { get; set; }

        /// <summary>推高施法辉光</summary>
        public void PushAura(float glow, Color color) {
            if (glow >= CastAura) {
                CastAura = glow;
                AuraColor = color;
            }
        }

        /// <summary>每帧衰减与缓动,控制器调用</summary>
        public void DecayVisuals() {
            CastAura *= 0.90f;
            OrreryGlow *= 0.93f;
            BodyHot *= 0.94f;
            StaggerWobble *= 0.96f;
            PupilOpen *= 0.985f;
            ScalePulse = MathHelper.Lerp(ScalePulse, 1f, 0.1f);
            //合相读数:环收拢跟随充能,平滑不跳
            OrreryAlignVis = MathHelper.Lerp(OrreryAlignVis, MathHelper.Clamp(AlignCharge / AlignMax, 0f, 1f), 0.05f);
            if (CastAura < 0.01f) {
                CastAura = 0f;
            }
            if (OrreryGlow < 0.01f) {
                OrreryGlow = 0f;
            }
            if (BodyHot < 0.01f) {
                BodyHot = 0f;
            }
            if (StaggerWobble < 0.01f) {
                StaggerWobble = 0f;
            }
        }
        #endregion

        #region 攻击洗牌袋(仅权威端使用)
        private readonly List<CultistStateIndex> attackBag = [];
        private CultistStateIndex lastAttack = CultistStateIndex.Coil;

        /// <summary>阶段主场技能:主场加倍进池,其余当伏笔</summary>
        public static CultistStateIndex HomeSkill(int phase) => phase switch {
            0 => CultistStateIndex.RingHurl,
            1 => CultistStateIndex.StarChart,
            2 => CultistStateIndex.OrbitLance,
            3 => CultistStateIndex.Eclipse,
            _ => CultistStateIndex.Gaze,
        };

        /// <summary>
        /// 当前阶段攻击池:四式全程可见,主场技能双倍权重;<br/>
        /// 蚀祭在星尘相退池(主星绕体公转太快,本影走不稳);月明相凝视入池;掷星全程在池;<br/>
        /// 三式后手逐相解锁作战线升级:彗星潮 P1、十二宫封禁 P2、滞星雷阵 P3(压轴留到后半场)
        /// </summary>
        private void FillPool(List<CultistStateIndex> pool) {
            pool.Clear();
            pool.Add(CultistStateIndex.OrbitLance);
            pool.Add(CultistStateIndex.RingHurl);
            pool.Add(CultistStateIndex.StarChart);
            if (Phase != 2) {
                pool.Add(CultistStateIndex.Eclipse);
            }
            if (Phase >= 4) {
                pool.Add(CultistStateIndex.Gaze);
            }
            if (Phase >= 1) {
                pool.Add(CultistStateIndex.Comet);
            }
            if (Phase >= 2) {
                pool.Add(CultistStateIndex.ZodiacSeal);
            }
            if (Phase >= 3) {
                pool.Add(CultistStateIndex.StasisMines);
            }

            CultistStateIndex home = HomeSkill(Phase);
            if (home != CultistStateIndex.Eclipse || Phase != 2) {
                pool.Add(home);
            }
            //掷星:他的神器就是武器
            pool.Add(CultistStateIndex.PlanetHurl);
        }

        /// <summary>抽下一招:洗牌袋防复读</summary>
        public CultistStateIndex NextAttack() {
            if (attackBag.Count == 0) {
                FillPool(attackBag);
                for (int i = attackBag.Count - 1; i > 0; i--) {
                    int j = Main.rand.Next(i + 1);
                    (attackBag[i], attackBag[j]) = (attackBag[j], attackBag[i]);
                }
                if (attackBag.Count > 1 && attackBag[0] == lastAttack) {
                    (attackBag[0], attackBag[^1]) = (attackBag[^1], attackBag[0]);
                }
            }

            CultistStateIndex next = attackBag[0];
            attackBag.RemoveAt(0);
            lastAttack = next;
            return next;
        }

        /// <summary>转阶段清袋重排</summary>
        public void ClearAttackBag() {
            attackBag.Clear();
        }
        #endregion

        #region 合相充能(权威端写,ai[3] 镜像)
        /// <summary>加充能并封顶</summary>
        public void AddAlign(float amount) {
            AlignCharge = MathHelper.Clamp(AlignCharge + amount, 0f, AlignMax);
        }

        /// <summary>充能是否满格</summary>
        public bool AlignFull => AlignCharge >= AlignMax - 0.01f;
        #endregion
    }
}
