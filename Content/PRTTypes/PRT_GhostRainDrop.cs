using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.Scenarios.Kiame;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼雨主雨滴：快成丝、慢成珠，触地转短暂扁溅后熄。
    /// 大范围重启倒带时倒飞；地面重生滴先以反向扁溅收拢成珠再升空。
    /// Extra_98 真 alpha，非加色，无光晕。
    /// </summary>
    internal class PRT_GhostRainDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 720;

        /// <summary>反向扁溅时长：摊开的水光收拢成珠的帧数</summary>
        private const int RebirthDuration = 8;

        /// <summary>默认下落终速（像素/帧）</summary>
        private const float DefaultTerminalFall = 16.5f;

        private Color initialColor;
        private float windX;
        private bool splashing;
        private int splashTicks;
        private int rebirthTicks;
        //满幕雨帘滴标记：近玩家淡出保战斗可读；溅花/沸泡等点缀不标
        private bool nearClear;
        //下落终速：远幕雨滴给更低终速，慢而小才读得出纵深
        private float terminalFall = DefaultTerminalFall;

        public PRT_GhostRainDrop Configure(int lifetime, float wind) {
            Lifetime = lifetime;
            windX = wind;
            initialColor = Color;
            return this;
        }

        /// <summary>标记为雨帘滴：贴近本地玩家的滴淡出，雨仍在、脸前留可读窗。
        /// depthMul&lt;1 时按远幕处理，终速同乘（配合出生时的小尺寸低透明度读作远处雨墙）</summary>
        public PRT_GhostRainDrop AsCurtain(float depthMul = 1f) {
            nearClear = true;
            terminalFall = DefaultTerminalFall * depthMul;
            return this;
        }

        /// <summary>倒带地面重生入场：钉在地表先收拢 <see cref="RebirthDuration"/> 帧，随后升空</summary>
        public PRT_GhostRainDrop BeginRebirth() {
            rebirthTicks = RebirthDuration;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            windX = 0f;
            splashing = false;
            splashTicks = 0;
            rebirthTicks = 0;
            nearClear = false;
            terminalFall = DefaultTerminalFall;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 100;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //重启倒带（大范围重启 / 鬼雨死亡重启）：雨滴倒着往回飞，
            //玩家对比雨的轨迹立刻读出时间在倒流
            bool kiameRewind = KiameWake.RainRewindActive;
            if (KikasaReset.RainRewindActive || kiameRewind) {
                splashing = false;
                splashTicks = 0;
                Color = initialColor;
                //生命同步倒流，倒带期间不熄
                if (Time > 1) {
                    Time--;
                }
                //反向扁溅重生：摊开的水光先收拢成珠，收完才起飞
                if (rebirthTicks > 0) {
                    rebirthTicks--;
                    Velocity = Vector2.Zero;
                    return;
                }
                //上飞速度吃回卷脉冲：拍点上全速抽回，拍间保留缓升，与世界同节奏
                float pulse = kiameRewind
                    ? KiameWake.RewindPulseRate : KikasaReset.RewindPulseRate;
                Velocity.X = -windX;
                Velocity.Y = MathHelper.Lerp(Velocity.Y, -6f - 12f * pulse, 0.14f);
                if (Velocity.LengthSquared() > 0.25f) {
                    Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
                }
                return;
            }

            //倒带窗口外还没收拢完的重生滴：就地转扁溅淡出
            if (rebirthTicks > 0) {
                rebirthTicks = 0;
                splashing = true;
                splashTicks = 2;
                Velocity = Vector2.Zero;
            }

            if (splashing) {
                Velocity = Vector2.Zero;
                splashTicks++;
                Color = Color.Lerp(initialColor, Color.Transparent, splashTicks / 8f);
                if (splashTicks >= 8) {
                    active = false;
                }
                return;
            }

            Velocity.X = windX;
            Velocity.Y = Math.Min(Velocity.Y + 0.5f, terminalFall);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //触地转扁溅
            if (Velocity.Y > 2f && Collision.SolidCollision(Position - new Vector2(1f, 1f), 2, 2)) {
                splashing = true;
                return;
            }

            float t = LifetimeCompletion;
            if (t > 0.85f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.85f) / 0.15f);
            }
        }

        //雨帘滴的近玩家淡出：150px 内压到 34%，380px 外全强。
        //雨没有停，只是脸前这一层退成焦外，可读窗随人走

        private float NearFade() {
            if (!nearClear) {
                return 1f;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return 1f;
            }
            float dist = Vector2.Distance(Position, player.Center);
            return MathHelper.Lerp(0.34f, 1f,
                MathHelper.Clamp((dist - 150f) / 230f, 0f, 1f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float fade = NearFade();

            if (rebirthTicks > 0) {
                //反向扁溅：横向水光随剩余时收拢回珠形，透明度渐升
                float k = rebirthTicks / (float)RebirthDuration;
                Vector2 scale = new Vector2(0.36f * (1f + k * 1.4f), 0.09f) * Scale;
                spriteBatch.Draw(tex, pos, null, Color * ((1f - 0.55f * k) * fade),
                    0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            if (splashing) {
                //扁溅：横向摊开的一小片水光
                float k = splashTicks / 8f;
                Vector2 scale = new Vector2(0.36f * (1f + k * 1.4f), 0.09f) * Scale;
                spriteBatch.Draw(tex, pos, null, Color * fade, 0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            //快成丝、慢成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.055f, 0f, 1f);
            Vector2 body = new Vector2(0.13f * (1f - stretch * 0.35f),
                0.42f * (1f + stretch * 2.4f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color * fade, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * (0.6f * fade), Rotation, origin,
                body * new Vector2(0.45f, 1.06f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
