using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant
{
    /// <summary>
    /// 蒸郁湿雾：贴地湿热薄雾，Masking/Fog 真 alpha，AlphaBlend 直绘。
    /// 血统同 PRT_GhostRainMist，色板与阻尼按丛林湿气调；就地放本槽位文件夹防撞名
    /// </summary>
    internal class PRT_VerdantMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 110;

        private Color initialColor;
        private float drift;

        public PRT_VerdantMist Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            drift = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            drift = Main.rand.NextFloat(-0.005f, 0.005f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 150;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //均匀弱阻尼：凝聚流保留向心速度，常态雾则自然滞缓
            Velocity *= 0.992f;
            Rotation += drift;
            Scale += 0.0018f;

            float t = LifetimeCompletion;
            float envelope = MathF.Sin(MathHelper.Pi * t);
            Color = initialColor * (0.30f * envelope);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 孢子浮尘：微小加色光点缓沉缓飘，随风微移，亮度轻微明灭。
    /// 尺寸压在数像素级，光点只当"尘"不当"效果本体"
    /// </summary>
    internal class PRT_VerdantSpore : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 90;

        private Color initialColor;
        private float flickerPhase;

        public override void Reset() {
            base.Reset();
            initialColor = default;
            flickerPhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = Main.rand.Next(140, 220);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            initialColor = Color == default ? new Color(168, 198, 118) : Color;
        }

        public override void AI() {
            //缓沉 + 向风速靠拢
            Velocity.Y = MathF.Min(Velocity.Y + 0.004f, 0.32f);
            Velocity.X = MathHelper.Lerp(Velocity.X, Main.windSpeedCurrent * 0.55f, 0.012f);

            float t = LifetimeCompletion;
            float envelope = MathF.Sin(MathHelper.Pi * t);
            float flicker = 0.82f + 0.18f * MathF.Sin(Time * 0.11f + flickerPhase);
            Color = initialColor * (0.42f * envelope * flicker);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, 0f,
                tex.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 夜萤：一点会呼吸的暖绿光。材质三签名：点光、明灭节律、漫游转向。
    /// 双层绘制=晕(大而淡)+芯(小而亮)，光点即萤火虫本体故不违裸光球禁令
    /// </summary>
    internal class PRT_VerdantFirefly : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 46;

        private Color initialColor;
        private float bright;
        private int blinkClock;
        private int offDur;
        private int steerClock;
        private Vector2 steerDir;

        public override void Reset() {
            base.Reset();
            initialColor = default;
            bright = 0f;
            blinkClock = 0;
            offDur = 0;
            steerClock = 0;
            steerDir = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = Main.rand.Next(300, 460);
            initialColor = Color == default ? new Color(198, 228, 96) : Color;
            offDur = Main.rand.Next(26, 70);
            blinkClock = Main.rand.Next(offDur + 20);
            steerClock = 0;
        }

        public override void AI() {
            //明灭节律：亮窗-暗窗交替，亮度软过渡
            if (--blinkClock <= 0) {
                offDur = Main.rand.Next(26, 70);
                blinkClock = offDur + Main.rand.Next(22, 46);
            }
            bool lit = blinkClock > offDur;
            bright = MathHelper.Lerp(bright, lit ? 1f : 0.06f, lit ? 0.14f : 0.09f);

            //漫游：周期换向的缓慢转向，轻微向上偏置
            if (--steerClock <= 0) {
                steerClock = Main.rand.Next(40, 75);
                steerDir = Main.rand.NextVector2Unit();
                steerDir.Y -= 0.18f;
            }
            Velocity += steerDir * 0.016f;
            if (Velocity.LengthSquared() > 0.55f) {
                Velocity *= 0.94f;
            }

            float t = LifetimeCompletion;
            float envelope = MathF.Min(t / 0.12f, 1f) * MathHelper.Clamp((1f - t) / 0.2f, 0f, 1f);
            Color = initialColor * (envelope * bright);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //晕层
            spriteBatch.Draw(tex, pos, null, Color * 0.20f, 0f, origin, Scale * 2.7f, SpriteEffects.None, 0f);
            //芯
            spriteBatch.Draw(tex, pos, null, Color, 0f, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 花粉团：艳阳下随风飘过的柔金光尘，纯视觉。
    /// 穿过玩家时在其身上洒少量花粉附身粒子（纯装饰，无任何数值效果）
    /// </summary>
    internal class PRT_VerdantPollen : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 70;

        private Color initialColor;
        private float bobPhase;
        private int brushClock;

        public override void Reset() {
            base.Reset();
            initialColor = default;
            bobPhase = 0f;
            brushClock = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = Main.rand.Next(250, 380);
            bobPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            initialColor = Color == default ? new Color(232, 204, 112) : Color;
        }

        public override void AI() {
            //随风推进 + 上下轻浮
            Velocity.X = MathHelper.Lerp(Velocity.X, Main.windSpeedCurrent * 3.2f, 0.015f);
            Velocity.Y = MathF.Sin(Time * 0.045f + bobPhase) * 0.22f;

            float t = LifetimeCompletion;
            float envelope = MathF.Min(t / 0.15f, 1f) * MathHelper.Clamp((1f - t) / 0.22f, 0f, 1f);
            float shimmer = 0.85f + 0.15f * MathF.Sin(Time * 0.16f + bobPhase * 2f);
            Color = initialColor * (0.5f * envelope * shimmer);

            //擦身：低频检查身边玩家，落一点花粉附身粒
            if (--brushClock > 0) {
                return;
            }
            brushClock = 8;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.active || player.dead
                    || Vector2.DistanceSquared(player.Center, Position) > 46f * 46f) {
                    continue;
                }
                Dust dust = Dust.NewDustDirect(player.position, player.width, player.height,
                    DustID.JungleTorch, 0f, -0.3f, 160, default, 0.7f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color * 0.24f, 0f, origin, Scale * 2.4f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color, 0f, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
