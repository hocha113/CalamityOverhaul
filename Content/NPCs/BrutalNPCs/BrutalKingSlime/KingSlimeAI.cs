using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime
{
    /// <summary>
    /// 史莱姆王 AI 重做（皇室霸主）
    /// <br/>使用状态机替换原版跳跃 + 召唤蓝史莱姆的循环；新增多种主动技能和皇室描边视觉。
    /// <br/>放大数值（HP / 伤害 / 防御）以匹配重做后的视觉冲击力。
    /// </summary>
    internal class KingSlimeAI : CWRNPCOverride
    {
        [VaultLoaden("Content/NPCs/BrutalNPCs/BrutalKingSlime/")]
        public static Texture2D KingSlime;
        [VaultLoaden(CWRConstant.Masking + "Wing")]
        public static Texture2D WingMask;
        public override int TargetID => NPCID.KingSlime;

        private KingSlimeStateMachine stateMachine;
        private KingSlimeStateContext stateContext;
        private Player targetPlayer;

        //保存初始 HP/伤害以便基于其放大
        private int origLifeMax = -1;
        private int origDamage = -1;

        //翅膀视觉本地状态——上一帧 velocity.Y，用于侦测"刚起跳"与"换向"
        private float prevVelY;
        private bool prevAirborne;

        //基础碰撞箱尺寸，用于按 scale 动态缩放命中盒
        private static int BaseHitboxWidth => 120;
        private static int BaseHitboxHeight => 110;

        #region 初始化

        public override void SetProperty() {
            //放大基础数值——皇室霸主，更耐打更危险
            //放在 SetProperty 中，先于 AI 执行，多次调用也不会重复放大（已有标记）
            if (origLifeMax < 0) {
                origLifeMax = npc.lifeMax;
                origDamage = npc.damage;

                //HP 提升 65%（专家 / 大师上层会自动叠加），防御 +6
                int newMax = (int)(origLifeMax * 1.65f);
                npc.lifeMax = newMax;
                npc.life = newMax;
                npc.defDefense = npc.defense = npc.defDefense + 6;

                //伤害提升 25%
                npc.damage = (int)(origDamage * 1.25f);
                npc.defDamage = npc.damage;
            }

            InitializeStateContext();
        }

        public override bool? CanCWROverride() => null;//没做完，暂时隐藏

        private void InitializeStateContext() {
            stateContext = new KingSlimeStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new KingSlimeStateMachine(stateContext);

            //客户端加入时根据 npc.ai[2] 还原服务端状态，避免 desync
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IKingSlimeState syncedState = KingSlimeStateMachine.CreateStateFromIndex(
                    (KingSlimeStateIndex)serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new KingSlimeIntroState());
            }
            else {
                stateMachine.SetInitialState(new KingSlimeIntroState());
            }
        }

        #endregion

        #region AI 主循环

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            //延迟初始化保护
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            UpdateScale();
            UpdateWings();

            Lighting.AddLight(npc.Center, Color.White.ToVector3());

            stateMachine?.Update();

            //每 10 帧强制网络同步
            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            //返回 false 跳过原版 AI；引擎仍然会基于 npc.velocity 做位置更新和地形碰撞
            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not KingSlimeDespawnState) {
                    stateMachine?.ForceChangeState(new KingSlimeDespawnState());
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsEnraged = npc.life < npc.lifeMax * 0.5f;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
        }

        /// <summary>
        /// 模拟原版"血量越低体型越小"的缩放，再叠加 SquishY 提供蹲伏 / 拉伸视觉。
        /// <br/>同时同步调整 <see cref="NPC.width"/> / <see cref="NPC.height"/>，
        /// 让碰撞箱实时跟随贴图缩放，避免出现"看起来很大却打不到"或"很小但仍占大格子"的问题。
        /// </summary>
        private void UpdateScale() {
            float lifePct = MathHelper.Clamp(npc.life / (float)MathHelper.Max(npc.lifeMax, 1), 0f, 1f);
            //从 1.6（满血）平滑插值到 0.85（残血）
            float baseScale = MathHelper.Lerp(0.85f, 1.6f, lifePct);
            //叠加 SquishY：正值（下蹲）整体偏小一些，负值（拉伸）整体偏大
            float squish = MathHelper.Clamp(stateContext.SquishY, -0.45f, 0.45f);
            //把纵向压扁简单转换为整体缩放变化（uniform scale）
            float visualScale = baseScale * (1f - squish * 0.30f);
            npc.scale = MathHelper.Clamp(visualScale, 0.45f, 2.0f);

            SyncHitboxWithScale();
        }

        /// <summary>
        /// 更新血光凝胶翅膀视觉状态。
        /// <br/>设计核心——把所有"模式"都表达为 0~1 的连续混入度（Extension / FlapStrength / FallingMix /
        ///       Alpha），AI 只设置目标值并以不同速率 lerp，渲染端禁用 if-else 分支。
        /// <br/>这样状态机在"飞行→落地"等切换时不会产生 baseAngle、phaseSpeed 等关键参数的瞬时跳变。
        /// <br/>所有字段均为视觉量，无需网络同步——多端各自从 npc.velocity / collideY 推导。
        /// </summary>
        private void UpdateWings() {
            //=======================================================
            // 1) 状态推导——bool 仅用于"目标值选择"，不直接驱动渲染
            //=======================================================
            //是否处于地面（碰到下方地块且基本静止）——加宽容度避免 collideY 抖动反复触发
            bool onGround = npc.collideY && npc.velocity.Y >= 0f && Math.Abs(npc.velocity.Y) < 1.5f;
            bool airborne = !onGround;
            bool fallingFast = npc.velocity.Y > 4f && !npc.collideY;
            bool slamming = stateMachine?.CurrentState is KingSlimeRoyalSlamFallingState;

            //=======================================================
            // 2) 计算每个连续控制量的"目标值"
            //=======================================================
            //展开度：空中或砸地展开；地面收拢
            float targetExtension = (airborne || slamming) ? 1f : 0f;

            //"用力扑翅程度"——核心连续量
            //  地面：0（自然垂落，仅靠 idle 慢相位维持微振）
            //  空中正常：1
            //  砸地下落 / 高速下坠：~0.10（翼会随风轻颤但不扑翅）
            //  IsEnraged 提供 +0.10 上限
            float targetStrength;
            if (slamming || fallingFast) {
                targetStrength = 0.10f;
            }
            else if (airborne) {
                targetStrength = 1f;
            }
            else {
                targetStrength = 0f;
            }
            if (stateContext.IsEnraged && targetStrength > 0.05f) {
                targetStrength = MathHelper.Min(1f, targetStrength + 0.10f);
            }
            //蓄力中也微振翅，让"地面蓄技"姿态不死板
            if (stateContext.IsCharging && !slamming) {
                targetStrength = MathHelper.Max(targetStrength, 0.30f);
            }

            //砸地下落"姿态"混入度——0=正常展翼姿势, 1=完全后掠绷紧
            float targetFalling = (slamming || fallingFast) ? 1f : 0f;

            //可见 alpha
            float targetAlpha = airborne ? 1f : 0.30f;
            if (stateContext.IsCharging || stateContext.IsEnraged) {
                targetAlpha = MathHelper.Max(targetAlpha, 0.65f);
            }

            //=======================================================
            // 3) 平滑接近——不同字段用不同速率，匹配各自语义
            //   速率越大越急；速率为 r 时 90% 达成约需 ln(0.1)/ln(1-r) 帧
            //   r=0.08 → ~28 帧（~0.47s）；r=0.12 → ~18 帧（~0.30s）；r=0.18 → ~11 帧（~0.18s）
            //=======================================================
            stateContext.WingExtension = MathHelper.Lerp(stateContext.WingExtension, targetExtension, 0.10f);
            stateContext.WingFlapStrength = MathHelper.Lerp(stateContext.WingFlapStrength, targetStrength, 0.09f);
            stateContext.WingFallingMix = MathHelper.Lerp(stateContext.WingFallingMix, targetFalling, 0.12f);
            stateContext.WingAlpha = MathHelper.Lerp(stateContext.WingAlpha, targetAlpha, 0.10f);

            //=======================================================
            // 4) 相位推进——idle 慢呼吸恒定 + active 受 strength 驱动
            //   关键：phase 永远以 idle 速率前进，不再受 airborne 硬分支控制
            //   ——避免空中→地面瞬间扑翅"冻结成纸板"
            //=======================================================
            const float idlePhaseSpeed = 0.045f;
            float speedFromVy = MathHelper.Clamp(-npc.velocity.Y, 0f, 18f) * 0.022f;
            float activeBoost = (0.16f + speedFromVy) * stateContext.WingFlapStrength;
            //砸地下落时再额外抑制（FallingMix 越大越压低）
            float fallingDamp = MathHelper.Lerp(1f, 0.30f, stateContext.WingFallingMix);
            float phaseSpeed = (idlePhaseSpeed + activeBoost) * fallingDamp;
            stateContext.WingFlapPhase = MathHelper.WrapAngle(stateContext.WingFlapPhase + phaseSpeed);

            //=======================================================
            // 5) 扑翅"能量"——事件型脉冲（非过渡量），衰减保留
            //=======================================================
            stateContext.WingFlapEnergy = MathHelper.Max(0f, stateContext.WingFlapEnergy - 0.045f);

            //刚离开地面（起跳瞬间）：刷一次最大扑翅冲量
            if (prevAirborne == false && airborne && npc.velocity.Y < -2f) {
                stateContext.WingFlapEnergy = 1f;
                //同步把 strength 抢拉到高位，下面的 lerp 再继续锁定
                stateContext.WingFlapStrength = MathHelper.Max(stateContext.WingFlapStrength, 0.85f);
            }
            //大跳冲量
            if (npc.velocity.Y < -8f && prevVelY > -2f) {
                stateContext.WingFlapEnergy = MathHelper.Max(stateContext.WingFlapEnergy, 0.85f);
            }
            //空中顶点——临界点小爆翅以营造"二段扇"感
            if (airborne && prevVelY < 0f && npc.velocity.Y >= 0f) {
                stateContext.WingFlapEnergy = MathHelper.Max(stateContext.WingFlapEnergy, 0.55f);
            }

            stateContext.WingFalling = slamming || fallingFast;
            prevVelY = npc.velocity.Y;
            prevAirborne = airborne;
        }

        /// <summary>
        /// 根据 <see cref="NPC.scale"/> 重新计算碰撞箱尺寸。
        /// <br/>以"底部居中"为锚点（脚底贴地），通过先减后加 <see cref="NPC.position"/>
        /// 抵消尺寸变化导致的瞬移，与原版史莱姆王逻辑一致。
        /// </summary>
        private void SyncHitboxWithScale() {
            int newWidth = (int)(BaseHitboxWidth * npc.scale);
            int newHeight = (int)(BaseHitboxHeight * npc.scale);
            //至少保留 2 像素，避免极端缩放导致碰撞箱归零
            if (newWidth < 2) newWidth = 2;
            if (newHeight < 2) newHeight = 2;

            if (newWidth == npc.width && newHeight == npc.height) {
                return;
            }

            //以底部中心为锚：先把锚点位移补回去，改完尺寸再减回来
            npc.position.X += npc.width / 2f;
            npc.position.Y += npc.height;
            npc.width = newWidth;
            npc.height = newHeight;
            npc.position.X -= npc.width / 2f;
            npc.position.Y -= npc.height;
        }

        #endregion

        #region 召唤蓝史莱姆——保留但减少频率

        /// <summary>
        /// 史莱姆王在战斗中会持续召唤蓝史莱姆，重做后改为低频召唤以避免数量爆炸。
        /// </summary>
        public override bool? On_PreKill() {
            //死亡时清除剩余的皇冠光柱与冲击波，避免视觉残留
            CleanUpProjectiles();
            return null;
        }

        private static void CleanUpProjectiles() {
            int beam = Terraria.ModLoader.ModContent.ProjectileType<Projectiles.KingSlimeCrownBeamProj>();
            int wave = Terraria.ModLoader.ModContent.ProjectileType<Projectiles.KingSlimeShockwaveProj>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == beam || p.type == wave) {
                    p.Kill();
                }
            }
        }

        #endregion

        #region 视觉描边状态推送

        /// <summary>
        /// 根据当前状态决定皇室光环的视觉模式，统一在 PostDraw 中读取。
        /// </summary>
        private (KingSlimeAuraMode mode, float intensity, float progress) GetAuraVisuals() {
            if (stateContext == null) return (KingSlimeAuraMode.Idle, 0.5f, 0f);

            //砸地下落 / 砸地恢复——白热爆闪
            if (stateMachine?.CurrentState is KingSlimeRoyalSlamFallingState) {
                return (KingSlimeAuraMode.Slamming, 1f, 1f);
            }

            //蓄力中——皇冠血焰光
            if (stateContext.IsCharging) {
                return (KingSlimeAuraMode.Charging, 0.85f, stateContext.ChargeProgress);
            }

            //暴怒（二阶段）——深血晶光
            if (stateContext.IsEnraged) {
                return (KingSlimeAuraMode.Enraged, 0.70f, 0f);
            }

            //常态——暗红皇室光
            return (KingSlimeAuraMode.Idle, 0.45f, 0f);
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 自定义贴图尺寸（单帧 170 x 150），如果未来更换贴图，仅改这里即可。
        /// </summary>
        private const int BodyTexWidth = 170;
        private const int BodyTexHeight = 150;

        /// <summary>
        /// 计算本体绘制所需的贴图、源矩形、原点和位置等参数。
        /// <br/>原点采用"底部居中"——这样无论 <c>npc.scale</c> 如何变化，
        /// 史莱姆视觉脚底始终贴合 <c>npc.Bottom</c>，避免血量低时整体悬浮在空中。
        /// </summary>
        private bool TryGetBodyDrawParams(out Texture2D texture, out Rectangle frame,
            out Vector2 origin, out Vector2 drawPos, out SpriteEffects effects, Vector2 screenPos) {
            texture = KingSlime;
            if (texture == null) {
                frame = default;
                origin = default;
                drawPos = default;
                effects = SpriteEffects.None;
                return false;
            }

            frame = new Rectangle(0, 0, BodyTexWidth, BodyTexHeight);
            origin = new Vector2(BodyTexWidth / 2f, BodyTexHeight);
            drawPos = new Vector2(npc.Center.X, npc.position.Y + npc.height) - screenPos
                + new Vector2(0, npc.gfxOffY);
            effects = npc.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //蓄力 / 演出附加特效（蓄力光圈、瞄准线）画在身体之前
            if (stateContext != null) {
                KingSlimeRenderHelper.DrawChargeEffect(spriteBatch, stateContext);
            }

            if (!TryGetBodyDrawParams(out Texture2D bodyTex, out Rectangle frame,
                out Vector2 origin, out Vector2 drawPos, out SpriteEffects effects, screenPos)) {
                //贴图尚未加载完成时，回退到原版绘制
                return null;
            }

            //血光凝胶翅膀——画在本体后面（先于身体绘制 + 描边光晕）
            if (stateContext != null && WingMask != null && stateContext.WingAlpha > 0.01f) {
                KingSlimeRenderHelper.DrawBloodWings(spriteBatch, WingMask, npc, stateContext, screenPos);
            }

            var (mode, intensity, progress) = GetAuraVisuals();

            //外圈描边光晕——确保夜晚远距离也能看清史莱姆王轮廓
            if (npc.alpha < 240) {
                KingSlimeRenderHelper.DrawRoyalHalo(spriteBatch, bodyTex, drawPos, frame,
                    npc.rotation, origin, new Vector2(npc.scale), effects, mode, intensity, progress);
            }

            //本体套上皇室光环着色器；alpha 极高时跳过着色器，直接淡出
            bool shaderApplied = false;
            if (npc.alpha < 240) {
                shaderApplied = KingSlimeRenderHelper.BeginRoyalAuraShader(
                    spriteBatch, bodyTex, frame, mode, intensity, progress, seed: 0f);
            }

            spriteBatch.Draw(bodyTex, drawPos, frame, drawColor,
                npc.rotation, origin, npc.scale, effects, 0f);

            if (shaderApplied) {
                KingSlimeRenderHelper.EndRoyalAuraShader(spriteBatch);
            }

            return false;
        }

        public override bool? CheckDead() => true;

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //本体绘制已经在 Draw 中完成，PostDraw 不再做任何叠加，
            //返回 false 跳过原版残留的皇冠 / 眼睛 / 凝胶贴图，避免与自定义贴图错位。
            return false;
        }

        #endregion

        #region 防御 / 命中

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) {
            //避免被自己生成的弹幕误伤
            if (projectile.type == Terraria.ModLoader.ModContent.ProjectileType<Projectiles.KingSlimeShockwaveProj>()
                || projectile.type == Terraria.ModLoader.ModContent.ProjectileType<Projectiles.KingSlimeCrownBeamProj>()
                || projectile.type == Terraria.ModLoader.ModContent.ProjectileType<Projectiles.KingSlimeRoyalGelDropletProj>()) {
                modifiers.FinalDamage *= 0f;
            }
        }

        #endregion
    }
}
