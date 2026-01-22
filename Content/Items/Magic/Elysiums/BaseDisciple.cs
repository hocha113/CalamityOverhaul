using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 门徒弹幕抽象基类，所有12门徒继承此类
    /// ai[0] = 原NPC的whoAmI(用于某些机制)
    /// </summary>
    internal abstract class BaseDisciple : ModProjectile
    {
        #region 抽象属性(子类必须实现)

        /// <summary>
        /// 门徒索引(0-11)
        /// </summary>
        public abstract int DiscipleIndex { get; }

        /// <summary>
        /// 门徒名称
        /// </summary>
        public abstract string DiscipleName { get; }

        /// <summary>
        /// 门徒代表色
        /// </summary>
        public abstract Color DiscipleColor { get; }

        /// <summary>
        /// 能力冷却时间(帧)
        /// </summary>
        public virtual int AbilityCooldownTime => 120;

        #endregion

        #region 运动配置(子类可重写以定制运动风格)

        /// <summary>
        /// 基础环绕速度倍率
        /// </summary>
        protected virtual float OrbitSpeedMultiplier => 1f;

        /// <summary>
        /// 垂直波动幅度
        /// </summary>
        protected virtual float VerticalWaveAmplitude => 15f;

        /// <summary>
        /// 水平波动幅度
        /// </summary>
        protected virtual float HorizontalWaveAmplitude => 10f;

        /// <summary>
        /// 运动平滑度(0-1，越大越快速响应)
        /// </summary>
        protected virtual float MovementSmoothness => 0.15f;

        /// <summary>
        /// 是否启用螺旋运动
        /// </summary>
        protected virtual bool UseSpiralMotion => false;

        /// <summary>
        /// 是否启用脉冲式移动(靠近-远离)
        /// </summary>
        protected virtual bool UsePulseMotion => false;

        /// <summary>
        /// 3D轨道倾斜角度(弧度)，每个门徒不同以避免重叠
        /// </summary>
        protected virtual float OrbitTiltAngle => DiscipleIndex * 0.15f;

        /// <summary>
        /// 3D轨道倾斜方向(弧度)
        /// </summary>
        protected virtual float OrbitTiltDirection => DiscipleIndex * MathHelper.TwoPi / 12f;

        /// <summary>
        /// 轨道高度层级(-1到1)，用于避免同层碰撞
        /// </summary>
        protected virtual float OrbitHeightLayer => (DiscipleIndex % 3 - 1) * 0.4f;

        #endregion

        #region 通用字段

        protected Player Owner => Main.player[Projectile.owner];

        //环绕角度
        protected float orbitAngle = 0f;

        //行为计时器
        protected int actionTimer = 0;

        //能力冷却
        protected int abilityCooldown = 0;

        //视觉效果
        protected float glowIntensity = 0f;
        protected float pulsePhase = 0f;
        protected List<Vector2> trailPositions = [];

        //运动相关
        private float personalPhaseOffset;      //个人相位偏移
        private float breathePhase;             //呼吸相位
        private float spiralPhase;              //螺旋相位
        private Vector2 velocitySmooth;         //平滑速度
        private float radiusOffset;             //半径偏移(用于脉冲运动)

        //伪3D相关
        private float pseudo3DDepth;            //伪3D深度值(用于缩放)
        private float currentScale = 1f;        //当前缩放

        //防碰撞
        private Vector2 avoidanceOffset;        //避让偏移
        private const float MinDiscipleDistance = 35f; //门徒最小间距

        #endregion

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 999999;
            Projectile.DamageType = DamageClass.Magic;

            //初始化个人相位偏移，基于门徒索引，使用黄金角度分布
            float goldenAngle = MathHelper.Pi * (3f - (float)Math.Sqrt(5f)); //约137.5度
            personalPhaseOffset = DiscipleIndex * goldenAngle;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //检查玩家是否还持有天国极乐
            bool hasElysium = false;
            foreach (Item item in Owner.inventory) {
                if (item.type == ModContent.ItemType<Elysium>()) {
                    hasElysium = true;
                    break;
                }
            }
            if (!hasElysium) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 120;

            //更新计时器
            actionTimer++;
            if (abilityCooldown > 0) abilityCooldown--;
            pulsePhase += 0.05f;
            breathePhase += 0.03f;
            spiralPhase += 0.02f;

            //环绕玩家运动
            UpdateOrbitMovement();

            //执行门徒特殊能力(子类实现)
            if (abilityCooldown <= 0) {
                ExecuteAbility();
            }

            //持续性效果(子类可选重写)
            PassiveEffect();

            //更新视觉效果
            UpdateVisuals();

            //发光效果
            Color lightColor = DiscipleColor;
            float baseLight = 0.5f;
            Lighting.AddLight(Projectile.Center, lightColor.R / 255f * baseLight, lightColor.G / 255f * baseLight, lightColor.B / 255f * baseLight);
        }

        #region 抽象方法(子类必须实现)

        /// <summary>
        /// 执行门徒特殊能力(主动技能)
        /// </summary>
        protected abstract void ExecuteAbility();

        #endregion

        #region 虚方法(子类可选重写)

        /// <summary>
        /// 持续性被动效果
        /// </summary>
        protected virtual void PassiveEffect() { }

        /// <summary>
        /// 自定义绘制(在基础绘制之后)
        /// </summary>
        protected virtual void CustomDraw(SpriteBatch sb, Vector2 drawPos) { }

        /// <summary>
        /// 死亡时的特殊效果
        /// </summary>
        protected virtual void OnDiscipleDeath() { }

        /// <summary>
        /// 计算额外的位置偏移(子类可重写实现独特运动)
        /// </summary>
        protected virtual Vector2 CalculateCustomOffset() => Vector2.Zero;

        #endregion

        #region 通用方法

        /// <summary>
        /// 更新环绕运动 - 伪3D版本，行星环绕风格
        /// </summary>
        protected void UpdateOrbitMovement() {
            int totalDisciples = 1;
            int discipleOrder = 0;
            if (Owner.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                totalDisciples = Math.Max(1, ep.GetDiscipleCount());
                discipleOrder = GetDiscipleOrder();
            }

            //基础环绕参数 - 较大的轨道半径
            float baseRadius = 100f + totalDisciples * 10f;

            //使用黄金角度分布，确保门徒均匀分散
            float goldenAngle = MathHelper.Pi * (3f - (float)Math.Sqrt(5f));
            float baseAngleOffset = goldenAngle * discipleOrder;

            //更新环绕角度 - 更快的基础速度，行星环绕感
            float orbitSpeed = 0.035f * OrbitSpeedMultiplier;
            orbitAngle += orbitSpeed;

            //当前在轨道上的角度
            float currentAngle = orbitAngle + baseAngleOffset + personalPhaseOffset * 0.3f;

            //============ 伪3D轨道计算 ============

            //计算3D空间中的位置 - 倾斜椭圆轨道
            float tiltAngle = OrbitTiltAngle + (float)Math.Sin(breathePhase * 0.3f) * 0.05f;
            float tiltDirection = OrbitTiltDirection;

            //3D坐标计算
            float x3D = (float)Math.Cos(currentAngle) * baseRadius;
            float y3D = (float)Math.Sin(currentAngle) * baseRadius;
            float z3D = (float)Math.Sin(currentAngle + tiltDirection) * baseRadius * (float)Math.Sin(tiltAngle);

            //添加高度层级偏移
            float heightOffset = OrbitHeightLayer * 25f;
            float verticalOscillation = (float)Math.Sin(pulsePhase * 0.6f + personalPhaseOffset) * VerticalWaveAmplitude * 0.5f;
            z3D += heightOffset + verticalOscillation;

            //将3D坐标投影到2D屏幕
            //z轴影响y坐标(上下位置)和缩放(远近感)
            Vector2 projected2D = Owner.Center + new Vector2(x3D, y3D * 0.5f - z3D * 0.35f);

            //根据深度调整缩放 - 只改变大小，不改变透明度
            pseudo3DDepth = z3D / baseRadius; //-1到1
            float targetScale = MathHelper.Clamp(0.65f + pseudo3DDepth * 0.35f, 0.5f, 1.15f);
            currentScale = MathHelper.Lerp(currentScale, targetScale, 0.15f);

            //============ 额外运动效果 ============

            //呼吸效果 - 轻微的半径波动
            float breathe = (float)Math.Sin(breathePhase + personalPhaseOffset * 0.5f) * 6f;
            Vector2 breatheOffset = currentAngle.ToRotationVector2() * breathe;

            //切线方向波动 - 轻微摆动
            Vector2 tangent = (currentAngle + MathHelper.PiOver2).ToRotationVector2();
            float horizontalWave = (float)Math.Cos(pulsePhase * 0.5f + personalPhaseOffset * 1.5f) * HorizontalWaveAmplitude * 0.5f;
            Vector2 waveOffset = tangent * horizontalWave;

            //螺旋运动(可选)
            Vector2 spiralOffset = Vector2.Zero;
            if (UseSpiralMotion) {
                float spiralRadius = (float)Math.Sin(spiralPhase * 2f + personalPhaseOffset) * 12f;
                float spiralAngle = spiralPhase * 3f + personalPhaseOffset;
                spiralOffset = spiralAngle.ToRotationVector2() * spiralRadius * 0.4f;
            }

            //脉冲运动(可选)
            if (UsePulseMotion) {
                float targetRadiusOffset = (float)Math.Sin(pulsePhase * 0.3f + personalPhaseOffset) * 15f;
                radiusOffset = MathHelper.Lerp(radiusOffset, targetRadiusOffset, 0.08f);
                projected2D += currentAngle.ToRotationVector2() * radiusOffset;
            }

            //子类自定义偏移
            Vector2 customOffset = CalculateCustomOffset();

            //============ 防碰撞处理 ============
            Vector2 avoidance = CalculateAvoidance();
            avoidanceOffset = Vector2.Lerp(avoidanceOffset, avoidance, 0.2f);

            //最终目标位置
            Vector2 targetPos = projected2D + breatheOffset + waveOffset + spiralOffset + customOffset + avoidanceOffset;

            //跟随玩家移动 - 快速响应
            Vector2 playerVelocityInfluence = Owner.velocity * 0.2f;

            //平滑移动到目标位置 - 更快的响应速度
            Vector2 toTarget = targetPos - Projectile.Center + playerVelocityInfluence;
            velocitySmooth = Vector2.Lerp(velocitySmooth, toTarget * MovementSmoothness * 3f, 0.2f);
            Projectile.Center += velocitySmooth;

            //位置修正 - 防止偏离太远
            float distToTarget = Vector2.Distance(Projectile.Center, targetPos);
            if (distToTarget > 60f) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);
            }

            //更新轨迹
            UpdateTrail();
        }

        /// <summary>
        /// 计算避让偏移，防止门徒重叠
        /// </summary>
        private Vector2 CalculateAvoidance() {
            if (!Owner.TryGetModPlayer<ElysiumPlayer>(out var ep)) return Vector2.Zero;

            Vector2 totalAvoidance = Vector2.Zero;
            int avoidCount = 0;

            foreach (int projIdx in ep.ActiveDisciples) {
                if (projIdx == Projectile.whoAmI) continue;
                if (projIdx < 0 || projIdx >= Main.maxProjectiles) continue;

                Projectile other = Main.projectile[projIdx];
                if (!other.active) continue;

                float dist = Vector2.Distance(Projectile.Center, other.Center);
                if (dist < MinDiscipleDistance && dist > 1f) {
                    //计算排斥力，距离越近排斥越强
                    Vector2 awayDir = (Projectile.Center - other.Center).SafeNormalize(Vector2.UnitX);
                    float strength = (MinDiscipleDistance - dist) / MinDiscipleDistance;
                    strength = (float)Math.Pow(strength, 1.5f); //非线性增强近距离排斥

                    totalAvoidance += awayDir * strength * 25f;
                    avoidCount++;
                }
            }

            if (avoidCount > 0) {
                totalAvoidance /= avoidCount;
            }

            return totalAvoidance;
        }

        /// <summary>
        /// 更新轨迹记录
        /// </summary>
        private void UpdateTrail() {
            if (actionTimer % 2 == 0) {
                if (trailPositions.Count > 15) trailPositions.RemoveAt(0);
                trailPositions.Add(Projectile.Center);
            }
        }

        /// <summary>
        /// 获取门徒在队列中的顺序
        /// </summary>
        protected int GetDiscipleOrder() {
            if (!Owner.TryGetModPlayer<ElysiumPlayer>(out var ep)) return 0;

            int order = 0;
            foreach (int projIdx in ep.ActiveDisciples) {
                if (projIdx == Projectile.whoAmI) break;
                if (projIdx >= 0 && projIdx < Main.maxProjectiles && Main.projectile[projIdx].active) {
                    order++;
                }
            }
            return order;
        }

        /// <summary>
        /// 寻找最近的敌人
        /// </summary>
        protected NPC FindNearestEnemy(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.npc) {
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        /// <summary>
        /// 设置能力冷却
        /// </summary>
        protected void SetCooldown(int? customCooldown = null) {
            abilityCooldown = customCooldown ?? AbilityCooldownTime;
        }

        /// <summary>
        /// 更新视觉效果
        /// </summary>
        protected void UpdateVisuals() {
            glowIntensity = 0.5f + (float)Math.Sin(pulsePhase) * 0.3f;
            //旋转随运动方向变化
            float targetRotation = velocitySmooth.ToRotation() * 0.1f + pulsePhase * 0.15f;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.15f);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

            //绘制光晕底层
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Color glowColor = DiscipleColor with { A = 0 } * glowIntensity * 0.5f;
                sb.Draw(glowTex, drawPos, null, glowColor, 0, glowTex.Size() / 2, 1.5f * currentScale, SpriteEffects.None, 0);

                Color innerGlow = Color.White with { A = 0 } * glowIntensity * 0.3f;
                sb.Draw(glowTex, drawPos, null, innerGlow, 0, glowTex.Size() / 2, 0.8f * currentScale, SpriteEffects.None, 0);
            }

            //绘制轨迹
            DrawTrail(sb, tex);

            //绘制门徒主体（带深度缩放，无透明度变化）
            sb.Draw(tex, drawPos, null, lightColor, 0, tex.Size() / 2, currentScale, SpriteEffects.None, 0);

            //绘制十字架光环
            DrawCrossHalo(sb, drawPos);

            //子类自定义绘制
            CustomDraw(sb, drawPos);

            return false;
        }

        /// <summary>
        /// 绘制轨迹 - 带深度感
        /// </summary>
        private void DrawTrail(SpriteBatch sb, Texture2D tex) {
            if (trailPositions.Count < 2) return;

            for (int i = 0; i < trailPositions.Count; i++) {
                float progress = (i + 1) / (float)(trailPositions.Count + 1);
                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                //轨迹颜色渐变，不受深度透明度影响
                Color trailColor = DiscipleColor with { A = 0 } * progress * 0.25f;
                float trailScale = (progress * 0.35f + 0.1f) * currentScale;

                float alphaMod = (float)Math.Pow(progress, 1.5f);
                trailColor *= alphaMod;

                sb.Draw(tex, trailPos, null, trailColor, Projectile.rotation * progress, tex.Size() / 2, trailScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制十字架光环
        /// </summary>
        protected void DrawCrossHalo(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;
            if (pixel == null) return;

            Color crossColor = DiscipleColor with { A = 0 } * glowIntensity * 0.6f;
            float crossSize = 20f * currentScale;
            float thickness = 2f * currentScale;

            sb.Draw(pixel, center - new Vector2(thickness / 2, crossSize / 2), null, crossColor, 0, Vector2.Zero, new Vector2(thickness, crossSize), SpriteEffects.None, 0);
            sb.Draw(pixel, center - new Vector2(crossSize / 2, thickness / 2), null, crossColor, 0, Vector2.Zero, new Vector2(crossSize, thickness), SpriteEffects.None, 0);
        }

        #endregion

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 30; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(5f, 5f), 100, DiscipleColor, 1.5f);
                d.noGravity = true;
            }
            OnDiscipleDeath();
        }

        public override bool? CanDamage() => false;
    }
}
