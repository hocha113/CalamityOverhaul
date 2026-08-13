using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering
{
    /// <summary>凝胶珠，重力弧线+速度拉伸，真alpha半透明</summary>
    internal class PRT_BKSGelBead : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 320;

        private Color initialColor;
        private float gravity;
        private float drag;

        public PRT_BKSGelBead Configure(int lifetime, float gravityPerFrame = 0.34f, float dragMul = 0.988f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            drag = dragMul;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            drag = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 40);
            }
            if (gravity <= 0f) {
                gravity = 0.34f;
            }
            if (drag <= 0f || drag > 1f) {
                drag = 0.988f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= drag;
            Velocity.Y += gravity;
            if (Velocity.Y > 15f) {
                Velocity.Y = 15f;
            }

            float t = LifetimeCompletion;
            Scale *= 0.987f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 2.6f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //快成线、慢成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.4f * (1f - stretch * 0.3f), 0.6f * (1f + stretch * 1.6f)) * Scale;

            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //内芯略亮，凝胶厚度感
            spriteBatch.Draw(tex, pos, null, Color * 0.8f, Rotation, origin, scale * new Vector2(0.5f, 0.92f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>贴地凝胶渍，飞行→贴住铺开→缓沉→消退</summary>
    internal class PRT_BKSGelSplat : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private bool stuck;
        private Color initialColor;
        private float gravity;
        private int stickLife;
        private int stuckTimer;
        private float stuckScale;

        public PRT_BKSGelSplat Configure(int flyLifetime, float gravityPerFrame = 0.4f, int stuckLifetime = 50) {
            Lifetime = flyLifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            stickLife = Math.Max(16, stuckLifetime);
            return this;
        }

        public override void Reset() {
            base.Reset();
            stuck = false;
            initialColor = default;
            gravity = 0f;
            stickLife = 0;
            stuckTimer = 0;
            stuckScale = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(28, 44);
            }
            if (gravity <= 0f) {
                gravity = 0.4f;
            }
            if (stickLife <= 0) {
                stickLife = Main.rand.Next(40, 60);
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override bool ShouldUpdatePosition() => !stuck;

        public override void AI() {
            if (stuck) {
                //贴住：铺开后缓慢下沉消退
                stuckTimer++;
                float t = stuckTimer / (float)stickLife;
                //前20%继续铺开(overshoot 回落)
                stuckScale = t < 0.2f ? MathHelper.Lerp(1f, 1.35f, t / 0.2f) : MathHelper.Lerp(1.35f, 1.1f, (t - 0.2f) / 0.8f);
                Position.Y += 0.08f;
                Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 1.6f));
                if (stuckTimer >= stickLife) {
                    active = false;
                }
                //贴住期不再走 Lifetime 计数
                Time = 0;
                return;
            }

            Velocity.X *= 0.985f;
            Velocity.Y += gravity;
            if (Velocity.Y > 15f) {
                Velocity.Y = 15f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 3f) * 0.4f);

            //碰实心方块即贴住
            if (Terraria.Collision.SolidCollision(Position - new Vector2(2f, 2f), 4, 4)) {
                stuck = true;
                //贴面姿态：横向铺开
                Rotation = 0f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            if (stuck) {
                //扁平渍块
                Vector2 scale = new Vector2(0.85f * stuckScale, 0.3f) * Scale;
                spriteBatch.Draw(tex, pos, null, Color, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos + new Vector2(0f, 1.5f), null, Color * 0.6f, 0f, origin, scale * new Vector2(0.7f, 0.8f), SpriteEffects.None, 0f);
                return false;
            }

            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.85f);
            Vector2 flyScale = new Vector2(0.45f * (1f - stretch * 0.3f), 0.62f * (1f + stretch * 1.5f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, flyScale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>凝胶气泡，上浮减速，末帧胀破；薄锐缘壳圈承形（Ring01 灰度图已禁用，见 VFX.md）</summary>
    internal class PRT_BKSBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private Color initialColor;

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 38);
            }
            initialColor = Color;
        }

        public override void AI() {
            Velocity *= 0.96f;
            Velocity.Y -= 0.05f;

            float t = LifetimeCompletion;
            //末10%胀破：急速放大并消失
            if (t > 0.9f) {
                Scale *= 1.12f;
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.9f) / 0.1f);
            }
            else {
                Scale *= 1.004f;
                Color = initialColor * (0.85f - t * 0.3f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            //可见泡径与旧 Ring01 对齐：Ring01 0.83R/128px → DiffusionCircle4 0.95R/156px，0.06 → 0.043
            spriteBatch.Draw(tex, pos, null, Color, 0f, tex.Size() * 0.5f, Scale * 0.043f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>王冠金屑，加色小星</summary>
    internal class PRT_BKSGoldSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Sparkle";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 220;

        private Color initialColor;
        private bool gravity;

        public PRT_BKSGoldSpark Configure(int lifetime, bool affectedByGravity = false) {
            Lifetime = lifetime;
            gravity = affectedByGravity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 30);
            }
            initialColor = Color;
        }

        public override void AI() {
            Velocity *= 0.93f;
            if (gravity) {
                Velocity.Y += 0.18f;
            }
            float t = LifetimeCompletion;
            Color = initialColor * (1f - t);
            Rotation += 0.12f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            float stretch = 1f + MathHelper.Clamp(Velocity.Length() * 0.06f, 0f, 0.8f);
            spriteBatch.Draw(tex, pos, null, Color, Rotation, tex.Size() * 0.5f,
                new Vector2(Scale * 0.3f, Scale * 0.3f * stretch), SpriteEffects.None, 0f);
            return false;
        }
    }
}
