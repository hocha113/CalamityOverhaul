using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>土鱼环游的干土陶土调色板与迸发帮手</summary>
    internal static class FishDirtVFX
    {
        public static readonly Color SoilLight = new(176, 130, 92);
        public static readonly Color SoilMid = new(140, 100, 70);
        public static readonly Color SoilDark = new(98, 68, 47);
        public static readonly Color SoilDeep = new(64, 44, 31);
        public static readonly Color DustWarm = new(158, 120, 88);
        public static readonly Color DustFade = new(102, 80, 62);
        public static readonly Color PebbleGray = new(129, 116, 103);
        public static readonly Color TrackBrown = new(66, 47, 34);

        /// <summary>按世界光照调制哑光颜色</summary>
        public static Color Lit(Vector2 worldPos, Color c) {
            Color l = Lighting.GetColor(worldPos.ToTileCoordinates());
            return new Color(c.R * l.R / 255, c.G * l.G / 255, c.B * l.B / 255, c.A);
        }

        /// <summary>亮度缩放，保持不透明</summary>
        public static Color Shade(Color c, float k)
            => new((int)(c.R * k), (int)(c.G * k), (int)(c.B * k), c.A);

        private static Color RandClod() => Main.rand.Next(3) switch {
            0 => SoilLight,
            1 => SoilMid,
            _ => SoilDark,
        };

        /// <summary>干土碎屑，受重力短命剥落，lifetime 0 走默认随机寿命</summary>
        public static void Crumb(Vector2 pos, Vector2 vel, float scale, int lifetime = 0) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishDirtCrumb>(pos, vel, Lit(pos, RandClod()), scale)
                ?.Configure(lifetime > 0 ? lifetime : Main.rand.Next(16, 26));
        }

        /// <summary>哑光尘团，lifetime 0 走默认，opacityMul 压峰值防糊屏</summary>
        public static void Puff(Vector2 pos, Vector2 vel, float scale, int lifetime = 0, float opacityMul = 1f) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishDirtPuff>(pos, vel, Lit(pos, DustWarm), scale)
                ?.Configure(lifetime > 0 ? lifetime : Main.rand.Next(22, 32), Lit(pos, DustFade), opacityMul);
        }

        /// <summary>碎石</summary>
        public static void Pebble(Vector2 pos, Vector2 vel, float scale) {
            if (Main.dedServ) {
                return;
            }
            Color c = Color.Lerp(PebbleGray, SoilMid, Main.rand.NextFloat(0.55f));
            PRTLoader.NewParticle<PRT_FishDirtCrumb>(pos, vel, Lit(pos, c), scale)
                ?.Configure(Main.rand.Next(30, 44), 0.38f, bounce: true);
        }

        /// <summary>滚痕</summary>
        public static void Track(Vector2 pos, float scale) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishDirtTrack>(pos, Vector2.Zero, Lit(pos, TrackBrown), scale)
                ?.Configure(Main.rand.Next(30, 44));
        }
    }

    /// <summary>干土碎屑</summary>
    internal class PRT_FishDirtCrumb : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float gravity;
        private float spin;
        private bool canBounce;
        private bool grounded;

        public PRT_FishDirtCrumb Configure(int lifetime, float gravityStrength = 0.32f, bool bounce = false) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            canBounce = bounce;
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            spin = 0f;
            canBounce = false;
            grounded = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.14f, 0.30f) * (Main.rand.NextBool() ? 1f : -1f);
            Opacity = 1f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 26);
            }
            if (gravity == 0f) {
                gravity = 0.32f;
            }
        }

        public override void AI() {
            if (Velocity.Y < 15f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.98f;
            Rotation += spin * (0.5f + Velocity.Length() * 0.05f);

            //触地
            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(2f), 4, 4)) {
                if (canBounce && !grounded) {
                    grounded = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.35f;
                    Velocity.X *= 0.55f;
                    spin *= 1.5f;
                }
                else {
                    Velocity *= 0.2f;
                    if (Lifetime - Time > 6) {
                        Time = Lifetime - 6;
                    }
                }
            }

            float t = LifetimeCompletion;
            Opacity = MathHelper.Clamp(Time / 2f, 0f, 1f) * MathHelper.Clamp((1f - t) * 3.4f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //双叠面硬角度差拼不规则土块，副面压暗造侧影
            Vector2 body = new Vector2(0.26f, 0.36f) * Scale;
            Vector2 facet = new Vector2(0.20f, 0.27f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, FishDirtVFX.Shade(Color, 0.72f) * Opacity
                , Rotation + 1.15f, origin, facet, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>干土尘团，哑光烟团帧染土色，快起慢散、短暂悬浮后自沉，峰值透明度压低防糊屏</summary>
    internal class PRT_FishDirtPuff : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private float opacityMul;
        private Color hotColor;
        private Color coldColor;

        public PRT_FishDirtPuff Configure(int lifetime, Color cold, float opacityScale = 1f) {
            Lifetime = lifetime;
            hotColor = Color;
            coldColor = cold;
            opacityMul = opacityScale;
            spin = Main.rand.NextFloat(0.006f, 0.016f) * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            opacityMul = 0f;
            hotColor = coldColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(22, 32);
            }
            if (opacityMul <= 0f) {
                opacityMul = 1f;
            }
            if (hotColor == default) {
                hotColor = FishDirtVFX.DustWarm;
                coldColor = FishDirtVFX.DustFade;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.012f;
            Rotation += spin;
            Velocity *= 0.90f;
            //干尘不上飘，短暂悬浮后自沉
            if (t > 0.35f) {
                Velocity.Y += 0.012f;
            }
            Color = Color.Lerp(hotColor, coldColor, MathF.Min(1f, t * 1.4f));
            Opacity = MathF.Min(t / 0.10f, 1f) * (1f - SmoothStep01((t - 0.40f) / 0.55f)) * 0.44f * opacityMul;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>滚痕</summary>
    internal class PRT_FishDirtTrack : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        public PRT_FishDirtTrack Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = MathHelper.PiOver2;  //竖条贴图横放
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 44);
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.002f;    //压痕缓慢摊开
            Opacity = MathF.Min(Time / 3f, 1f) * MathF.Pow(1f - t, 1.6f) * 0.4f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            //横向长、极扁的抹迹，两叠错缝
            Vector2 flat = new Vector2(0.16f, 0.95f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, tex.Size() / 2f, flat, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + new Vector2(6f, 1f), null, FishDirtVFX.Shade(Color, 0.8f) * (Opacity * 0.7f)
                , Rotation, tex.Size() / 2f, flat * new Vector2(0.8f, 0.55f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
