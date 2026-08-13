using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering
{
    /// <summary>
    /// 蜂蜜黏滴：重力抛物+速度拉伸，触地压扁成渍再淡出<br/>
    /// Extra_98 真 alpha，AlphaBlend 染琥珀色
    /// </summary>
    internal class PRT_HoneyDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        //ai[0]=1 已触地成渍
        private float splatTimer;

        public override void SetProperty() {
            Lifetime = 46;
            initialColor = Color;
            splatTimer = 0f;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            splatTimer = 0f;
        }

        public override void AI() {
            if (ai[0] == 1f) {
                //贴地蜜渍，缓慢淡出
                Velocity = Vector2.Zero;
                splatTimer++;
                Color = initialColor * MathHelper.Clamp(1f - splatTimer / 26f, 0f, 1f) * 0.85f;
                if (splatTimer > 26f) {
                    Time = Lifetime;
                }
                return;
            }

            Velocity.X *= 0.988f;
            Velocity.Y += 0.34f;
            if (Velocity.Y > 15f) {
                Velocity.Y = 15f;
            }

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 2.6f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //触地转蜜渍
            if (Terraria.Collision.SolidCollision(Position - new Vector2(3f, 3f), 6, 6)) {
                ai[0] = 1f;
                Time = 0;
                Lifetime = 30;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            if (ai[0] == 1f) {
                //扁平蜜渍
                spriteBatch.Draw(tex, pos, null, Color, 0f, origin,
                    new Vector2(0.6f, 0.16f) * Scale, SpriteEffects.None, 0f);
                return false;
            }

            //快成线、慢成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.3f * (1f - stretch * 0.35f), 0.55f * (1f + stretch * 1.8f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //高光芯
            spriteBatch.Draw(tex, pos, null, Color * 0.7f, Rotation, origin, scale * new Vector2(0.42f, 0.9f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 蜜雾软团：Fog 单帧真 alpha，随机旋转+镜像防贴纸感，缓升缓散
    /// </summary>
    internal class PRT_HoneyMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private Color initialColor;
        private float spin;

        public override void SetProperty() {
            Lifetime = 42;
            initialColor = Color;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            ai[0] = Main.rand.Next(2);
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
        }

        public override void AI() {
            Velocity *= 0.96f;
            Rotation += spin;
            Scale += 0.008f;
            float t = LifetimeCompletion;
            //先浮现后消散
            float fade = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            Color = initialColor * fade;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            SpriteEffects flip = ai[0] == 1f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, Scale * 0.5f, flip, 0f);
            return false;
        }
    }

    /// <summary>
    /// 蜂蜡碎屑：哑光小片，翻滚坠落，炮台生灭时迸出
    /// </summary>
    internal class PRT_WaxChip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float spin;

        public override void SetProperty() {
            Lifetime = 38;
            initialColor = Color;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.24f, 0.24f);
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
        }

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y += 0.3f;
            Rotation += spin;
            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 3f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //扁片双层错缩出体积
            Vector2 scale = new Vector2(0.42f, 0.2f) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.6f, Rotation + 0.5f,
                tex.Size() * 0.5f, scale * 0.7f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 蜂翅微光：加色小闪点，编队蜂身上零星冒出，卖出蜂群整体的金尘质感
    /// </summary>
    internal class PRT_BeeGlint : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Sparkle";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 3000;

        private Color initialColor;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = 20;
            initialColor = Color;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void AI() {
            Velocity *= 0.93f;
            float t = LifetimeCompletion;
            //0→1→0 脉冲
            float pulse = (float)Math.Sin(t * MathHelper.Pi);
            Color = initialColor * pulse;
            Scale *= 0.985f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, Scale * 0.22f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
