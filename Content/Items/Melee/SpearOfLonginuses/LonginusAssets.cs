using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>朗基努斯专用着色器</summary>
    internal class LonginusAssets
    {
        /// <summary>AT力场八边形</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusATField { get; set; }
        /// <summary>十字光柱</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusCross { get; set; }
        /// <summary>双螺旋尾迹</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusHelix { get; set; }
        /// <summary>光轮</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusHalo { get; set; }
        /// <summary>处决冲击帧</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusImpact { get; set; }
        /// <summary>充能吸入场</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusCharge { get; set; }
        /// <summary>光之翼翼羽条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> LonginusWing { get; set; }
    }

    /// <summary>光之翼玩家身后图层，遍历活跃 <see cref="LonginusHeld"/> 各画各的</summary>
    internal sealed class LonginusWingsRender : RenderHandle
    {
        public override float Weight => 1.12f;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            //近两帧无持握弹幕盖戳（无人持枪）：跳过全弹幕表扫描
            if (!LonginusHeld.PresenceStamp.ActiveWithin()) {
                return;
            }
            foreach (Projectile proj in Main.projectile) {
                if (!proj.active || proj.ModProjectile is not LonginusHeld held) {
                    continue;
                }
                held.DrawWingsLayer();
            }
        }
    }

    /// <summary>处决终结冲击帧，1~3帧高对比白闪剪影后急退</summary>
    internal sealed class LonginusImpactRender : RenderHandle
    {
        private const int ImpactLife = 9;
        /// <summary>两次冲击帧至少间隔 1.5 秒</summary>
        private const uint MinInterval = 90;

        private static int impactAge = ImpactLife;
        private static float impactStrength;
        private static Vector2 impactCenter = new(0.5f, 0.5f);
        private static uint lastTriggerTick;

        public override float Weight => 1.10f;

        /// <summary>触发冲击帧(客户端)，worldPos 为处决点；限频与RT技术门内部处理</summary>
        public static void Trigger(float strength, Vector2 worldPos) {
            if (Main.dedServ || RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            if (Main.GameUpdateCount - lastTriggerTick < MinInterval) {
                return;
            }
            lastTriggerTick = Main.GameUpdateCount;
            impactAge = 0;
            impactStrength = strength;
            Vector2 uv = (worldPos - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            impactCenter = new Vector2(MathHelper.Clamp(uv.X, 0f, 1f), MathHelper.Clamp(uv.Y, 0f, 1f));
        }

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (Main.gameMenu || impactAge >= ImpactLife) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                impactAge = ImpactLife;
                return;
            }
            Effect shader = LonginusAssets.LonginusImpact?.Value;
            if (shader == null) {
                impactAge = ImpactLife;
                return;
            }

            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(impactAge / (float)ImpactLife, 0f, 1f));
            shader.Parameters["uIntensity"]?.SetValue(impactStrength);
            shader.Parameters["uCenter"]?.SetValue(impactCenter);

            //拷屏到 screenSwap 再带 shader 写回 screenTarget
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();

            impactAge++;
        }
    }

    /// <summary>朗基努斯共用图元绘制与配色</summary>
    internal static class LonginusVFX
    {
        /// <summary>AT力场琥珀橙</summary>
        public static readonly Color Amber = new(255, 158, 40);
        /// <summary>圣光金</summary>
        public static readonly Color HolyGold = new(255, 214, 96);
        /// <summary>枪体绯红</summary>
        public static readonly Color Crimson = new(232, 36, 48);

        /// <summary>
        /// 层叠AT力场 quad，沿 normal 方向近大远小错相排开<br/>
        /// normal 指向来袭方向；squash=1 为正对镜头平面，越小越侧倾
        /// </summary>
        public static void DrawATField(Vector2 center, Vector2 normal, float radius, float spread
            , float shatter, float alphaMul, int layers = 3, float phaseSeed = 0f, float squash = 0.62f) {
            Effect effect = LonginusAssets.LonginusATField?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || radius < 2f) {
                return;
            }

            normal = normal.UnitVector();
            if (normal == Vector2.Zero) {
                normal = Vector2.UnitX;
            }
            Vector2 perp = normal.RotatedBy(MathHelper.PiOver2);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            for (int i = 0; i < layers; i++) {
                float layerSpread = MathHelper.Clamp(spread * 1.15f - i * 0.18f, 0f, 1f);
                float layerShatter = MathHelper.Clamp(shatter * 1.2f - i * 0.15f, 0f, 1f);
                if (layerSpread <= 0.001f) {
                    continue;
                }
                float scale = 1f - i * 0.13f;
                Vector2 c = center + normal * (radius * 0.24f * i);
                Vector2 a = normal * (radius * squash * scale);
                Vector2 b = perp * (radius * scale);
                Vector2 shear = perp * (radius * 0.07f * i);
                Color tint = Color.White * (alphaMul * (1f - i * 0.24f));

                VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
                quad[0] = new((c + a + b + shear).ToVector3(), tint, new Vector2(0f, 0f));
                quad[1] = new((c + a - b + shear).ToVector3(), tint, new Vector2(0f, 1f));
                quad[2] = new((c - a + b - shear).ToVector3(), tint, new Vector2(1f, 0f));
                quad[3] = new((c - a - b - shear).ToVector3(), tint, new Vector2(1f, 1f));

                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSpread"]?.SetValue(layerSpread);
                effect.Parameters["uShatter"]?.SetValue(layerShatter);
                effect.Parameters["uPhase"]?.SetValue(phaseSeed + i * 0.37f);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 拉丁十字光柱 quad<br/>
        /// up=立轴上端方向，halfLength=立柱半长px，halfWidth=横臂半展px<br/>
        /// widthUnits=柱体半厚(横向归一单位)，fill 自下而上点亮(计量用)
        /// </summary>
        public static void DrawCross(Vector2 center, Vector2 up, float halfLength, float halfWidth
            , float grow, float dissolve, float alphaMul, float widthUnits = 0.12f, float hot = 0f
            , float fill = 1f, Color? tint = null) {
            Effect effect = LonginusAssets.LonginusCross?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || grow <= 0.001f || dissolve >= 0.999f) {
                return;
            }

            up = up.UnitVector();
            if (up == Vector2.Zero) {
                up = -Vector2.UnitY;
            }
            Vector2 perp = up.RotatedBy(MathHelper.PiOver2) * halfWidth;
            Vector2 top = center + up * halfLength;
            Vector2 bottom = center - up * halfLength;
            Color color = (tint ?? Color.White) * alphaMul;

            VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
            quad[0] = new((top + perp).ToVector3(), color, new Vector2(0f, 0f));
            quad[1] = new((top - perp).ToVector3(), color, new Vector2(0f, 1f));
            quad[2] = new((bottom + perp).ToVector3(), color, new Vector2(1f, 0f));
            quad[3] = new((bottom - perp).ToVector3(), color, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uDissolve"]?.SetValue(dissolve);
            effect.Parameters["uFill"]?.SetValue(fill);
            effect.Parameters["uAspect"]?.SetValue(halfLength / halfWidth);
            effect.Parameters["uWidth"]?.SetValue(widthUnits);
            effect.Parameters["uHot"]?.SetValue(hot);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 圣光光轮 quad，squash 竖向压扁模拟倾斜冠环
        /// </summary>
        public static void DrawHalo(Vector2 center, float radius, float squash, float reveal
            , float pulse, float alphaMul, Color? tint = null) {
            Effect effect = LonginusAssets.LonginusHalo?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || reveal <= 0.01f || radius < 2f) {
                return;
            }

            float rx = radius * 1.45f;
            float ry = radius * squash * 1.45f;
            Color color = (tint ?? Color.White) * alphaMul;

            VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
            quad[0] = new(new Vector3(center.X - rx, center.Y - ry, 0f), color, new Vector2(0f, 0f));
            quad[1] = new(new Vector3(center.X + rx, center.Y - ry, 0f), color, new Vector2(1f, 0f));
            quad[2] = new(new Vector3(center.X - rx, center.Y + ry, 0f), color, new Vector2(0f, 1f));
            quad[3] = new(new Vector3(center.X + rx, center.Y + ry, 0f), color, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uReveal"]?.SetValue(reveal);
            effect.Parameters["uPulse"]?.SetValue(pulse);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>暗红副股</summary>
        private static readonly Color HelixDark = new(126, 18, 30);
        /// <summary>中轴体股暗红</summary>
        private static readonly Color WakeDark = new(92, 10, 16);

        /// <summary>
        /// 双螺旋尾迹，两股相位差 π 缠绕<br/>
        /// points 头→尾世界点列(count 个有效)，spinPhase 推进产生拧转，erode 尾先碎<br/>
        /// withWake 加一条无摆动的宽幅中轴体股垫底
        /// </summary>
        public static void DrawHelixTrail(Vector2[] points, int count, float baseWidth, float amplitude
            , float spinPhase, float erode, float alphaMul, float hot = 0.2f, float twists = 2.4f
            , bool withWake = false) {
            Effect effect = LonginusAssets.LonginusHelix?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || count < 3 || alphaMul <= 0.01f || erode >= 0.999f) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uErode"]?.SetValue(erode);
            effect.Parameters["uHot"]?.SetValue(hot);

            //s=2 为中轴体股，先画垫底
            int strandStart = withWake ? 2 : 0;
            VertexPositionColorTexture[] strip = new VertexPositionColorTexture[count * 2];
            for (int s = strandStart; s >= 0; s--) {
                bool isWake = s == 2;
                float phase0 = spinPhase + s * MathHelper.Pi;
                Color strand = (isWake ? WakeDark * 0.75f : s == 0 ? Crimson : HelixDark) * alphaMul;
                for (int i = 0; i < count; i++) {
                    float t = i / (count - 1f);
                    Vector2 pos = points[i];
                    Vector2 tangent = (points[System.Math.Min(i + 1, count - 1)] - points[System.Math.Max(i - 1, 0)]).UnitVector();
                    if (tangent == Vector2.Zero) {
                        tangent = Vector2.UnitX;
                    }
                    Vector2 perp = tangent.RotatedBy(MathHelper.PiOver2);

                    float ph = t * twists * MathHelper.TwoPi + phase0;
                    float lateral = isWake ? 0f : (float)System.Math.Sin(ph);
                    float depth01 = isWake ? 0.5f : (float)System.Math.Cos(ph) * 0.5f + 0.5f;

                    //振幅头部收拢尾部微敛
                    float amp = amplitude * MathHelper.Clamp(t / 0.22f, 0f, 1f) * (1f - t * 0.25f);
                    Vector2 center = pos + perp * lateral * amp;
                    //近侧股略粗；体股宽幅覆盖螺旋摆域
                    float halfW = isWake
                        ? (baseWidth * 1.6f + amplitude * 0.85f) * (1f - t * 0.42f)
                        : baseWidth * (1f - t * 0.5f) * (0.72f + 0.42f * depth01);

                    strip[i * 2] = new(new Vector3(center.X + perp.X * halfW, center.Y + perp.Y * halfW, depth01), strand, new Vector2(t, 0f));
                    strip[i * 2 + 1] = new(new Vector3(center.X - perp.X * halfW, center.Y - perp.Y * halfW, depth01), strand, new Vector2(t, 1f));
                }
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, strip, 0, count * 2 - 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 充能吸入场 quad，charge=进度强度，full=满层稳态聚核
        /// </summary>
        public static void DrawChargeIntake(Vector2 center, float radius, float charge, float full
            , float alphaMul, float phaseSeed = 0f) {
            Effect effect = LonginusAssets.LonginusCharge?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || alphaMul <= 0.01f || charge <= 0.01f || radius < 2f) {
                return;
            }

            Color color = Color.White * alphaMul;
            VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
            quad[0] = new(new Vector3(center.X - radius, center.Y - radius, 0f), color, new Vector2(0f, 0f));
            quad[1] = new(new Vector3(center.X + radius, center.Y - radius, 0f), color, new Vector2(1f, 0f));
            quad[2] = new(new Vector3(center.X - radius, center.Y + radius, 0f), color, new Vector2(0f, 1f));
            quad[3] = new(new Vector3(center.X + radius, center.Y + radius, 0f), color, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uCharge"]?.SetValue(charge);
            effect.Parameters["uFull"]?.SetValue(full);
            effect.Parameters["uPhase"]?.SetValue(phaseSeed);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
