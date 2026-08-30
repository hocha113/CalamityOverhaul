using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴绘制与换装契约的唯一所在：贴图资产、帧约定、锚点约定都收在这里
    /// 换真贴图 = 覆盖 PNG（多帧竖排则只改 <see cref="FrameCount"/>）。
    /// 凝聚/融化走 <c>KikasaThrallForm</c> 着色器（帧矩形钳制，任意精灵表可用），
    /// 行走期普通绘制加程序化蹒跚；批次形制镜 KasaOniRenderer/KikasaDrownFX。
    /// </summary>
    internal static class KikasaThrallRenderer
    {
        //==================== 换装契约 ====================

        /// <summary>贴图帧数（竖排）。真贴图多帧时只改这一处，帧矩形与着色器自动适配</summary>
        internal const int FrameCount = 1;

        /// <summary>贴图 2x 入库（画布 154×230），身量语义仍按旧 77×115 画布走：本体纹理绘制统一乘它归一</summary>
        internal const float BodyTexelScale = 0.5f;

        /// <summary>贴图脚底距画布底边的像素（154×230 画布，KasaOni 的 1.6 倍身量），留白同比放大</summary>
        internal const int FeetOffsetY = 12;

        [VaultLoaden(CWRConstant.NPC + "Kikasa/KikasaThrall")]
        private static Asset<Texture2D> ThrallTex = null;
        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> PuddleMask = null;

        private static bool renderFailureLogged;

        internal static Texture2D BodyTexture => ThrallTex?.Value;

        internal static Rectangle FrameOf(Texture2D tex, int frameIndex) {
            int height = tex.Height / FrameCount;
            return new Rectangle(0, height * (frameIndex % FrameCount), tex.Width, height);
        }

        /// <summary>贴图中心的世界锚定：脚底对齐 feet，脚在底边上方 FeetOffsetY px</summary>
        internal static Vector2 BodyCenterFromFeet(Vector2 feet, Rectangle frame, float scale)
            => feet - new Vector2(0f, frame.Height * 0.5f * scale - FeetOffsetY * scale);

        //==================== 着色器参数（融化与凝聚共用） ====================

        /// <summary>
        /// 设 KikasaThrallForm 全套参数。调用方须已开着 Immediate 批并在设参后
        /// <c>form.CurrentTechnique.Passes[0].Apply()</c>；s1 需绑 PerlinNoise。
        /// groundY 传 float.MaxValue 即关闭地面裁切（空中融化的尸影）
        /// </summary>
        internal static void SetFormParams(Effect form, Texture2D tex, Rectangle frame,
            float progress, float scale, float rotation, float centerY, float groundY,
            float timeOffset) {
            form.Parameters["uProgress"]?.SetValue(progress);
            form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + timeOffset);
            form.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            form.Parameters["uFrameSize"]?.SetValue(new Vector2(frame.Width, frame.Height));
            form.Parameters["uScale"]?.SetValue(scale);
            form.Parameters["uRotation"]?.SetValue(rotation);
            form.Parameters["uCenterY"]?.SetValue(centerY);
            form.Parameters["uGroundY"]?.SetValue(
                groundY >= float.MaxValue * 0.5f ? centerY + 1e7f : groundY);
            form.Parameters["uSewageDeep"]?.SetValue(KikasaThrall.SewageDeep.ToVector3());
            form.Parameters["uEdgeColor"]?.SetValue(KikasaThrall.PaleSheen.ToVector3());
        }

        //==================== 伞奴本体（弹幕 PreDraw 语境） ====================

        /// <summary>
        /// 凝聚/溶解期本体：切 Immediate + KikasaThrallForm，异常回退普通淡入淡出，
        /// 收尾恢复弹幕层 Deferred 批。wobble=液体蠕动的微转角
        /// </summary>
        internal static void DrawBodyCondensing(SpriteBatch sb, Vector2 feet, int frameIndex,
            float progress, float scale, bool facingLeft, float groundY, Color light,
            float wobble, float seedOffset) {

            Texture2D body = BodyTexture;
            if (body == null || body.IsDisposed) {
                return;
            }
            Rectangle frame = FrameOf(body, frameIndex);
            float texScale = scale * BodyTexelScale;
            Vector2 center = BodyCenterFromFeet(feet, frame, texScale);
            SpriteEffects flip = facingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Effect form = EffectLoader.KikasaThrallForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice device = Main.instance?.GraphicsDevice;
            if (form == null || noise == null || noise.IsDisposed || device == null) {
                DrawBodyFallback(sb, body, frame, center, texScale, flip, light, progress);
                return;
            }

            Texture previousTexture1 = device.Textures[1];
            SamplerState previousSampler1 = device.SamplerStates[1];
            bool callerBatchEnded = false;
            bool formBatchOpen = false;
            bool batchRestored = false;
            bool drawFallback = false;

            try {
                sb.End();
                callerBatchEnded = true;
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                formBatchOpen = true;
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;

                SetFormParams(form, body, frame, progress, texScale, wobble, center.Y, groundY, seedOffset);
                form.CurrentTechnique.Passes[0].Apply();
                sb.Draw(body, center - Main.screenPosition, frame, light,
                    wobble, frame.Size() * 0.5f, texScale, flip, 0f);

                sb.End();
                formBatchOpen = false;
            } catch (Exception exception) {
                drawFallback = true;
                LogRenderFailure(exception);
            } finally {
                if (formBatchOpen) {
                    TryEnd(sb);
                }
                device.Textures[1] = previousTexture1;
                device.SamplerStates[1] = previousSampler1;
                if (callerBatchEnded) {
                    batchRestored = RestoreProjectileBatch(sb);
                }
            }

            if (drawFallback && batchRestored) {
                DrawBodyFallback(sb, body, frame, center, texScale, flip, light, progress);
            }
        }

        /// <summary>行走：蹒跚摇摆 + 踏步压缩 + 冷灰青幽光衬底（弹幕层 Deferred 内直画）</summary>
        internal static void DrawBodyWalking(SpriteBatch sb, Vector2 feet, int frameIndex,
            float scale, bool facingLeft, Color light, float waddlePhase, float moveFactor,
            float identitySeed) {

            Texture2D body = BodyTexture;
            if (body == null || body.IsDisposed) {
                return;
            }
            Rectangle frame = FrameOf(body, frameIndex);
            float texScale = scale * BodyTexelScale;
            float rotation = MathF.Sin(waddlePhase) * 0.07f * moveFactor;
            float bob = Math.Abs(MathF.Sin(waddlePhase)) * 1.6f * moveFactor;
            Vector2 squash = new(1f + MathF.Sin(waddlePhase * 2f) * 0.015f * moveFactor,
                1f - Math.Abs(MathF.Sin(waddlePhase * 2f)) * 0.03f * moveFactor);
            Vector2 center = BodyCenterFromFeet(feet, frame, texScale);
            Vector2 drawPos = center - Main.screenPosition - new Vector2(0f, bob);
            SpriteEffects flip = facingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + identitySeed) * 0.5f + 0.5f;
                Color backing = new Color(80, 102, 106) with { A = 0 } * (0.10f + pulse * 0.05f);
                //衬底尺寸按旧 77×115 身量语义配（世界尺寸，不随贴图 2x 入库变），撑得住放大后的身量
                sb.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f,
                    new Vector2(176f * scale / glow.Width, 154f * scale / glow.Height),
                    SpriteEffects.None, 0f);
            }

            sb.Draw(body, drawPos, frame, light, rotation,
                frame.Size() * 0.5f, squash * texScale, flip, 0f);
        }

        /// <summary>脚下污潭：envelope 0~1 张开度，真 alpha 深色水渍 + 尸斑青薄光沿</summary>
        internal static void DrawPuddle(SpriteBatch sb, Vector2 feet, float envelope,
            float widthScale, float identitySeed) {
            Texture2D mask = PuddleMask?.Value;
            if (mask == null || envelope <= 0.03f) {
                return;
            }
            Vector2 pos = feet - Main.screenPosition + new Vector2(0f, 3f);
            float wobble = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + identitySeed) * 0.06f;
            float width = MathHelper.Lerp(24f, 96f, envelope) * wobble * widthScale;
            float height = MathHelper.Lerp(4f, 12f, envelope);
            Vector2 origin = mask.Size() * 0.5f;
            Vector2 scale = new(width / mask.Width, height / mask.Height);

            sb.Draw(mask, pos, null, KikasaThrall.SewageDark * (0.72f * envelope),
                0f, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(mask, pos - new Vector2(0f, 1.5f), null,
                (KikasaThrall.CorpseTeal with { A = 0 }) * (0.22f * envelope),
                0f, origin, scale * new Vector2(0.82f, 0.55f), SpriteEffects.None, 0f);
        }

        /// <summary>着色器缺失/失败的后备：按凝聚度淡入淡出</summary>
        internal static void DrawBodyFallback(SpriteBatch sb, Texture2D body, Rectangle frame,
            Vector2 center, float scale, SpriteEffects flip, Color light, float progress) {
            if (progress <= 0.01f) {
                return;
            }
            sb.Draw(body, center - Main.screenPosition, frame, light * progress,
                0f, frame.Size() * 0.5f, scale, flip, 0f);
        }

        /// <summary>恢复弹幕层标准批（与眼奴 DrawBody 的收尾一致）</summary>
        internal static bool RestoreProjectileBatch(SpriteBatch sb) {
            try {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null,
                    Main.GameViewMatrix.TransformationMatrix);
                return true;
            } catch (Exception exception) {
                LogRenderFailure(exception);
                return false;
            }
        }

        private static void TryEnd(SpriteBatch sb) {
            try {
                sb.End();
            } catch (Exception exception) {
                LogRenderFailure(exception);
            }
        }

        private static void LogRenderFailure(Exception exception) {
            if (renderFailureLogged) {
                return;
            }
            renderFailureLogged = true;
            CWRMod.Instance.Logger.Warn($"KikasaThrall renderer fallback: {exception.Message}");
        }
    }
}
