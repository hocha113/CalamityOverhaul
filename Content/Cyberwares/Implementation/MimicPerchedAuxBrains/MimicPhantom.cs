using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.MimicPerchedAuxBrains
{
    /// <summary>
    /// 拟态幻象弹幕，玩家克隆绘制
    /// <br/>Orbit 环绕→Rush 冲撞→Explode 自爆；localAI[0]=状态，ai[0]=槽位
    /// </summary>
    internal class MimicPhantom : ModProjectile
    {
        public enum PhantomState : byte
        {
            Orbit,
            Rush,
            Explode,
        }

        /// <summary>槽位索引 0~3，ai[0]</summary>
        public int PhantomSlot => (int)Projectile.ai[0];

        /// <summary>幻象状态，localAI[0] 仅本地</summary>
        public PhantomState State {
            get => (PhantomState)Projectile.localAI[0];
            set => Projectile.localAI[0] = (float)value;
        }

        /// <summary>冲撞目标 NPC，-1 无</summary>
        public int RushTargetNpc { get; private set; } = -1;

        /// <summary>冲撞终点缓存</summary>
        public Vector2 RushTargetPosition;

        /// <summary>状态计时</summary>
        public int StateTimer;

        /// <summary>克隆绘制复用 Player</summary>
        private static Player phantomDrawPlayer;

        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 60 * 60 * 30;
            Projectile.penetrate = -1;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 12;
            Projectile.DamageType = DamageClass.Generic;
        }

        /// <summary>槽位与时间计算椭圆轨道偏移</summary>
        public static Vector2 GetOrbitOffset(int slot, float time, float radius) {
            float baseAngle = MathHelper.TwoPi * slot / MimicPerchedAuxBrainPlayer.PhantomCount;
            float angle = baseAngle + time * 0.02f;
            //椭圆形轨道，竖直略小制造层叠感
            return new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius * 0.55f);
        }

        /// <summary>切换 Rush，伤害=敌怪攻击力换算</summary>
        public void BeginRush(Vector2 targetCenter, int targetNpcIndex, int damage) {
            State = PhantomState.Rush;
            RushTargetPosition = targetCenter;
            RushTargetNpc = targetNpcIndex;
            StateTimer = 0;
            //冲撞期间使用敌怪攻击力换算后的伤害
            Projectile.damage = damage;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.netUpdate = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //装备被卸下时立即销毁
            if (MimicPerchedAuxBrainPlayer.GetEquipped(owner) == null && State == PhantomState.Orbit) {
                Projectile.Kill();
                return;
            }

            StateTimer++;

            switch (State) {
                case PhantomState.Orbit:
                    UpdateOrbit(owner);
                    break;
                case PhantomState.Rush:
                    UpdateRush(owner);
                    break;
                case PhantomState.Explode:
                    UpdateExplode();
                    break;
            }
        }

        /// <summary>环绕飘行，Orbit 态 friendly=false</summary>
        private void UpdateOrbit(Player owner) {
            if (MimicPerchedAuxBrainPlayer.GetEquipped(owner) is not MimicPerchedAuxBrain equipped) {
                Projectile.Kill();
                return;
            }

            Vector2 offset = GetOrbitOffset(PhantomSlot, Main.GameUpdateCount, equipped.OrbitRadius);
            Vector2 target = owner.Center + offset;
            //平滑跟随，使位移自然
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (target - Projectile.Center) * 0.18f, 0.5f);
            Projectile.Center += Projectile.velocity * 0.5f;

            //朝向主体面向方向
            Projectile.spriteDirection = owner.direction;
            Projectile.rotation = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + PhantomSlot) * 0.06f;

            //轨道状态时不造成伤害
            Projectile.friendly = false;
            Projectile.damage = 0;
        }

        /// <summary>冲向袭击者，距&lt;32 或 StateTimer&gt;90 引爆</summary>
        private void UpdateRush(Player owner) {
            //目标NPC仍存活时持续刷新目标位置
            if (RushTargetNpc >= 0 && RushTargetNpc < Main.maxNPCs) {
                NPC npc = Main.npc[RushTargetNpc];
                if (npc.active && !npc.friendly && npc.life > 0) {
                    RushTargetPosition = npc.Center;
                }
            }

            Vector2 toTarget = RushTargetPosition - Projectile.Center;
            float distance = toTarget.Length();

            //接近目标或冲撞时间过长时引爆
            if (distance < 32f || StateTimer > 90) {
                EnterExplode();
                return;
            }

            //加速冲撞，最大速度限制
            float speed = MathHelper.Lerp(12f, 26f, MathHelper.Clamp(StateTimer / 30f, 0f, 1f));
            Vector2 dir = Vector2.Normalize(toTarget);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * speed, 0.25f);

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.spriteDirection = Projectile.velocity.X >= 0 ? 1 : -1;

            //冲撞期间持续制造尾迹粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustVel = -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.ShadowbeamStaff, dustVel, 120, default, 1.2f);
                dust.noGravity = true;
            }
        }

        /// <summary>切换 Explode，扩张 160×160 判定盒</summary>
        private void EnterExplode() {
            State = PhantomState.Explode;
            StateTimer = 0;
            Projectile.velocity = Vector2.Zero;
            //扩张判定盒进行AOE伤害
            Projectile.position = Projectile.Center - new Vector2(80f);
            Projectile.width = 160;
            Projectile.height = 160;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            //爆炸粒子
            for (int i = 0; i < 28; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.ShadowbeamStaff, vel, 100, default, 1.6f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 120, default, 1.4f);
                dust.noGravity = true;
            }
        }

        /// <summary>爆炸阶段 4 帧后销毁</summary>
        private void UpdateExplode() {
            if (StateTimer > 4) {
                Projectile.Kill();
            }
        }

        public override bool? CanHitNPC(NPC target) {
            //仅在冲撞或爆炸阶段允许伤害
            if (State == PhantomState.Orbit) {
                return false;
            }
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中后立即引爆，扩大场面感
            if (State == PhantomState.Rush) {
                EnterExplode();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) {
                return false;
            }

            //结束当前批次以使用 PlayerRenderer 绘制克隆体
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend
                , SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            phantomDrawPlayer ??= new Player();
            Player gp = phantomDrawPlayer;
            //标记为陈列体：tileRangeX/Y 是全局静态，ResetEffects 在 whoAmI==myPlayer 时会把它刷回原版 5/4，
            //而傀儡 whoAmI 默认 0、单人下恰好等于 myPlayer，会在绘制阶段抹掉本人与其他模组的接触距离加成
            gp.isDisplayDollOrInanimate = true;
            gp.CopyVisuals(owner);
            gp.ResetEffects();
            gp.position = Projectile.Center - owner.Size * 0.5f;
            gp.velocity = Vector2.Zero;
            gp.direction = Projectile.spriteDirection == 0 ? owner.direction : Projectile.spriteDirection;
            gp.bodyFrame = owner.bodyFrame;
            gp.legFrame = owner.legFrame;
            gp.fullRotation = Projectile.rotation;
            gp.fullRotationOrigin = owner.Size * 0.5f;
            gp.skinVariant = owner.skinVariant;
            gp.heldProj = -1;

            //不同状态采用不同色调，制造混乱视觉
            float alpha = State == PhantomState.Explode ? MathHelper.Clamp(1f - StateTimer / 4f, 0f, 1f) : 0.85f;
            Color tint = State switch {
                PhantomState.Rush => new Color(255, 60, 80, 255),
                PhantomState.Explode => new Color(255, 220, 100, 255),
                _ => new Color(160, 80, 220, 255),
            } * alpha;

            gp.skinColor = tint;
            gp.shirtColor = tint;
            gp.underShirtColor = tint;
            gp.pantsColor = tint;
            gp.shoeColor = tint;
            gp.hairColor = tint;
            gp.eyeColor = tint;

            Main.PlayerRenderer.DrawPlayer(Main.Camera, gp, gp.position, gp.fullRotation, gp.fullRotationOrigin);

            //恢复主世界的批次状态
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend
                , Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override bool ShouldUpdatePosition() {
            //轨道阶段位置由AI直接接管
            return State != PhantomState.Orbit;
        }
    }
}
