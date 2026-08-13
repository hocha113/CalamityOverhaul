using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering
{
    /// <summary>阴魂火舌：速度拉伸、冷却降火势、微浮升；绘制委托冷焰顶点批（无灰度图本体）</summary>
    internal class PRT_SkeleGhostFlame : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float drift;
        private float seed;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            drift = Main.rand.NextFloat(-0.02f, 0.02f);
            seed = Main.rand.NextFloat();
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
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            //本体是冷焰顶点quad：焰轴沿运动方向，速度拉伸焰高，寿命末段降火势
            float flick = 0.82f + 0.24f * ((Main.GameUpdateCount * 7 + (int)(seed * 97)) % 5) / 4f;
            float stretch = MathHelper.Clamp(Velocity.Length() / 7f, 0.2f, 1.7f);
            float cooling = 1f - MathHelper.Clamp(LifetimeCompletion * 1.2f, 0f, 0.85f);
            Vector2 size = new Vector2(Scale * 11f * flick, Scale * (13f + 9f * stretch));
            //焰根压到粒子后方，尖端指向前进方向
            float axis = Rotation - MathHelper.PiOver2;
            Vector2 root = Position - axis.ToRotationVector2() * size.Y * 0.35f;
            SkeletronFlameRender.Push(root, axis, size,
                0.35f + 0.6f * cooling, seed, 0.12f + (1f - cooling) * 0.3f,
                Opacity * 0.85f);
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
