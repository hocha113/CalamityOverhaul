using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 引路鬼火：中性单只冷白焰，夜雾中亮起，缓缓飘向最近的洞口或地表开阔处。
    /// 只照明不伤害，不逼近玩家（240px 内反向让开）；到达目标后驻留一会儿自行熄灭。
    /// 材质=冷焰：SoulFire 五帧火体、时域高频闪变、烬屑上浮、冷白蓝光晕。
    /// </summary>
    internal class PRT_WoodsongWisp : BasePRT
    {
        public override string Texture => CWRConstant.Other + "SoulFire";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4;

        private static readonly Color HaloCold = new(86, 124, 186, 0);
        private static readonly Color BodyDim = new(150, 196, 238);
        private static readonly Color BodyLit = new(190, 220, 248);
        private static readonly Color CoreWhite = new(222, 238, 255, 0);

        /// <summary>巡航速度（px/tick），"缓缓"是身份，不许加速冲刺</summary>
        private const float CruiseSpeed = 0.62f;
        /// <summary>对玩家的让避半径</summary>
        private const float AvoidRadius = 240f;

        /// <summary>心跳：调度器用它判断场上是否已有活鬼火（单只上限）</summary>
        internal static uint LastBeat;

        private Vector2 target;
        private float phase;
        private int emberIn;
        private float flick;

        public PRT_WoodsongWisp Configure(Vector2 driftTarget, int lifetime) {
            target = driftTarget;
            Lifetime = lifetime;
            return this;
        }

        /// <summary>近 3 tick 内有活体心跳（LastBeat=0 视为无）</summary>
        internal static bool AliveRecently =>
            LastBeat != 0 && Main.GameUpdateCount >= LastBeat && Main.GameUpdateCount - LastBeat <= 3;

        public override void Reset() {
            base.Reset();
            target = default;
            phase = 0f;
            emberIn = 0;
            flick = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            phase = Main.rand.NextFloat(100f);
            emberIn = 30;
            if (Lifetime <= 0) {
                Lifetime = 1200;
            }
        }

        public override void AI() {
            LastBeat = Main.GameUpdateCount;
            float lc = LifetimeCompletion;
            float env = MathHelper.Clamp(lc / 0.08f, 0f, 1f) * MathHelper.Clamp((1f - lc) / 0.12f, 0f, 1f);

            //双正弦伪噪声闪变：冷焰的时域签名
            flick = 0.85f + 0.15f * MathF.Sin(Time * 0.31f + phase) * MathF.Sin(Time * 0.117f + phase * 1.7f);

            //五帧火焰循环（与霭祭同节拍，本体贴图共享）
            if (++ai[0] > 6f) {
                ai[0] = 0f;
                if (++ai[1] > 4f) {
                    ai[1] = 0f;
                }
            }
            Rotation = MathF.Sin(Time * 0.09f + phase) * 0.10f;

            //到达目标附近改为驻留，并把余命压到谢幕段
            Vector2 toTarget = target - Position;
            Vector2 desired;
            if (toTarget.Length() < 70f) {
                desired = Vector2.Zero;
                if (Lifetime - Time > 150) {
                    Lifetime = Time + 150;
                }
            }
            else {
                desired = toTarget.SafeNormalize(Vector2.UnitX) * CruiseSpeed;
            }

            //不逼近玩家：让避力随距离衰减
            Player local = Main.LocalPlayer;
            if (local != null && local.active) {
                Vector2 away = Position - local.Center;
                float dist = away.Length();
                if (dist < AvoidRadius && dist > 0.01f) {
                    desired += away / dist * (1f - dist / AvoidRadius) * 0.9f;
                }
            }

            //缓慢转向+轻微浮沉
            Velocity = Vector2.Lerp(Velocity, desired, 0.02f);
            Velocity.Y += MathF.Sin(Time * 0.05f + phase) * 0.012f;

            Lighting.AddLight(Position, 0.24f * env * flick, 0.32f * env * flick, 0.46f * env * flick);

            //烬屑上浮：焰体的物质代谢
            if (--emberIn <= 0) {
                emberIn = 30 + Main.rand.Next(26);
                PRTLoader.NewParticle<PRT_WoodsongMote>(Position + Main.rand.NextVector2Circular(4f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.3f),
                    new Color(170, 205, 240) * 0.6f, Main.rand.NextFloat(0.045f, 0.065f))
                    ?.Configure(PRT_WoodsongMote.ModeWispEmber, Main.rand.Next(32, 48));
            }

            Opacity = env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.01f) {
                return false;
            }
            Texture2D flame = TexValue;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 pos = Position - Main.screenPosition;
            float a = Opacity * flick;
            Rectangle frameRect = flame.GetRectangle((int)ai[1], 5);
            Vector2 frameOrigin = frameRect.Size() * 0.5f;

            //光晕衬底（黑底图 A=0，占比压在三成以内）
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, HaloCold * (a * 0.30f), 0f,
                    glow.Size() * 0.5f, 0.62f, SpriteEffects.None, 0f);
            }

            Color body = Color.Lerp(BodyDim, BodyLit, flick) * a;
            spriteBatch.Draw(flame, pos, frameRect, body, Rotation, frameOrigin,
                Scale, SpriteEffects.None, 0f);

            //冷白芯：A=0 加色点亮贴图白芯，不用第二张灰度舌
            spriteBatch.Draw(flame, pos, frameRect, CoreWhite * (a * 0.55f), Rotation,
                frameOrigin, Scale * 0.72f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
