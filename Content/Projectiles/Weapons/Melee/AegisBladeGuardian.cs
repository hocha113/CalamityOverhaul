using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AegisBladeGuardian : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AegisBlade";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }
        public override bool? CanDamage() => Projectile.ai[1] != 2 ? false : base.CanDamage();
        public override void AI() {
            //根据速度调整旋转角度
            if (Projectile.ai[1] != 1) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }

            //初始化发射速度
            if (Projectile.ai[0] == 0) {
                Projectile.velocity = new Vector2(0, -12);
            }
            else if (Projectile.ai[1] == 0) {
                HandleStateZero();
            }
            else if (Projectile.ai[1] == 1) {
                HandleStateOne();
            }
            else if (Projectile.ai[1] == 2) {
                HandleStateTwo();
            }

            //全局计时器递增
            Projectile.ai[0]++;
        }

        //处理状态0：发射后悬停并跟随玩家
        private void HandleStateZero() {
            Player player = Main.player[Projectile.owner];
            //平滑插值缩放
            Projectile.scale = MathHelper.Lerp(Projectile.scale, 1.2f, 0.1f);
            //快速减速以停留在空中
            Projectile.velocity *= 0.9f;
            //平滑移动到玩家头顶上方
            Vector2 targetPos = player.Center + new Vector2(0, -60);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.1f);

            //当速度足够小且接近目标位置时进入下一状态
            if (Projectile.velocity.LengthSquared() < 1 && Vector2.Distance(Projectile.Center, targetPos) < 120) {
                TransitionToState(1, resetVelocity: true);
            }
        }

        //处理状态1：蓄力阶段
        private void HandleStateOne() {
            Player player = Main.player[Projectile.owner];
            
            //计算蓄力进度
            float progress = Math.Min(Projectile.ai[0] / 60f, 1f);
            
            //旋转速度随蓄力进度增加
            Projectile.rotation += 0.2f + progress * 0.5f;
            
            //缩放效果加入弹性震荡模拟能量不稳定性
            float pulse = (float)Math.Sin(Projectile.ai[0] * 0.2f) * 0.1f * progress;
            Projectile.scale = MathHelper.Lerp(1.2f, 2.5f, progress) + pulse;

            //生成汇聚粒子
            if (Projectile.ai[0] % 5 == 0) {
                Vector2 offset = Main.rand.NextVector2Circular(80, 80);
                Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * (2f + progress * 3f);
                PRT_Light particle = new PRT_Light(Projectile.Center + offset, velocity, 0.5f, Color.Gold, 30);
                PRTLoader.AddParticle(particle);
            }

            //伤害随蓄力增加
            if (Projectile.damage < Projectile.originalDamage * 45) {
                Projectile.damage += 35;
            }

            //保持在玩家上方
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = Vector2.Lerp(Projectile.Center, player.Center + new Vector2(0, -60), 0.2f);

            //检测按键释放或超时
            if (player.PressKey(false)) {
                Projectile.timeLeft = 300;
                //限制最大蓄力时间
                if (Projectile.ai[0] > 55) Projectile.ai[0] = 55;
            }

            if (Projectile.ai[0] > 60 || !player.PressKey(false)) {
                TransitionToState(2, resetVelocity: false);
                //蓄力完成时的爆发特效
                if (!VaultUtils.isServer) {
                    PRT_DWave wave = new PRT_DWave(Projectile.Center, Vector2.Zero, Color.Gold, new Vector2(1f, 1f), 0f, 0.5f, 3f, 20);
                    PRTLoader.AddParticle(wave);
                }
            }
        }

        //处理状态2：追踪攻击
        private void HandleStateTwo() {
            //寻找最近的敌人
            NPC npc = Projectile.Center.FindClosestNPC(6000, true, true);

            if (npc != null) {
                //强力追踪逻辑
                Vector2 toTarget = npc.Center - Projectile.Center;
                if (toTarget.Length() > 0) {
                    //如果是刚进入状态2，给予一个巨大的初速度
                    if (Projectile.ai[0] == 1) {
                        Projectile.velocity = toTarget.SafeNormalize(Vector2.Zero) * 40f;
                    }
                    else {
                        //后续平滑转向
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * 40f, 0.1f);
                    }
                }
                Projectile.penetrate = 1;
            }
            else {
                //如果没有敌人则自爆
                Projectile.Kill();
            }

            //高速移动时的拖尾粒子
            if (Projectile.ai[0] % 2 == 0) {
                PRT_Spark spark = new PRT_Spark(Projectile.Center, -Projectile.velocity * 0.2f, false, 10, 1.5f, Color.Gold);
                PRTLoader.AddParticle(spark);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        //状态转换逻辑
        private void TransitionToState(int newState, bool resetVelocity) {
            Projectile.ai[1] = newState;
            Projectile.ai[0] = 0; //重置计时器为0，方便计算
            if (resetVelocity) {
                Projectile.velocity = Vector2.Zero;
            }
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            Projectile.damage = Projectile.originalDamage;
            Projectile.Explode(1200);
            
            //爆炸视觉效果
            if (!VaultUtils.isServer) {
                //冲击波
                PRT_DWave wave = new PRT_DWave(Projectile.Center, Vector2.Zero, Color.Gold, new Vector2(1f, 1f), 0f, 2f, 5f, 30);
                PRTLoader.AddParticle(wave);

                //大量散射光点
                for (int i = 0; i < 30; i++) {
                    Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 15f);
                    PRT_Light light = new PRT_Light(Projectile.Center, velocity, Main.rand.NextFloat(0.8f, 1.5f), Color.Gold, 40);
                    PRTLoader.AddParticle(light);
                }

                //火花飞溅
                for (int i = 0; i < 50; i++) {
                    Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(10f, 30f);
                    PRT_Spark spark = new PRT_Spark(Projectile.Center, velocity, true, 60, 2f, Color.Orange);
                    PRTLoader.AddParticle(spark);
                }
            }

            //生成后续弹幕
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.scale > 1.6f) {
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = VaultUtils.RandVr(12, 16);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + velocity.UnitVector() * 13
                        , velocity, CWRID.Proj_AegisFlame, (int)(Projectile.damage * 0.5), 0f, Projectile.owner, 0f, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            
            //残影绘制
            if (Projectile.ai[1] == 2) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.EntitySpriteDraw(value, Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2, null, Color.White * (1 - i * 0.1f) * 0.5f
                    , Projectile.rotation, value.Size() / 2, Projectile.scale - i * 0.1f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
