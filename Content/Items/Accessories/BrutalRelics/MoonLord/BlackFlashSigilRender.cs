using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.MoonLord
{
    /// <summary>
    /// 黑闪印记渲染层（权重 1.88）：
    /// ①领域感残影——连闪高层时敌对弹幕拖出冷色滞后残影（时间感知暗示，
    /// 只画在所有者本地屏幕，不改任何弹速）；
    /// ②胸前印记环——收缩读秒 / 窗口锁定 / 黑金电弧 / 失手碎裂，
    /// shader 出体，缺 shader 走克制的 CPU 光点回退
    /// </summary>
    internal sealed class BlackFlashSigilRender : RenderHandle
    {
        public override float Weight => 1.88f;

        /// <summary>印记 quad 画布边长（px）</summary>
        private const float QuadPx = 216f;
        /// <summary>残影弹幕上限（填充率保险）</summary>
        private const int MaxGhostProj = 48;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || Main.dedServ) {
                return;
            }
            DrawTimeSenseGhosts(spriteBatch);
            DrawSigils(spriteBatch);
        }

        #region 领域感残影
        /// <summary>
        /// 敌对弹幕沿速度反向拖三帧冷色残影：残影读作"它变慢了"，
        /// 实际弹速一帧未改。强度随连闪层数爬升
        /// </summary>
        private static void DrawTimeSenseGhosts(SpriteBatch spriteBatch) {
            Player local = Main.LocalPlayer;
            if (local == null || !local.active || local.dead
                || !local.TryGetModPlayer(out BlackFlashSigilPlayer mp)
                || !mp.Equipped || mp.Stacks < BlackFlashSigilPlayer.DomainStacks) {
                return;
            }
            float t = MathHelper.Clamp(
                (mp.Stacks - BlackFlashSigilPlayer.DomainStacks)
                / (float)(BlackFlashSigilPlayer.MaxStacks - BlackFlashSigilPlayer.DomainStacks), 0f, 1f);
            float strength = 0.5f + 0.5f * t;
            //冷色滞象：去饱和的月白偏紫
            Color ghostTint = new(150, 158, 208);

            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);

            bool begun = false;
            int drawn = 0;
            for (int i = 0; i < Main.maxProjectiles && drawn < MaxGhostProj; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.hostile || proj.hide
                    || proj.velocity.LengthSquared() < 4f
                    || !view.Contains(proj.Center.ToPoint())) {
                    continue;
                }
                Main.instance.LoadProjectile(proj.type);
                Texture2D tex = TextureAssets.Projectile[proj.type].Value;
                if (tex == null) {
                    continue;
                }
                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                }
                int frames = Math.Max(Main.projFrames[proj.type], 1);
                Rectangle frame = tex.Frame(1, frames, 0, Math.Clamp(proj.frame, 0, frames - 1));
                Vector2 origin = frame.Size() * 0.5f;
                for (int g = 1; g <= 3; g++) {
                    //滞后残影：越远越淡越缩
                    Vector2 pos = proj.Center - proj.velocity * (1.7f * g) - Main.screenPosition;
                    float alpha = strength * (0.20f - 0.055f * g);
                    spriteBatch.Draw(tex, pos, frame, ghostTint * alpha,
                        proj.rotation, origin, proj.scale * (1f - 0.05f * g), SpriteEffects.None, 0f);
                }
                drawn++;
            }
            if (begun) {
                spriteBatch.End();
            }
        }
        #endregion

        #region 胸前印记
        private static void DrawSigils(SpriteBatch spriteBatch) {
            Rectangle view = new((int)Main.screenPosition.X - 240, (int)Main.screenPosition.Y - 240,
                Main.screenWidth + 480, Main.screenHeight + 480);

            bool any = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (SigilVisible(player, view, out _)) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect shader = EffectLoader.BRelicBlackFlash?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (shader != null && canvas != null && noise != null) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);

                float scale = QuadPx / canvas.Width;
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (!SigilVisible(player, view, out BlackFlashSigilPlayer mp)) {
                        continue;
                    }
                    shader.Parameters["uPhase"]?.SetValue(mp.TelegraphT);
                    shader.Parameters["uWindow"]?.SetValue(mp.WindowT);
                    shader.Parameters["uFlash"]?.SetValue(mp.FlashGlow / 26f);
                    shader.Parameters["uArc"]?.SetValue(mp.ArcLevel);
                    shader.Parameters["uBreak"]?.SetValue(mp.BreakFlicker / 18f);
                    shader.Parameters["uSeed"]?.SetValue(player.whoAmI % 89 * 0.211f);
                    shader.Parameters["uAlpha"]?.SetValue(mp.VisualFade);
                    shader.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(canvas, mp.SigilCenter() - Main.screenPosition, null,
                        Color.White, 0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                }
                spriteBatch.End();
                return;
            }

            //CPU 回退：克制的光点环 + 真 alpha 暗核，不拿灰度堆叠拟形
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (SigilVisible(player, view, out BlackFlashSigilPlayer mp)) {
                    DrawFallback(spriteBatch, mp);
                }
            }
            spriteBatch.End();
        }

        private static bool SigilVisible(Player player, Rectangle view, out BlackFlashSigilPlayer mp) {
            mp = null;
            if (player == null || !player.active || player.dead
                || !view.Contains(player.Center.ToPoint())
                || !player.TryGetModPlayer(out mp)) {
                return false;
            }
            return mp.VisualFade > 0.01f;
        }

        /// <summary>缺 shader：SoftGlow 光点收缩环（A=0 加色）+ Extra_98 暗核（真 alpha）</summary>
        private static void DrawFallback(SpriteBatch spriteBatch, BlackFlashSigilPlayer mp) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D dark = CWRAsset.Extra_98?.Value;
            Vector2 center = mp.SigilCenter() - Main.screenPosition;
            float fade = mp.VisualFade;

            if (dark != null) {
                spriteBatch.Draw(dark, center, null, new Color(10, 6, 18) * (0.85f * fade),
                    Main.GlobalTimeWrappedHourly * 0.5f, dark.Size() * 0.5f, 0.42f, SpriteEffects.None, 0f);
            }
            if (glow == null) {
                return;
            }
            float phase = mp.TelegraphT;
            float radius = MathHelper.Lerp(95f, 33f, phase);
            Color ringCol = Color.Lerp(BlackFlashSigilPlayer.FlashRed,
                BlackFlashSigilPlayer.GoldArc, phase * phase) with { A = 0 };
            if (phase > 0f || mp.WindowT > 0f) {
                for (int k = 0; k < 12; k++) {
                    Vector2 pos = center + (MathHelper.TwoPi * k / 12f).ToRotationVector2() * radius;
                    spriteBatch.Draw(glow, pos, null, ringCol * ((0.5f + 0.5f * mp.WindowT) * fade),
                        0f, glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
                }
            }
            float arc = mp.ArcLevel;
            if (arc > 0.02f) {
                for (int k = 0; k < 6; k++) {
                    float ang = MathHelper.TwoPi * k / 6f + Main.GlobalTimeWrappedHourly * 2.4f;
                    Vector2 pos = center + ang.ToRotationVector2() * 56f;
                    spriteBatch.Draw(glow, pos, null,
                        BlackFlashSigilPlayer.GoldArc with { A = 0 } * (0.55f * arc * fade),
                        0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0f);
                }
            }
        }
        #endregion
    }
}
