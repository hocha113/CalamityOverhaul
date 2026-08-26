using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm.Projectiles
{
    /// <summary>
    /// 「沙鞭」弹幕：沙暴甩出的斜向沙浪，出手慢、随即复合加速的鞭击运动学。
    /// 材质是沙（颗粒承体 + 速度拉伸 + 尾粒沉降），身体由旧位置链上的沙块贴图逐节承载，
    /// 头部一条速度拉伸的加色气流线；死亡后留下坠沙余痕（活得比弹体久）。
    /// 伤害在生成参数里随包完整送达，轨迹是本地帧数的确定性函数（各端同形）；
    /// Boss 在场时判定静默关闭
    /// </summary>
    internal class DuneStormSandLashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        private const int LifeFrames = 52;
        /// <summary>末段淡出帧（判定同步关闭）</summary>
        private const int FadeFrames = 8;
        /// <summary>复合加速：每帧续力直到达到上限（鞭子越甩越快）</summary>
        private const float AccelPerTick = 1.06f;
        private const float MaxSpeed = 24f;
        /// <summary>蛇摆转角幅度（弧度/帧，确定性正弦）</summary>
        private const float WeaveAmp = 0.028f;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;//沙鞭贴着沙丘游走，不被地形截断
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            int elapsed = Elapsed;

            //Boss 在场与末段淡出：判定与可见形态同步关闭
            Projectile.hostile = !CWRWorld.HasBoss && Projectile.timeLeft > FadeFrames - 2;

            //复合加速 + 确定性蛇摆（identity 同步，各端同形；无 Main.rand 参与轨迹）
            float speed = Projectile.velocity.Length();
            if (speed < MaxSpeed) {
                Projectile.velocity *= AccelPerTick;
            }
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(elapsed * 0.42f + Projectile.identity * 1.7f) * WeaveAmp);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.dedServ) {
                return;
            }

            //随行沙粒：一粒贴体拉丝，一粒隔帧沉降（沙的重力签名）
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Sand, -Projectile.velocity * 0.08f, 100, default, Main.rand.NextFloat(0.9f, 1.3f));
            trail.noGravity = true;
            if (elapsed % 2 == 0) {
                Dust fall = Dust.NewDustPerfect(
                    Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.3f, 0.9f),
                    DustID.Sand, new Vector2(Projectile.velocity.X * 0.06f, Main.rand.NextFloat(0.5f, 1.4f)),
                    120, default, Main.rand.NextFloat(0.7f, 1.1f));
                fall.noGravity = false;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.26f, 0.20f, 0.08f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
            float speedK = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0.2f, 1f);

            //鞭身：旧位置链逐节沙块，向尾收细变淡（颗粒承体，非光效冒充）
            for (int k = Projectile.oldPos.Length - 1; k >= 0; k--) {
                Vector2 old = Projectile.oldPos[k];
                if (old == Vector2.Zero) {
                    continue;
                }
                float t = 1f - k / (float)Projectile.oldPos.Length;
                Vector2 pos = old + Projectile.Size * 0.5f - Main.screenPosition;
                Color body = Color.Lerp(lightColor, DuneStorm.SandBright, 0.45f) * (fade * (0.25f + 0.6f * t));
                float rot = Projectile.oldRot.Length > k ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, pos, null, body, rot + k * 0.35f, orig,
                    (0.5f + 0.65f * t) * (0.8f + 0.3f * speedK), SpriteEffects.None, 0);
            }

            //鞭头：速度拉伸的暖沙气流线（A=0 加色敷料，长度随速度）。
            //Airflow ext_w=1.00 无端部衰减，按三段截条阶梯收口防两端硬切
            Texture2D streak = CWRAsset.Airflow.Value;
            float len = 60f + 130f * speedK;
            float segScaleX = len / streak.Width;
            float segScaleY = 22f / streak.Height;
            Color head = new Color(DuneStorm.SandBright.R, DuneStorm.SandBright.G, DuneStorm.SandBright.B, 0)
                * (0.5f * fade);
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            ReadOnlySpan<int> segX = [0, 77, 179];
            ReadOnlySpan<int> segW = [77, 102, 77];
            ReadOnlySpan<float> segA = [0.4f, 1f, 0.4f];
            for (int s = 0; s < 3; s++) {
                var src = new Rectangle(segX[s], 0, segW[s], streak.Height);
                float axisOffset = (segX[s] + segW[s] * 0.5f - streak.Width * 0.5f) * segScaleX;
                Main.EntitySpriteDraw(streak, Projectile.Center + axis * axisOffset - Main.screenPosition,
                    src, head * segA[s], Projectile.rotation,
                    new Vector2(segW[s] * 0.5f, streak.Height * 0.5f),
                    new Vector2(segScaleX, segScaleY), SpriteEffects.None, 0);
            }

            //鞭梢暖芯（小面积敷料，占比克制）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color tip = new Color(255, 226, 150, 0) * (0.35f * fade * speedK);
            Main.EntitySpriteDraw(glow, Projectile.Center + Projectile.velocity * 0.6f - Main.screenPosition,
                null, tip, 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散鞭余痕：坠沙活得比弹体久
            for (int i = 0; i < 14; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0f, 1.2f),
                    DustID.Sand,
                    new Vector2(Projectile.velocity.X * Main.rand.NextFloat(0.05f, 0.2f),
                        Main.rand.NextFloat(-0.5f, 2.2f)),
                    110, default, Main.rand.NextFloat(0.8f, 1.4f));
                dust.noGravity = false;
            }
        }
    }
}
