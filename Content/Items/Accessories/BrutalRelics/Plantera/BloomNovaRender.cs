using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Plantera
{
    /// <summary>
    /// 新星屏幕演出通道：花瓣环+爆心闪光。客户端纯表现，
    /// 弹幕在爆发拍推送，此处只消费，不新增网络包(镜像 PlanteraScreenFX 通道范式)
    /// </summary>
    internal static class BloomNovaFX
    {
        internal struct RingState
        {
            public bool Active;
            public Vector2 WorldCenter;
            /// <summary>负值=延迟起演</summary>
            public int Age;
            public int Life;
            public float MaxRadiusPx;
            public float Seed;
        }

        internal const int MaxRings = 4;
        internal static readonly RingState[] Rings = new RingState[MaxRings];

        internal static Vector2 FlashWorldCenter;
        internal static int FlashAge;
        internal const int FlashLife = 26;
        internal static float FlashIntensity;
        internal static bool FlashActive;

        internal static bool HasAny {
            get {
                if (FlashActive) {
                    return true;
                }
                for (int i = 0; i < MaxRings; i++) {
                    if (Rings[i].Active) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal static void PushRing(Vector2 worldCenter, float maxRadius, int life, int delay, float seed) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < MaxRings; i++) {
                if (Rings[i].Active) {
                    continue;
                }
                Rings[i] = new RingState {
                    Active = true,
                    WorldCenter = worldCenter,
                    Age = -delay,
                    Life = life,
                    MaxRadiusPx = maxRadius,
                    Seed = seed,
                };
                return;
            }
        }

        internal static void PushFlash(Vector2 worldCenter, float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            FlashWorldCenter = worldCenter;
            FlashIntensity = Math.Max(FlashIntensity, intensity);
            FlashAge = 0;
            FlashActive = true;
        }

        internal static void Update() {
            for (int i = 0; i < MaxRings; i++) {
                if (!Rings[i].Active) {
                    continue;
                }
                Rings[i].Age++;
                if (Rings[i].Age >= Rings[i].Life) {
                    Rings[i].Active = false;
                }
            }
            if (FlashActive && ++FlashAge >= FlashLife) {
                FlashActive = false;
                FlashIntensity = 0f;
            }
        }

        internal static void Clear() {
            Array.Clear(Rings, 0, Rings.Length);
            FlashActive = false;
            FlashIntensity = 0f;
        }
    }

    /// <summary>
    /// 遗物渲染层(权重1.83)：EndCapture 消费新星花环/闪光；
    /// DrawBeforePlayers 画低血待机的脚下花茎缠绕(呼吸光走藤蔓脉络行波+辉团)。
    /// DrawBeforePlayers 每帧被 BehindNPCs 与主玩家层各触发一次，
    /// 用 DrawAfterTiles 上膛、首次消费的闩锁保证只画一次(Unsunghero 同款)
    /// </summary>
    internal class BloomNovaRender : RenderHandle, ICWRLoader
    {
        /// <summary>认领表分配的遗物权重槽</summary>
        public override float Weight => 1.83f;

        private static bool feetArmed;

        void ICWRLoader.UnLoadData() => BloomNovaFX.Clear();

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => feetArmed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!feetArmed || Main.gameMenu) {
                return;
            }
            feetArmed = false;
            DrawStandbyFeetVines(spriteBatch);
        }

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            BloomNovaFX.Update();
            if (!BloomNovaFX.HasAny || Main.screenTarget == null) {
                return;
            }
            gd.SetRenderTarget(Main.screenTarget);

            DrawRings(sb, gd);
            if (BloomNovaFX.FlashActive) {
                DrawFlash(sb);
            }
        }

        #region 脚下花茎待机
        /// <summary>低血待机：脚踝处交叉花茎，随血量下降逐节长出，脉络行波即呼吸。
        /// 先辉团批(垫底)后藤蔓图元，避免图元夹在延迟批中间导致层序错乱</summary>
        private static void DrawStandbyFeetVines(SpriteBatch sb) {
            Span<float> intensities = stackalloc float[Main.maxPlayers];
            bool any = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                intensities[i] = 0f;
                if (player == null || !player.active || player.dead
                    || !player.TryGetModPlayer(out BloomNovaBulbPlayer mp)) {
                    continue;
                }
                float intensity = mp.StandbyIntensity;
                if (intensity <= 0.04f || !PlanteraRenderHelper.OnScreen(player.Bottom, 160f)) {
                    continue;
                }
                intensities[i] = intensity;
                any = true;
            }
            if (!any) {
                return;
            }

            //第一遍：脚下呼吸辉团(加色批：A随强度走，不许A=0)
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                for (int i = 0; i < Main.maxPlayers; i++) {
                    if (intensities[i] <= 0f) {
                        continue;
                    }
                    float breath = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f + i * 1.7f);
                    Vector2 feet = Main.player[i].Bottom + new Vector2(0f, -2f);
                    Color glowCol = new Color(
                        PlanteraRenderHelper.GlowGreen.R, PlanteraRenderHelper.GlowGreen.G, PlanteraRenderHelper.GlowGreen.B)
                        * (0.4f * intensities[i] * breath);
                    sb.Draw(glow, feet - Main.screenPosition, null, glowCol,
                        0f, glow.Size() / 2f, new Vector2(1.5f, 0.7f), SpriteEffects.None, 0f);
                }
                sb.End();
            }

            //第二遍：两道交叉花茎图元(根在两侧地面，生长前沿=强度)
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (intensities[i] <= 0f) {
                    continue;
                }
                float intensity = intensities[i];
                float breath = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f + i * 1.7f);
                Vector2 feet = Main.player[i].Bottom + new Vector2(0f, -2f);

                VineParams vine = VineParams.Default;
                vine.HalfWidth = 4.5f;
                vine.Taut = 0.8f;
                vine.Pulse = 0.25f + 0.35f * breath * intensity;
                vine.PulseDir = 1f;
                vine.Grow = MathHelper.Clamp(intensity * 1.3f, 0.2f, 1f);
                vine.Fade = MathHelper.Clamp(intensity * 1.6f, 0f, 0.9f);
                vine.Seed = 0.21f + i * 0.043f % 0.6f;
                Vector2 a1 = feet + new Vector2(-30f, 4f);
                Vector2 a2 = feet + new Vector2(24f, -30f);
                vine.RestLength = Vector2.Distance(a1, a2) * 1.08f;
                PlanteraVineRenderer.DrawVineRaw(a1, a2, vine);

                vine.Seed += 0.27f;
                Vector2 b1 = feet + new Vector2(30f, 4f);
                Vector2 b2 = feet + new Vector2(-24f, -30f);
                vine.RestLength = Vector2.Distance(b1, b2) * 1.08f;
                PlanteraVineRenderer.DrawVineRaw(b1, b2, vine);
            }
        }
        #endregion

        #region 新星花环与闪光
        /// <summary>花瓣环：PlanteraBloom 着色器(玩家侧一阶段绿粉配色)，缺编走软光圈回退</summary>
        private static void DrawRings(SpriteBatch sb, GraphicsDevice gd) {
            Effect shader = EffectLoader.PlanteraBloom?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D quad = VaultAsset.placeholder2.Value;

            for (int i = 0; i < BloomNovaFX.MaxRings; i++) {
                ref readonly var ring = ref BloomNovaFX.Rings[i];
                if (!ring.Active || ring.Age < 0) {
                    continue;
                }

                float t = ring.Age / (float)ring.Life;
                Vector2 screenPos = WorldToScreenPx(ring.WorldCenter);
                float radiusPx = ring.MaxRadiusPx * ZoomY();
                //环画在归一化0.82半径处，quad按此折算
                float quadSize = radiusPx * 2f / 0.82f;

                if (shader == null || noise == null) {
                    //回退：软光圈扩散(加色批A随强度走)
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    Color col = new Color(
                        PlanteraRenderHelper.GlowGreen.R, PlanteraRenderHelper.GlowGreen.G, PlanteraRenderHelper.GlowGreen.B)
                        * ((1f - t) * 0.5f);
                    sb.Draw(glow, screenPos, null, col, 0f, glow.Size() / 2f,
                        quadSize * VaultUtils.EaseOutCubic(t) / glow.Width, SpriteEffects.None, 0f);
                    sb.End();
                    continue;
                }

                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive);
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uProgress"]?.SetValue(VaultUtils.EaseOutCubic(t));
                shader.Parameters["uIntensity"]?.SetValue(1f - t * t);
                shader.Parameters["uPhase2"]?.SetValue(0f);
                shader.Parameters["uGapOn"]?.SetValue(0f);
                shader.Parameters["uGap1"]?.SetValue(0f);
                shader.Parameters["uGap2"]?.SetValue(0f);
                shader.Parameters["uGapCos"]?.SetValue(1f);
                shader.Parameters["seed"]?.SetValue(ring.Seed);
                //噪声显式绑s1：SpriteBatch.Draw会把s0覆写成画布贴图
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.CurrentTechnique.Passes[0].Apply();

                sb.Draw(quad, screenPos, null, Color.White, 0f, quad.Size() / 2f,
                    quadSize / quad.Width, SpriteEffects.None, 0f);
                sb.End();
            }
        }

        /// <summary>爆心闪光：全幕轻罩+爆心辐射，加色批A随强度走</summary>
        private static void DrawFlash(SpriteBatch sb) {
            float t = BloomNovaFX.FlashAge / (float)BloomNovaFX.FlashLife;
            float strength = BloomNovaFX.FlashIntensity * (1f - t) * (1f - t);
            if (strength <= 0.01f) {
                return;
            }

            Vector2 center = WorldToScreenPx(BloomNovaFX.FlashWorldCenter);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D quad = VaultAsset.placeholder2.Value;
            Color flashPink = new(255, 180, 205);
            Color flashGreen = new(195, 255, 165);

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            sb.Draw(quad, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                flashPink * (strength * 0.22f));
            float coreScale = Main.screenWidth * 1.1f / glow.Width;
            sb.Draw(glow, center, null, flashGreen * (strength * 0.7f),
                0f, glow.Size() / 2f, coreScale * (0.5f + t * 0.5f), SpriteEffects.None, 0f);
            sb.Draw(glow, center, null, Color.White * (strength * 0.5f),
                0f, glow.Size() / 2f, coreScale * 0.28f, SpriteEffects.None, 0f);
            sb.End();
        }

        private static Vector2 WorldToScreenPx(Vector2 worldPos) {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenter;
            return screenCenter + (worldPos - viewWorldCenter) * zoom;
        }

        private static float ZoomY() {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            return zoomY <= 0f ? 1f : zoomY;
        }
        #endregion
    }
}
