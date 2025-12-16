using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AirBomb : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "Cyclone";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        private bool span;
        const float MaxAttractionRange = 600f; //最大吸引距离
        const float DustRotationStep = 60f; //每个 Dust 的角度增量
        const float SmokeDustScale = 1.3f; //烟尘的比例
        const int MaxSmokeDusts = 6; //最大烟尘数量
        const float HomingSpeed = 1f; //追踪速度
        const float HomingFactor = 0.1f; //追踪平滑因子
        const float MinVelocityForKill = 2f; //最小速度阈值
        const float DecelerationFactor = 0.99f; //减速因子
        const int LightDecayRate = 30; //光照消减速率
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 2;
            Projectile.penetrate = 2;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (!span) {
                if (Projectile.ai[0] == 1) {
                    Projectile.damage /= 2;
                    Projectile.timeLeft = 360;
                }
                span = true;
            }

            Projectile.rotation += 2.5f;
            Projectile.alpha = Math.Max(50, Projectile.alpha - 5);

            if (Projectile.alpha == 50 && Projectile.ai[2] >= 15f) {
                GenerateDust(Projectile.Center, MaxSmokeDusts, DustRotationStep, SmokeDustScale);
                Projectile.ai[2] = 0f;
            }

            if (Projectile.ai[0] == 0) {
                HandlePhaseOne(MaxAttractionRange);
            }
            else if (Projectile.ai[0] == 1) {
                HandlePhaseTwo(HomingSpeed, HomingFactor, MinVelocityForKill, DecelerationFactor, LightDecayRate);
            }
        }

        /// <summary>
        /// 处理第一阶段的逻辑，包括 NPC 引力吸引
        /// </summary>
        private void HandlePhaseOne(float maxAttractionRange) {
            Projectile.ai[1]++;
            Projectile.ai[2]++;
            if (Projectile.ai[1] >= 12f) {
                Projectile.tileCollide = true;
            }

            foreach (var npc in Main.npc) {
                if (!IsValidTarget(npc, maxAttractionRange)) {
                    continue;
                }
                ApplyAttractionForce(npc, Projectile.Center, maxAttractionRange);
            }
        }

        /// <summary>
        /// 处理第二阶段的逻辑，包括追踪和烟尘效果
        /// </summary>
        private void HandlePhaseTwo(float homingSpeed, float homingFactor, float minVelocity, float deceleration, int lightDecayRate) {
            Projectile.localAI[0]++;
            Projectile.scale = 0.7f + MathF.Abs(MathF.Sin(MathHelper.ToRadians(Projectile.localAI[0] * 2)) * 0.5f);

            NPC target = Projectile.Center.FindClosestNPC(300);
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, homingSpeed, homingFactor);
            }

            if (Framing.GetTileSafely(Projectile.position).HasSolidTile()) {
                Projectile.velocity *= deceleration;
                if (Projectile.velocity.LengthSquared() < minVelocity * minVelocity) {
                    Projectile.Kill();
                }
            }

            if (Projectile.timeLeft < 300 && Projectile.timeLeft % lightDecayRate == 0) {
                GenerateDustCircle(Projectile.Center, 3f * Projectile.scale, 1.4f, DustID.Smoke, new Color(232, 251, 250, 200));
            }

            Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * Projectile.scale * 0.5f);
        }

        /// <summary>
        /// 检查 NPC 是否是有效目标
        /// </summary>
        private bool IsValidTarget(NPC npc, float maxRange) {
            return npc.CanBeChasedBy(Projectile) &&
                   Collision.CanHit(Projectile.Center, 1, 1, npc.Center, 1, 1) &&
                   Vector2.Distance(Projectile.Center, npc.Center) < maxRange;
        }

        /// <summary>
        /// 对目标施加吸引力
        /// </summary>
        private void ApplyAttractionForce(NPC npc, Vector2 origin, float maxRange) {
            if (npc.Center.X < origin.X) {
                npc.velocity.X += 0.05f;
            }
            else {
                npc.velocity.X -= 0.05f;
            }

            if (npc.Center.Y < origin.Y) {
                npc.velocity.Y += 0.05f;
            }
            else {
                npc.velocity.Y -= 0.05f;
            }
        }

        /// <summary>
        /// 生成一个烟尘旋涡效果
        /// </summary>
        private void GenerateDust(Vector2 center, int dustCount, float angleStep, float scale) {
            float currentAngle = 0;
            for (int i = 0; i < dustCount; i++) {
                Vector2 velocity = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(currentAngle));
                int dustId = Dust.NewDust(center, 0, 0, DustID.Smoke, velocity.X, velocity.Y, 200, new Color(232, 251, 250, 200), scale);
                Main.dust[dustId].noGravity = true;
                Main.dust[dustId].velocity = velocity;
                currentAngle += angleStep;
            }
        }

        /// <summary>
        /// 生成一个环形的烟尘效果
        /// </summary>
        private void GenerateDustCircle(Vector2 center, float speed, float scale, int dustType, Color color) {
            for (int i = 0; i < 360; i += 3) {
                Vector2 velocity = new Vector2(speed, speed).RotatedBy(MathHelper.ToRadians(i)) * scale;
                int dustId = Dust.NewDust(center, 0, 0, dustType, velocity.X, velocity.Y, 200, color, scale);
                Main.dust[dustId].noGravity = true;
                Main.dust[dustId].position = center;
                Main.dust[dustId].velocity = velocity;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] == 0) {
                if (Projectile.velocity.X != oldVelocity.X)
                    Projectile.velocity.X = -oldVelocity.X;

                if (Projectile.velocity.Y != oldVelocity.Y)
                    Projectile.velocity.Y = -oldVelocity.Y;

                if (Projectile.numHits > 12) {
                    Projectile.Kill();
                }

                SpanDust();
            }
            return false;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(204, 255, 255, Projectile.alpha);

        public void SpanDust() {
            for (int i = 0; i <= 360; i += 3) {
                Vector2 velocity = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.Smoke, velocity.X, velocity.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                Main.dust[num].noGravity = true;
                Main.dust[num].position = Projectile.Center;
                Main.dust[num].velocity = velocity;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            float bmtSize = 1f;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Color.White * (float)(((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale * bmtSize, SpriteEffects.None, 0);
                bmtSize -= 0.01f;
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                SoundStyle style = SoundID.Item60;
                style.Volume = SoundID.Item60.Volume * 0.6f;
                SoundEngine.PlaySound(in style, Projectile.Center);
            }

            SpanDust();
        }
    }
}
