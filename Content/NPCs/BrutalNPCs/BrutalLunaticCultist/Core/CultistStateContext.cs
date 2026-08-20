using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>
    /// 状态机共享上下文<br/>
    /// 同步槽位约定：ai[0]=阶段 ai[1]=当前元素 ai[2]=状态索引(状态机) ai[3]=仪式充能 0~300
    /// </summary>
    internal class CultistStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 常量
        /// <summary>仪式充能满格值（ai[3]）</summary>
        public const float RitualMax = 300f;
        /// <summary>二阶段血量比</summary>
        public const float Phase2Ratio = 0.65f;
        /// <summary>三阶段血量比</summary>
        public const float Phase3Ratio = 0.30f;
        /// <summary>咏唱打断所需伤害占比</summary>
        public const float ChantBreakRatio = 0.06f;
        #endregion

        #region 阶段与模式
        public bool IsDeathMode { get; set; }
        /// <summary>阶段 0/1/2，镜像 ai[0]</summary>
        public int Phase { get; set; }
        /// <summary>当前元素 0火 1冰 2雷，镜像 ai[1]</summary>
        public int Element { get; set; }
        /// <summary>仪式充能，镜像 ai[3]，权威端写</summary>
        public float RitualCharge { get; set; }
        /// <summary>转阶段演出进行中</summary>
        public bool IsInPhaseTransition { get; set; }
        /// <summary>死亡演出完，CheckDead 放行</summary>
        public bool DeathPerformanceFinished { get; set; }

        /// <summary>咏唱冷却帧，权威端递减；开局留缓冲</summary>
        public int ChantCooldown { get; set; } = 480;
        /// <summary>踉跄时长（帧），进 Stagger 前由触发方设置</summary>
        public int StaggerDuration { get; set; } = 90;
        /// <summary>本次镜像仪式已计入的假身惩罚充能（上限阀）</summary>
        public float MirrorPenaltyGained { get; set; }
        /// <summary>镜像仪式进行中（克隆弹幕语义与渲染提示用）</summary>
        public bool MirrorActive { get; set; }
        /// <summary>P3 幻影龙已唤出</summary>
        public bool DragonSpawned { get; set; }
        /// <summary>猎杀幻影龙的充能削减已发放</summary>
        public bool DragonRewardGiven { get; set; }
        #endregion

        #region 本体视觉数据（各端本地驱动）
        /// <summary>施法辉光 0~1，状态推高，控制器衰减</summary>
        public float CastAura { get; set; }
        /// <summary>施法辉光颜色</summary>
        public Color AuraColor { get; set; } = new(255, 150, 60);
        /// <summary>背后仪式法阵描绘进度 0~1，入场推满</summary>
        public float SigilReveal { get; set; }
        /// <summary>法阵定形迸发 0~1，充能满/大招时推高</summary>
        public float SigilCommit { get; set; }
        /// <summary>身体缩放脉冲</summary>
        public float ScalePulse { get; set; } = 1f;
        /// <summary>咏唱强度 0~1，喂给帷幕屏效</summary>
        public float ChantGlow { get; set; }

        /// <summary>推高施法辉光</summary>
        public void PushAura(float glow, Color color) {
            if (glow >= CastAura) {
                CastAura = glow;
                AuraColor = color;
            }
        }

        /// <summary>每帧衰减与缓动，控制器调用</summary>
        public void DecayVisuals() {
            CastAura *= 0.90f;
            SigilCommit *= 0.92f;
            ChantGlow *= 0.94f;
            ScalePulse = MathHelper.Lerp(ScalePulse, 1f, 0.1f);
            if (CastAura < 0.01f) {
                CastAura = 0f;
            }
            if (SigilCommit < 0.01f) {
                SigilCommit = 0f;
            }
            if (ChantGlow < 0.01f) {
                ChantGlow = 0f;
            }
        }
        #endregion

        #region 攻击洗牌袋（仅权威端使用）
        private readonly List<CultistStateIndex> attackBag = [];
        private CultistStateIndex lastAttack = CultistStateIndex.Weave;

        /// <summary>当前阶段攻击池</summary>
        private void FillPool(List<CultistStateIndex> pool) {
            pool.Clear();
            pool.Add(CultistStateIndex.FlameHunt);
            pool.Add(CultistStateIndex.FrostLattice);
            pool.Add(CultistStateIndex.StormCadence);
            pool.Add(CultistStateIndex.AncientRite);
            if (Phase >= 1) {
                pool.Add(CultistStateIndex.MirrorRite);
            }
            if (Phase >= 2) {
                //三阶段镜像加权：骗术是他低血时的求生本能
                pool.Add(CultistStateIndex.MirrorRite);
            }
        }

        /// <summary>抽下一招：咏唱按冷却优先，其余洗牌袋防复读</summary>
        public CultistStateIndex NextAttack() {
            if (ChantCooldown <= 0) {
                return CultistStateIndex.Chant;
            }

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

        #region 仪式充能（权威端写，ai[3] 镜像）
        /// <summary>加充能并封顶</summary>
        public void AddRitual(float amount) {
            RitualCharge = MathHelper.Clamp(RitualCharge + amount, 0f, RitualMax);
        }

        /// <summary>充能是否满格</summary>
        public bool RitualFull => RitualCharge >= RitualMax - 0.01f;
        #endregion
    }
}
