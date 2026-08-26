using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 噬灯魂的灯魂珠（C2 专用）：被吞掉的那口「光」本身。
    /// 两种活法：给 target 就加速追体（周围的光被拉进妖火——倒吸/进食演出），
    /// 到体即灭；不给 target 就减速上漂渐隐（死亡时吐还的灯魂逸散）。
    /// 画法=拉长光streak+圆芯，暖灯金；珠是「光的实体」不是火星，永远朝运动方向拉长。
    /// </summary>
    internal class PRT_LampeaterLightMote : BasePRT
    {
        public Color InitialColor;
        public Entity Target;
        /// <summary>追体加速度（px/f²）</summary>
        public float Homing;

        public override int InGame_World_MaxCount => 400;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Extra_98";

        public PRT_LampeaterLightMote Configure(int lifetime, Entity target = null, float homing = 0.5f) {
            InitialColor = Color;
            Lifetime = lifetime;
            Target = target;
            Homing = homing;
            //每粒独立明闪相位（ID 是类型全局量不能当相位；纯表现，客户端掷点无碍）
            ai[0] = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            Target = null;
            Homing = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            if (Target != null && Target.active) {
                //追体：加速吸向目标，贴近即被吞（寿命直接烧完）
                Vector2 to = Target.Center - Position;
                float dist = to.Length();
                if (dist < 14f) {
                    Time = Lifetime;
                    return;
                }
                Velocity += to / Math.Max(dist, 1f) * Homing;
                float cap = 3f + Homing * 14f;
                if (Velocity.Length() > cap) {
                    Velocity = Velocity.SafeNormalize(Vector2.Zero) * cap;
                }
                Color = InitialColor * (0.65f + 0.35f * MathF.Sin(Time * 0.7f + ai[0]));
            }
            else {
                //逸散：减速上漂，光慢慢散进黑暗里
                Velocity *= 0.965f;
                Velocity.Y -= 0.02f;
                Color = InitialColor * (1f - LifetimeCompletion);
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.16f, 0.4f, 2.4f);
            //拉长光痕 + 圆芯（芯偏暖白：珠心比痕亮）
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.75f, Rotation,
                tex.Size() * 0.5f, new Vector2(0.30f, 0.55f + stretch) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null,
                Color.Lerp(Color, new Color(255, 245, 220), 0.45f), Rotation,
                tex.Size() * 0.5f, new Vector2(0.28f, 0.34f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
