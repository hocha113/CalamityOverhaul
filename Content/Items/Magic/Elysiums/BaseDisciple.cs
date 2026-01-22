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

        #endregion

        #region 通用方法

        /// <summary>
        /// 更新环绕运动
        /// </summary>
        protected void UpdateOrbitMovement() {
            int totalDisciples = 1;
            if (Owner.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                totalDisciples = Math.Max(1, ep.GetDiscipleCount());
            }

            float baseRadius = 100f + totalDisciples * 8f;
            float angleOffset = MathHelper.TwoPi / totalDisciples * GetDiscipleOrder();

            //Lissajous曲线
            float a = 2f;
            float b = 3f;
            float lissajousX = (float)Math.Sin(a * pulsePhase) * 15f;
            float lissajousY = (float)Math.Cos(b * pulsePhase) * 10f;

            //呼吸效果
            float breathe = (float)Math.Sin(pulsePhase * 0.5f) * 8f;

            orbitAngle += 0.02f + DiscipleIndex * 0.002f;
            float targetAngle = orbitAngle + angleOffset;

            Vector2 orbitPos = Owner.Center + targetAngle.ToRotationVector2() * (baseRadius + breathe);
            orbitPos += new Vector2(lissajousX, lissajousY);

            Projectile.Center = Vector2.Lerp(Projectile.Center, orbitPos, 0.08f);

            if (trailPositions.Count > 8) trailPositions.RemoveAt(0);
            trailPositions.Add(Projectile.Center);
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
            Projectile.rotation = pulsePhase * 0.5f;
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
            for (int i = 0; i < trailPositions.Count; i++) {
                float progress = i / (float)trailPositions.Count;
                Vector2 trailPos = trailPositions[i] - Main.screenPosition;
                Color trailColor = DiscipleColor with { A = 0 } * progress * 0.3f;
                float trailScale = progress * 0.5f;
                sb.Draw(tex, trailPos, null, trailColor, Projectile.rotation, tex.Size() / 2, trailScale, SpriteEffects.None, 0);
            }

            //绘制门徒主体
            sb.Draw(tex, drawPos, null, lightColor, Projectile.rotation, tex.Size() / 2, 1f, SpriteEffects.None, 0);

            //绘制十字架光环
            DrawCrossHalo(sb, drawPos);

            //子类自定义绘制
            CustomDraw(sb, drawPos);

            return false;
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
