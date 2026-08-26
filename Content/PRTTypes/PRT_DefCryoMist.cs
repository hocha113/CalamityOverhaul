using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 冰冻塔寒雾:绕塔缓慢旋绕的冷雾层,Masking/Fog 真 alpha 直接染冷色。
    /// 轨道运动+半径微呼吸,淡入淡出,读作被寒场搅动的低温雾
    /// </summary>
    internal class PRT_DefCryoMist : BasePRT
    {
        public override int InGame_World_MaxCount => 60;
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private Color initialColor;
        private Vector2 orbitCenter;
        private float orbitRadius;
        private float orbitAngle;
        private float orbitSpeed;
        private float breathPhase;
        private SpriteEffects mirror;
        private float spin;

        /// <param name="lifetime">存活帧数</param>
        /// <param name="center">轨道心(塔心)</param>
        /// <param name="radius">轨道半径</param>
        public PRT_DefCryoMist Configure(int lifetime, Vector2 center, float radius) {
            Lifetime = lifetime;
            initialColor = Color;
            orbitCenter = center;
            orbitRadius = radius;
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            //内圈略快,方向随机:雾层不齐步走
            orbitSpeed = Main.rand.NextFloat(0.004f, 0.010f) * (Main.rand.NextBool() ? 1f : -1f);
            breathPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            mirror = Main.rand.NextBool() ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.008f, 0.008f);
            Position = center + orbitAngle.ToRotationVector2() * radius;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            orbitCenter = default;
            orbitRadius = 0f;
            orbitAngle = 0f;
            orbitSpeed = 0f;
            breathPhase = 0f;
            mirror = SpriteEffects.None;
            spin = 0f;
        }

        public override void AI() {
            orbitAngle += orbitSpeed;
            Rotation += spin;
            float t = LifetimeCompletion;
            //半径微呼吸:雾被寒场搅动
            float radius = orbitRadius * (1f + 0.05f * MathF.Sin(t * 9f + breathPhase));
            Position = orbitCenter + orbitAngle.ToRotationVector2() * radius;
            Velocity = Vector2.Zero;

            //出生1/4淡入,尾程淡出
            float env = MathF.Min(t / 0.25f, 1f) * MathF.Pow(1f - t, 1.1f);
            Color = initialColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, tex.Size() * 0.5f, Scale, mirror, 0f);
            return false;
        }
    }
}
