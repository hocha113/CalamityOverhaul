using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering
{
    /// <summary>阴魂火舌：速度拉伸、幽青→深青冷却、微浮升、尖端撕闪</summary>
    internal class PRT_SkeleGhostFlame : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        private float drift;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            drift = Main.rand.NextFloat(-0.02f, 0.02f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 44);
            }
        }

        /// <summary>lifetime 帧数；buoyancy 上浮加速度</summary>
        public PRT_SkeleGhostFlame Configure(int lifetime, float buoyancy = 0.045f) {
            Lifetime = lifetime;
            ai[0] = buoyancy;
            return this;
        }

        public override void AI() {
            Velocity *= 0.93f;
            Velocity += new Vector2(0f, -ai[0]);
            Rotation = Rotation.AngleLerp(Velocity.ToRotation() + MathHelper.PiOver2, 0.3f) + drift;
            Opacity = (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
            //冷却：亮青→深青
            Color = Color.Lerp(SkeletronRenderHelper.GhostCyan, SkeletronRenderHelper.GhostDeep,
                MathHelper.Clamp(LifetimeCompletion * 1.35f, 0f, 1f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            //高频闪变：帧哈希抖尺度
            float flick = 0.82f + 0.24f * ((Main.GameUpdateCount * 7 + (int)(ai[1] * 97)) % 5) / 4f;
            float stretch = MathHelper.Clamp(Velocity.Length() / 7f, 0.2f, 1.7f);
            Vector2 scale = new Vector2(Scale * 0.32f * flick, Scale * (0.34f + 0.22f * stretch));
            Vector2 orig = new Vector2(tex.Width / 2f, tex.Height * 0.92f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * (Opacity * 0.85f),
                Rotation, orig, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>骨屑碎片：翻滚坠落，用原版骨头贴图，尾段褪淡</summary>
    internal class PRT_SkeleBoneChip : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float spin;
        private float shade;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            spin = Main.rand.NextFloat(-0.24f, 0.24f);
            shade = Main.rand.NextFloat(0.55f, 1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(42, 88);
            }
        }

        /// <summary>gravity 重力加速度</summary>
        public PRT_SkeleBoneChip Configure(int lifetime, float gravity = 0.30f) {
            Lifetime = lifetime;
            ai[0] = gravity;
            return this;
        }

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.985f, Velocity.Y + ai[0]);
            if (Velocity.Y > 14f) {
                Velocity = new Vector2(Velocity.X, 14f);
            }
            Rotation += spin * MathHelper.Clamp(Velocity.Length() / 6f, 0.3f, 1.4f);
            Opacity = MathHelper.Clamp((1f - LifetimeCompletion) * 4f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Main.instance.LoadProjectile(ProjectileID.Bone);
            Texture2D bone = TextureAssets.Projectile[ProjectileID.Bone].Value;
            Color lit = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            Color col = Color.Lerp(SkeletronRenderHelper.BoneShadow, SkeletronRenderHelper.BonePale, shade)
                .MultiplyRGB(lit) * Opacity;
            spriteBatch.Draw(bone, Position - Main.screenPosition, null, col, Rotation,
                bone.Size() / 2f, Scale * 0.8f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
