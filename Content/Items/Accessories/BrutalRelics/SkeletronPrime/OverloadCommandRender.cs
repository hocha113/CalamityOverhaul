using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.SkeletronPrime
{
    /// <summary>
    /// 过载指令核心表现层：环绕玩家的指令序列字符流（速率随充能加快）、
    /// 高充能离子漩涡（PrimeChargeVortex 换青）、过载入场爆发环与白闪。<br/>
    /// 全部状态取自逐玩家 <see cref="OverloadCorePlayer"/> 实例字段，无静态可变态；
    /// 充能在 owner 端记账，远端经 OverloadStateNet 档位镜像(跨25充能/过载/过热沿)
    /// 同步，故字符流全端可见（弹幕类表现另经原版同步）
    /// </summary>
    internal sealed class OverloadCommandRender : RenderHandle
    {
        /// <summary>认领槽位 1.822（错开环境渲染 LumindepthAmbientRender 的 1.82）</summary>
        public override float Weight => 1.822f;

        /// <summary>活跃帧戳：有可见状态的玩家在 PostUpdate 盖戳，无戳跳过全表扫描</summary>
        internal static ActivityStamp RenderStamp;

        //指令助记符池：机械指令风味，逐槽位哈希轮换
        private static readonly string[] Tokens = [
            "EXEC", "PWR:MAX", "0x2F", "SYNC", "ION+", "ARM.4", "LNK", "OVR",
            "1101", "CHG%", "RDY", "0xFF", "CTRL", "AMP", "»»", "SEQ",
        ];
        private static readonly string[] HeatTokens = ["ERR", "HEAT", "COOL", "0x00", "VENT"];

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || !RenderStamp.ActiveWithin()) {
                return;
            }

            bool begun = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active || player.dead
                    || !player.TryGetModPlayer(out OverloadCorePlayer mp)) {
                    continue;
                }
                bool anyVisible = mp.IonCharge > 0.5f || mp.OverloadActive
                    || mp.Overheated || mp.BurstFlashTimer > 0;
                if (!anyVisible || !OnScreen(player.Center)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                        null, Main.GameViewMatrix.TransformationMatrix);
                }

                //绘制顺序：漩涡（底）→ 爆发（中）→ 字符流（顶）
                DrawChargeVortex(spriteBatch, player, mp);
                DrawBurst(spriteBatch, player, mp);
                DrawCommandStream(spriteBatch, player, mp);
            }

            if (begun) {
                spriteBatch.End();
            }
        }

        private static bool OnScreen(Vector2 worldPos) {
            Vector2 screen = Main.screenPosition;
            const float pad = 400f;
            return worldPos.X > screen.X - pad && worldPos.X < screen.X + Main.screenWidth + pad
                && worldPos.Y > screen.Y - pad && worldPos.Y < screen.Y + Main.screenHeight + pad;
        }

        //==================== 指令字符流 ====================

        private static void DrawCommandStream(SpriteBatch sb, Player player, OverloadCorePlayer mp) {
            var font = FontAssets.MouseText.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            //过热态：少量琥珀告警符缓慢下坠
            if (mp.Overheated) {
                float heatT = mp.OverheatTimer / (float)OverloadCommandCore.OverheatFrames;
                for (int s = 0; s < 3; s++) {
                    string token = HeatTokens[(s * 31 + player.whoAmI * 7) % HeatTokens.Length];
                    float drop = 1f - heatT;
                    Vector2 pos = player.MountedCenter + new Vector2(
                        (s - 1) * 30f + (float)Math.Sin(mp.StreamPhase * 3f + s * 2.1f) * 6f,
                        -34f + drop * 44f);
                    DrawToken(sb, font, glow, token, pos,
                        OverloadCommandCore.HeatEmber, 0.32f * heatT, 0.58f);
                }
                return;
            }

            bool overload = mp.OverloadActive;
            float charge = mp.ChargeRatio;
            if (!overload && charge <= 0.005f) {
                return;
            }

            int count = overload ? 14 : 3 + (int)(charge * 9f);
            float alphaBase = overload ? 0.85f : 0.25f + charge * 0.55f;
            float radiusX = overload ? 54f : 46f + charge * 6f;
            float radiusY = radiusX * 0.8f;

            for (int s = 0; s < count; s++) {
                float ang = mp.StreamPhase + MathHelper.TwoPi * s / count;
                Vector2 pos = player.MountedCenter + new Vector2(
                    (float)Math.Cos(ang) * radiusX,
                    (float)Math.Sin(ang) * radiusY - 4f);

                //逐槽位轮换助记符：每 ~30 帧换一个
                int tokenIdx = (s * 13 + (int)(mp.StreamPhase * 2f) + player.whoAmI * 5) % Tokens.Length;
                //背面（轨道下行侧）压暗，读出环绕纵深
                float depth = 0.62f + 0.38f * (float)Math.Sin(ang);
                //过载态逐字符哈希闪烁
                float flick = overload && (s * 7 + (int)(Main.GlobalTimeWrappedHourly * 26f)) % 9 == 0
                    ? 0.35f : 1f;

                Color c = overload
                    ? Color.Lerp(OverloadCommandCore.IonCyan, OverloadCommandCore.IonHot, s % 3 / 2f)
                    : Color.Lerp(OverloadCommandCore.IonDeep, OverloadCommandCore.IonCyan, charge);
                DrawToken(sb, font, glow, Tokens[tokenIdx], pos, c,
                    alphaBase * depth * flick, overload ? 0.66f : 0.52f + charge * 0.12f);
            }
        }

        private static void DrawToken(SpriteBatch sb, DynamicSpriteFont font, Texture2D glow,
            string token, Vector2 worldPos, Color color, float alpha, float scale) {
            if (alpha <= 0.02f) {
                return;
            }
            Vector2 size = font.MeasureString(token);
            Vector2 drawPos = worldPos - Main.screenPosition;
            //衬底辉光：黑底贴图在 AlphaBlend 批走 A=0 加色技法
            sb.Draw(glow, drawPos, null, (color with { A = 0 }) * (alpha * 0.55f), 0f,
                glow.Size() * 0.5f, scale * (size.X / 48f + 0.5f), SpriteEffects.None, 0f);
            sb.DrawString(font, token, drawPos, color * alpha, 0f,
                size * 0.5f, scale, SpriteEffects.None, 0f);
        }

        //==================== 高充能离子漩涡（PrimeChargeVortex 协议换青） ====================

        private static void DrawChargeVortex(SpriteBatch sb, Player player, OverloadCorePlayer mp) {
            float progress;
            float opacity;
            if (mp.OverloadActive) {
                //入场 45 帧绽放淡出，窗口常态不驻留
                int elapsed = OverloadCommandCore.OverloadFrames - mp.OverloadTimer;
                if (elapsed > 45) {
                    return;
                }
                progress = 1f;
                opacity = 1f - elapsed / 45f;
            }
            else {
                if (mp.ChargeRatio < 0.65f) {
                    return;
                }
                progress = (mp.ChargeRatio - 0.65f) / 0.35f;
                opacity = 0.35f + progress * 0.4f;
            }

            Effect shader = EffectLoader.PrimeChargeVortex?.Value;
            if (shader == null) {
                return;
            }

            shader.Parameters["uColor"]?.SetValue(OverloadCommandCore.IonCyan.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(
                Color.Lerp(OverloadCommandCore.IonCyan, Color.White, 0.55f).ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(progress);
            shader.Parameters["uIntensity"]?.SetValue(0.35f + progress * 0.5f);
            shader.Parameters["uOpacity"]?.SetValue(opacity);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            float size = 190f + 130f * progress;
            sb.Draw(quad, player.MountedCenter - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 过载入场爆发：白闪 + 冲击环 ====================

        private static void DrawBurst(SpriteBatch sb, Player player, OverloadCorePlayer mp) {
            if (mp.BurstFlashTimer <= 0) {
                return;
            }
            float t = mp.BurstFlashTimer / 18f; //1→0
            Vector2 center = player.MountedCenter;

            //核心白闪（SoftGlow 黑底，AlphaBlend 批 A=0 加色）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            sb.Draw(glow, center - Main.screenPosition, null,
                (OverloadCommandCore.IonHot with { A = 0 }) * (t * 0.9f), 0f,
                glow.Size() * 0.5f, 2.6f + (1f - t) * 1.2f, SpriteEffects.None, 0f);

            //扩张冲击环（共享 ShockRing，内部自管批切换并还原）
            float ringR = (1f - t) * 330f + 20f;
            ShockRingDraw.Draw(sb, center, ringR, 14f,
                OverloadCommandCore.IonHot, OverloadCommandCore.IonCyan, OverloadCommandCore.IonDeep,
                t, innerGlow: 0.25f, timeSeed: player.whoAmI * 0.7f);
        }
    }
}
