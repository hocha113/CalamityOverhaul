using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Tidecall
{
    /// <summary>
    /// 「汐声」屏幕层：白昼水面碎金光斑（加色微闪，随浪涌与日高变化）、
    /// 岸线泡沫拍涌（泡沫团随 <see cref="TidecallAmbience.Swell"/> 进退+少量粉尘）、
    /// 「鲨影」水下低频背鳍影线（纯氛围恐吓，不生成敌怪，暗形走真 alpha 贴图）。
    /// 光斑与泡沫走 EndEntityDraw 自开加色批；鲨影走 DrawAfterTiles 自开 AlphaBlend 批压在实体层之下
    /// </summary>
    internal sealed class TidecallAmbientRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.64</summary>
        public override float Weight => 1.64f;

        //Sparkle 黑底星闪，加色批专用
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Sparkle = null;

        private const int GlintColumnStep = 3;
        private const int MaxGlints = 96;
        private static readonly Color GlintGold = new(255, 210, 120);
        private static readonly Color FoamWhite = new(235, 245, 250);
        private static readonly Color SharkShade = new(24, 38, 50);

        //==== 光斑采样缓存（本机屏幕级） ====
        private struct SurfacePoint
        {
            internal float WorldX;
            internal float WorldY;
        }
        private static readonly SurfacePoint[] glints = new SurfacePoint[MaxGlints];
        private static int glintCount;
        private static int glintRefreshIn;

        //==== 岸线缓存 ====
        private static bool shoreValid;
        private static Vector2 shoreWorld;
        private static int shoreSeaDir;
        private static int shoreRefreshIn;
        private static int foamDustIn;

        //==== 鲨影状态机 ====
        private static bool sharkActive;
        private static Vector2 sharkPos;
        private static int sharkDir;
        private static float sharkSpeed;
        private static int sharkLife;
        private static int sharkMaxLife;
        private static float sharkScale;
        private static int sharkCooldown = 900;

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = TidecallAmbience.Presence;
            if (presence < 0.02f) {
                glintCount = 0;
                shoreValid = false;
                sharkActive = false;
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            if (--shoreRefreshIn <= 0) {
                shoreRefreshIn = 30;
                RefreshShore(player);
            }
            if (--glintRefreshIn <= 0) {
                glintRefreshIn = 8;
                RefreshGlints();
            }
            UpdateShoreFoamDust(presence);
            UpdateShark(player, presence);
        }

        private static void RefreshShore(Player player) {
            shoreValid = TidecallAmbience.TryFindShoreline(player, out int shoreX, out int surfaceY, out int seaDir);
            if (shoreValid) {
                shoreWorld = new Vector2(shoreX * 16f + 8f, surfaceY * 16f);
                shoreSeaDir = seaDir;
            }
        }

        //扫屏内水面点：以岸线水面行为参考带，避免全屏逐行探格
        private static void RefreshGlints() {
            glintCount = 0;
            if (!shoreValid) {
                return;
            }
            int surfaceRow = (int)(shoreWorld.Y / 16f);
            int left = (int)(Main.screenPosition.X / 16f) - 2;
            int right = left + Main.screenWidth / 16 + 4;
            for (int x = left; x <= right && glintCount < MaxGlints; x += GlintColumnStep) {
                for (int y = surfaceRow - 10; y <= surfaceRow + 12; y++) {
                    if (TidecallAmbience.SolidAt(x, y)) {
                        break;//先碰实心：此列到该高度无海面
                    }
                    if (!TidecallAmbience.WaterAt(x, y)) {
                        continue;
                    }
                    if (TidecallAmbience.WaterDepthTiles(x, y, 4) >= 2) {
                        glints[glintCount++] = new SurfacePoint {
                            WorldX = x * 16f + 8f,
                            WorldY = y * 16f + 2f,
                        };
                    }
                    break;//此列已定（有水面或浅水洼），换下一列
                }
            }
        }

        //岸线泡沫粒子：拍涌节奏下的少量云尘+水花（≤4/s 量级）
        private static void UpdateShoreFoamDust(float presence) {
            if (!shoreValid || --foamDustIn > 0) {
                return;
            }
            foamDustIn = Main.rand.Next(14, 24);
            Vector2 lap = shoreWorld + new Vector2(shoreSeaDir * (TidecallAmbience.Swell * 22f - 8f), 0f);
            if (Vector2.Distance(lap, Main.LocalPlayer.Center) > 1400f) {
                return;//屏外剔除
            }
            Dust foam = Dust.NewDustPerfect(
                lap + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-4f, 2f)),
                DustID.Cloud, new Vector2(shoreSeaDir * -0.3f, -Main.rand.NextFloat(0.2f, 0.8f)),
                160, FoamWhite, Main.rand.NextFloat(0.7f, 1.1f) * presence);
            foam.noGravity = true;
            if (Main.rand.NextBool(3)) {
                Dust spray = Dust.NewDustPerfect(lap, DustID.Water,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1f, 2.2f)),
                    80, default, Main.rand.NextFloat(0.8f, 1.2f));
                spray.noGravity = false;
            }
        }

        //鲨影：玩家泡在海里时低频出现，从屏幕一侧的水下深处滑过
        private static void UpdateShark(Player player, float presence) {
            if (sharkActive) {
                sharkPos.X += sharkDir * sharkSpeed;
                sharkPos.Y += MathF.Sin(sharkLife * 0.03f) * 0.35f;
                sharkLife++;
                //游出水体或走完行程就散
                if (sharkLife >= sharkMaxLife
                    || !TidecallAmbience.WaterAt((int)(sharkPos.X / 16f), (int)(sharkPos.Y / 16f))) {
                    sharkActive = false;
                    sharkCooldown = Main.rand.Next(1800, 4200);
                }
                return;
            }

            if (--sharkCooldown > 0 || CWRWorld.HasBoss || presence < 0.5f) {
                return;
            }
            if (!TidecallAmbience.InSurfWater(player)) {
                sharkCooldown = 240;//不在水里就迟些再试
                return;
            }

            //出生在玩家向海一侧屏缘外的水下；找不到足够深的水就推迟
            int dir = TidecallAmbience.DeepDir(player.Center.X);
            float spawnX = player.Center.X + dir * (Main.screenWidth * 0.5f + 160f);
            Point probe = new Vector2(spawnX, player.Center.Y).ToTileCoordinates();
            if (!TidecallAmbience.TryFindWaterSurface(probe.X, probe.Y, out int surfaceY)
                || TidecallAmbience.WaterDepthTiles(probe.X, surfaceY) < 8) {
                sharkCooldown = 300;
                return;
            }
            sharkActive = true;
            sharkDir = -dir;//从深海向岸方向掠过
            sharkPos = new Vector2(spawnX, surfaceY * 16f + Main.rand.NextFloat(90f, 190f));
            sharkSpeed = Main.rand.NextFloat(2.2f, 3.4f);
            sharkLife = 0;
            sharkMaxLife = Main.rand.Next(300, 420);
            sharkScale = Main.rand.NextFloat(0.85f, 1.25f);
        }

        //==================== 绘制 ====================

        //鲨影压在实体层之下：暗形只能走真 alpha（Extra_98），加色物理上画不出暗
        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu || !sharkActive) {
                return;
            }
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            if (spindle == null || spindle.IsDisposed) {
                return;
            }

            //首尾淡入淡出，中段最清晰；Boss 在场再压一档
            float t = sharkLife / (float)sharkMaxLife;
            float envelope = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.22f, 0f, 1f);
            float alpha = 0.38f * envelope * TidecallAmbience.Presence * TidecallAmbience.BossDim;
            if (alpha < 0.01f) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = spindle.Size() / 2f;
            Vector2 pos = sharkPos - Main.screenPosition;
            float bodyRot = MathHelper.PiOver2 + MathF.Sin(sharkLife * 0.05f) * 0.04f;
            Color shade = SharkShade * alpha;
            //躯干：横置长梭
            spriteBatch.Draw(spindle, pos, null, shade, bodyRot, origin,
                new Vector2(1.35f, 3.1f) * sharkScale, SpriteEffects.None, 0f);
            //背鳍：躯干中段上方的小竖梭，随游动微倾
            Vector2 finPos = pos + new Vector2(sharkDir * 6f * sharkScale, -16f * sharkScale);
            spriteBatch.Draw(spindle, finPos, null, shade * 0.9f,
                sharkDir * 0.28f, origin, new Vector2(0.5f, 0.95f) * sharkScale, SpriteEffects.None, 0f);
            //尾鳍：后端小梭，摆频更高
            Vector2 tailPos = pos - new Vector2(sharkDir * 62f * sharkScale, 0f);
            spriteBatch.Draw(spindle, tailPos, null, shade * 0.8f,
                MathF.Sin(sharkLife * 0.16f) * 0.5f, origin,
                new Vector2(0.45f, 0.9f) * sharkScale, SpriteEffects.None, 0f);

            spriteBatch.End();
        }

        //碎金光斑与岸线泡沫：加色批，画在实体之上（都在水面上层，物理正确）
        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = TidecallAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            float dayFactor = DaylightFactor();
            bool anyGlint = dayFactor > 0.02f && glintCount > 0;
            if (!anyGlint && !shoreValid) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (anyGlint) {
                DrawGlints(spriteBatch, presence, dayFactor);
            }
            if (shoreValid) {
                DrawShoreFoam(spriteBatch, presence);
            }
            spriteBatch.End();
        }

        //日光强度：正午最盛，雨天近乎失效
        private static float DaylightFactor() {
            if (!Main.dayTime) {
                return 0f;
            }
            float elevation = MathF.Sin(MathHelper.Pi * (float)(Main.time / 54000.0));
            float rain = Main.raining ? 0.12f : 1f;
            return MathHelper.Clamp(elevation, 0f, 1f) * rain;
        }

        //碎金光斑：确定性哈希选闪点，同一时刻只亮少数几粒，随浪涌加密
        private static void DrawGlints(SpriteBatch sb, float presence, float dayFactor) {
            Texture2D star = Sparkle?.Value;
            if (star == null || star.IsDisposed) {
                return;
            }
            Vector2 origin = star.Size() / 2f;
            float swell = TidecallAmbience.Swell;
            float time = (float)Main.timeForVisualEffects * 0.05f;
            for (int i = 0; i < glintCount; i++) {
                float hash = (glints[i].WorldX * 0.0817f) % MathHelper.TwoPi;
                float twinkle = MathF.Sin(time * (1.6f + (hash % 0.7f)) + hash * 7.3f);
                //只取波峰上缘：同一帧只有少数点在闪
                float vis = (twinkle - 0.72f) / 0.28f;
                if (vis <= 0f) {
                    continue;
                }
                float alpha = vis * (0.30f + 0.30f * swell) * presence * dayFactor
                    * TidecallAmbience.BossDim;
                Vector2 pos = new Vector2(glints[i].WorldX, glints[i].WorldY) - Main.screenPosition;
                sb.Draw(star, pos, null, GlintGold * alpha, hash,
                    origin, 0.10f + 0.14f * vis, SpriteEffects.None, 0f);
            }
        }

        //岸线泡沫：泡沫团贴着水陆交界，随浪涌沿向海方向进退
        private static void DrawShoreFoam(SpriteBatch sb, float presence) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || glow.IsDisposed) {
                return;
            }
            Vector2 origin = glow.Size() / 2f;
            float swell = TidecallAmbience.Swell;
            Vector2 lap = shoreWorld + new Vector2(shoreSeaDir * (swell * 22f - 8f), 0f) - Main.screenPosition;
            float alpha = (0.10f + 0.16f * swell) * presence * TidecallAmbience.BossDim;
            //三个错相泡沫团组成一小截拍岸沫线
            for (int i = -1; i <= 1; i++) {
                float phase = MathF.Sin((float)Main.timeForVisualEffects * 0.03f + i * 2.1f);
                Vector2 pos = lap + new Vector2(i * 20f + phase * 4f, phase * 1.5f);
                sb.Draw(glow, pos, null, FoamWhite * (alpha * (i == 0 ? 1f : 0.65f)), 0f,
                    origin, new Vector2(0.85f, 0.24f), SpriteEffects.None, 0f);
            }
        }
    }
}
