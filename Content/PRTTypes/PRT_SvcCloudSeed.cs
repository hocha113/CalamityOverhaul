using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 天气控制机云种:白色能量弹自机顶升空(复合加速,拒绝匀速直线),
    /// 沿途甩湿雾丝,升到寿命尽头炸开成一簇 <see cref="PRT_SvcCloud"/>。
    /// 速度拉伸本体,加色绘制
    /// </summary>
    internal class PRT_SvcCloudSeed : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 12;

        private int fuse;
        private Color burstTint;

        /// <param name="lifetime">升空帧数,到点即炸</param>
        /// <param name="fuseDelay">点火延迟(隐身蓄势),错帧发射用</param>
        /// <param name="burstColor">炸开云团的染色</param>
        public PRT_SvcCloudSeed Configure(int lifetime, int fuseDelay, Color burstColor) {
            Lifetime = lifetime + fuseDelay;
            fuse = fuseDelay;
            burstTint = burstColor;
            return this;
        }

        public override void Reset() {
            base.Reset();
            fuse = 0;
            burstTint = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            //引信期:加载器每帧先行应用速度,必须暂存清零否则蓄势期就在漂移
            if (fuse > 0 && Time <= fuse) {
                if (Time == 1) {
                    ai[1] = Velocity.X;
                    ai[2] = Velocity.Y;
                    Velocity = Vector2.Zero;
                }
                if (Time == fuse) {
                    Velocity = new Vector2(ai[1], ai[2]);
                }
                if (Time < fuse) {
                    return;
                }
            }

            //复合加速:越飞越急,像被天空吸走
            Velocity *= 1.045f;
            Velocity.X *= 0.985f;

            //沿途湿雾丝
            if (Time % 5 == 0) {
                PRTLoader.NewParticle<PRT_SvcCloud>(Position, -Velocity * 0.06f,
                    new Color(190, 210, 235), Main.rand.NextFloat(0.10f, 0.18f))?.Configure(34);
            }
            Lighting.AddLight(Position, Color.ToVector3() * 0.4f);

            //到点炸开成云;AI 在 Time==Lifetime 那帧仍会跑,== 判定防双爆
            if (Time == Lifetime - 1) {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(1.6f, 0.9f) - new Vector2(0f, 0.3f);
                    PRTLoader.NewParticle<PRT_SvcCloud>(Position + Main.rand.NextVector2Circular(10f, 6f),
                        vel, burstTint, Main.rand.NextFloat(0.22f, 0.42f))?.Configure(Main.rand.Next(70, 110), 0.0026f);
                }
                PRTLoader.NewParticle<PRT_Light>(Position, Vector2.Zero, Color, 0.24f)?.Configure(16, 0.9f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Time < fuse) {
                return false;
            }
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //速度拉伸:运动体必须有方向性
            float stretch = MathHelper.Clamp(Velocity.Length() / 7f, 1f, 2.6f);
            float rot = Velocity.ToRotation() + MathHelper.PiOver2;
            Vector2 scale = new(Scale * 0.6f, Scale * stretch);
            Vector2 pos = Position - Main.screenPosition;

            //PRT 加色批是真 BlendState.Additive(源因子=SourceAlpha):A=0 什么都画不出,
            //亮度走 Color * x 让 A 随强度;SoftGlow 黑底在加色批天然无黑边
            spriteBatch.Draw(tex, pos, null, Color * 0.75f, rot, tex.Size() * 0.5f, scale * 1.7f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color.White * 0.9f, rot, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
