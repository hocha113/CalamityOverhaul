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
        /// 运动平滑度(0-1，越小越平滑)
        /// </summary>
        protected virtual float MovementSmoothness => 0.08f;

        /// <summary>
        /// 是否启用螺旋运动
        /// </summary>
        protected virtual bool UseSpiralMotion => false;

        /// <summary>
        /// 是否启用脉冲式移动(靠近-远离)
        /// </summary>
        protected virtual bool UsePulseMotion => false;

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

            //初始化个人相位偏移，基于门徒索引
            personalPhaseOffset = DiscipleIndex * 0.523f; //约30度间隔
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
        /// 更新环绕运动 - 优化版
        /// </summary>
        protected void UpdateOrbitMovement() {
            int totalDisciples = 1;
            int discipleOrder = 0;
            if (Owner.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                totalDisciples = Math.Max(1, ep.GetDiscipleCount());
                discipleOrder = GetDiscipleOrder();
            }

            //基础环绕参数
            float baseRadius = 90f + totalDisciples * 6f;
            float angleSpacing = MathHelper.TwoPi / totalDisciples;
            float baseAngleOffset = angleSpacing * discipleOrder;

            //更新环绕角度(考虑个人速度差异)
            float orbitSpeed = 0.015f * OrbitSpeedMultiplier;
            //门徒越多，整体旋转越慢，但更协调
            orbitSpeed *= 1f - totalDisciples * 0.02f;
            orbitAngle += orbitSpeed;

            //计算主环绕位置
            float currentAngle = orbitAngle + baseAngleOffset;
            Vector2 baseOrbitPos = Owner.Center + currentAngle.ToRotationVector2() * baseRadius;

            //呼吸效果 - 所有门徒同步呼吸，但有细微相位差
            float breathe = (float)Math.Sin(breathePhase + personalPhaseOffset * 0.5f) * 12f;
            baseOrbitPos += currentAngle.ToRotationVector2() * breathe;

            //垂直波动 - 创造浮动感
            float verticalWave = (float)Math.Sin(pulsePhase * 1.2f + personalPhaseOffset) * VerticalWaveAmplitude;
            //水平波动 - 左右轻微摆动
            float horizontalWave = (float)Math.Cos(pulsePhase * 0.8f + personalPhaseOffset * 1.5f) * HorizontalWaveAmplitude;

            //将波动转换为相对于环绕切线方向的偏移
            Vector2 tangent = (currentAngle + MathHelper.PiOver2).ToRotationVector2();
            Vector2 waveOffset = tangent * horizontalWave + new Vector2(0, verticalWave * 0.5f);

            //螺旋运动(可选)
            Vector2 spiralOffset = Vector2.Zero;
            if (UseSpiralMotion) {
                float spiralRadius = (float)Math.Sin(spiralPhase * 2f + personalPhaseOffset) * 20f;
                float spiralAngle = spiralPhase * 3f + personalPhaseOffset;
                spiralOffset = spiralAngle.ToRotationVector2() * spiralRadius * 0.5f;
            }

            //脉冲运动(可选) - 周期性靠近和远离玩家
            if (UsePulseMotion) {
                float targetRadiusOffset = (float)Math.Sin(pulsePhase * 0.5f + personalPhaseOffset) * 25f;
                radiusOffset = MathHelper.Lerp(radiusOffset, targetRadiusOffset, 0.05f);
                baseOrbitPos += currentAngle.ToRotationVector2() * radiusOffset;
            }

            //子类自定义偏移
            Vector2 customOffset = CalculateCustomOffset();

            //最终目标位置
            Vector2 targetPos = baseOrbitPos + waveOffset + spiralOffset + customOffset;

            //跟随玩家移动 - 当玩家移动时，门徒会有轻微滞后
            Vector2 playerVelocityInfluence = Owner.velocity * 0.15f;

            //平滑移动到目标位置
            Vector2 toTarget = targetPos - Projectile.Center + playerVelocityInfluence;
            velocitySmooth = Vector2.Lerp(velocitySmooth, toTarget * MovementSmoothness * 2f, 0.1f);
            Projectile.Center += velocitySmooth;

            //额外的位置修正，防止偏离太远
            float distToTarget = Vector2.Distance(Projectile.Center, targetPos);
            if (distToTarget > 100f) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.15f);
            }

            //更新轨迹
            UpdateTrail();
        }

        /// <summary>
        /// 更新轨迹记录
        /// </summary>
        private void UpdateTrail() {
            //每隔几帧记录一次位置，使轨迹更平滑
            if (actionTimer % 2 == 0) {
                if (trailPositions.Count > 12) trailPositions.RemoveAt(0);
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
            Projectile.rotation = velocitySmooth.ToRotation() * 0.1f + pulsePhase * 0.3f;
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //获取门徒纹理(自动加载同名纹理)
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

            //绘制光晕底层
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Color glowColor = DiscipleColor with { A = 0 } * glowIntensity * 0.5f;
                sb.Draw(glowTex, drawPos, null, glowColor, 0, glowTex.Size() / 2, 1.5f, SpriteEffects.None, 0);

                Color innerGlow = Color.White with { A = 0 } * glowIntensity * 0.3f;
                sb.Draw(glowTex, drawPos, null, innerGlow, 0, glowTex.Size() / 2, 0.8f, SpriteEffects.None, 0);
            }

            //绘制轨迹
            DrawTrail(sb, tex);

            //绘制门徒主体
            sb.Draw(tex, drawPos, null, lightColor, Projectile.rotation, tex.Size() / 2, 1f, SpriteEffects.None, 0);

            //绘制十字架光环
            DrawCrossHalo(sb, drawPos);

            //子类自定义绘制
            CustomDraw(sb, drawPos);

            return false;
        }

        /// <summary>
        /// 绘制轨迹 - 优化版
        /// </summary>
        private void DrawTrail(SpriteBatch sb, Texture2D tex) {
            if (trailPositions.Count < 2) return;

            for (int i = 0; i < trailPositions.Count; i++) {
                float progress = (i + 1) / (float)(trailPositions.Count + 1);
                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                //使用Catmull-Rom样条插值使轨迹更平滑
                Color trailColor = DiscipleColor with { A = 0 } * progress * 0.25f;
                float trailScale = progress * 0.4f + 0.1f;

                //轨迹透明度渐变
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
            float crossSize = 20f;
            float thickness = 2f;

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
