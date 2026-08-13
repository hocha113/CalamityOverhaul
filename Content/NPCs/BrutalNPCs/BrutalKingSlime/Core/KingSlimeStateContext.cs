using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using InnoVault.StateMachines;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core
{
    /// <summary>状态上下文</summary>
    internal class KingSlimeStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 战斗状态
        /// <summary>低于60%血(阶段2)；王冠仍默认扣顶，仅招式期离体</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>低于30%血</summary>
        public bool IsLowHP { get; set; }
        public bool IsDeathMode { get; set; }
        /// <summary>阶段转换演出已完成</summary>
        public bool Phase2Started { get; set; }
        /// <summary>低血大招已放过</summary>
        public bool DecreeDone { get; set; }
        /// <summary>出招环索引</summary>
        public int AttackPhaseIndex { get; set; }
        /// <summary>本次潮汐为中距液化掠近(位移工具化)：单程、贴近即收、重组后直入下一招。
        /// 服务端在连接拍置位，ai[5]镜像给客户端，潮汐退出时清除</summary>
        public bool TideTravelActive { get; set; }
        /// <summary>液化掠近冷却(帧，服务端消费)，防止连续液化位移压掉输出窗</summary>
        public int TideTravelCooldown { get; set; }
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>失去视线/远离累计帧，追击阀依据(服务端)</summary>
        public int LostContactTimer { get; set; }
        #endregion

        #region 每帧声明（状态设置，主控消费后复位）
        /// <summary>接触伤害系数，每帧由状态声明，默认1</summary>
        public float ContactDamageScale { get; set; } = 1f;
        /// <summary>本帧跳过主控重力积分(潮汐/塔/分裂悬浮期自管)</summary>
        public bool SkipGravity { get; set; }
        /// <summary>落地时要生成的冲击波档位 -1无 0小 1中 2大，主控消费后清-1</summary>
        public int PendingLandingShockwave { get; set; } = -1;
        /// <summary>落地凝胶飞溅强度系数</summary>
        public float LandingSplashMul { get; set; } = 1f;
        #endregion

        #region 视觉（各端本地推导）
        /// <summary>压扁-拉伸弹簧值，1=常态，&lt;1扁宽，&gt;1瘦高</summary>
        public float VisualSquash { get; set; } = 1f;
        public float SquashVelocity { get; set; }
        /// <summary>受击/落地摇晃振幅</summary>
        public float WobbleAmp { get; set; }
        public float WobblePhase { get; set; }
        /// <summary>身体绘制透明度(液化/潜地时降低)</summary>
        public float BodyOpacity { get; set; } = 1f;
        /// <summary>隐藏身体贴图(潮汐/塔期由弹幕接管形体)</summary>
        public bool HideBodySprite { get; set; }
        /// <summary>光环模式 0常态 1蓄力 2狂暴 3砸地，映射 RoyalAura shader</summary>
        public int AuraMode { get; set; }
        /// <summary>光环进度 0~1</summary>
        public float AuraProgress { get; set; }
        /// <summary>体内忍者发亮 0~1(影袭前摇)</summary>
        public float NinjaGlow { get; set; }
        /// <summary>形体缩放乘子(分裂期收缩为核心球)</summary>
        public float ScaleMul { get; set; } = 1f;
        /// <summary>身体倾倒角(弧度，立塔倾倒用)，绕底部中心</summary>
        public float BodyLean { get; set; }
        /// <summary>体内忍者已脱出(死亡演出)，本体不再绘制忍者</summary>
        public bool NinjaGone { get; set; }
        /// <summary>入场王冠天降进度，(0,1]=坠落中，其余不绘制；每帧声明</summary>
        public float IntroCrownDrop { get; set; }
        /// <summary>本帧隐藏头顶扣冠(入场未加冕等)；每帧声明</summary>
        public bool HideCrown { get; set; }
        /// <summary>扣冠纵向滞后偏移(px，正=下沉)，弹簧跟随本体起落做次级运动</summary>
        public float CrownLag { get; set; }
        public float CrownLagVel { get; set; }
        #endregion

        #region 落地检测（主控每帧维护）
        /// <summary>上一帧纵速，用于落地冲量</summary>
        public float PrevVelY { get; set; }
        /// <summary>本帧刚落地</summary>
        public bool JustLanded { get; set; }
        /// <summary>落地冲量(上一帧下落速度)</summary>
        public float LandingPower { get; set; }
        #endregion

        /// <summary>状态退出时复位每帧声明项</summary>
        public void ResetPerStateFlags() {
            ContactDamageScale = 1f;
            SkipGravity = false;
            PendingLandingShockwave = -1;
            LandingSplashMul = 1f;
            HideBodySprite = false;
            AuraMode = 0;
            AuraProgress = 0f;
            NinjaGlow = 0f;
            BodyOpacity = 1f;
            ScaleMul = 1f;
            BodyLean = 0f;
            HideCrown = false;
        }

        /// <summary>王冠砸扣头顶：凝胶受压微陷+扣冠回弹</summary>
        public void CrownMountImpact(float power) {
            ImpactSquash(power);
            CrownLagVel += 7f + power * 14f;
        }

        /// <summary>落地/受压冲击：压扁弹簧冲量</summary>
        public void ImpactSquash(float power) {
            SquashVelocity -= power;
            WobbleAmp = MathHelper.Clamp(WobbleAmp + power * 0.5f, 0f, 0.5f);
        }

        /// <summary>起跳拉伸冲量</summary>
        public void StretchImpulse(float power) {
            SquashVelocity += power;
        }

        /// <summary>找到本体的离体王冠弹幕(仅招式三拍内存在)，无则null</summary>
        public Projectile FindCrown() {
            int type = ModContent.ProjectileType<BKSCrownProj>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == Npc.whoAmI) {
                    return proj;
                }
            }
            return null;
        }
    }
}
