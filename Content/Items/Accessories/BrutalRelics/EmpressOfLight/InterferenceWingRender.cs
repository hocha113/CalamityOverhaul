using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EmpressOfLight
{
    /// <summary>
    /// 昼夜干涉之翼顶点绘制层：程序化极光双翼（BRelicAuroraWing）+干涉光径
    /// （复用 EmpressLanceBeam TelegraphTech 的色散折光线）。
    /// <see cref="RenderHandle.DrawBeforePlayers"/> 每帧被 BehindNPCs 与主玩家层各触发一次，
    /// 用 <see cref="RenderHandle.DrawAfterTiles"/> 上膛、首次消费的闩锁保证只画一次，
    /// 且落在 NPC/弹幕/玩家之下（翼在身后、光径垫在弹幕下）
    /// </summary>
    internal sealed class WingsOfInterferenceRender : RenderHandle
    {
        /// <summary>认领表权重槽 1.87（光女 Boss 自身的屏幕后效在 1.088，互不干涉）</summary>
        public override float Weight => 1.87f;

        /// <summary>每侧羽数</summary>
        private const int FeatherCount = 5;
        /// <summary>光径条带半宽(px)</summary>
        private const float TrailHalfWidth = 9f;

        private static bool armed;
        private static readonly VertexPositionColorTexture[] featherVerts = new VertexPositionColorTexture[4];
        private static VertexPositionColorTexture[] trailVerts = new VertexPositionColorTexture[128];

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu) {
                return;
            }
            armed = false;

            //帧戳门：无任何翼/光径在场时跳过全玩家表扫描
            if (!WingsOfInterferencePlayer.PresenceStamp.ActiveWithin()) {
                return;
            }

            bool deviceReady = false;
            BlendState origBlend = null;
            RasterizerState origRaster = null;

            try {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player == null || !player.active
                        || !player.TryGetModPlayer(out WingsOfInterferencePlayer mp)) {
                        continue;
                    }
                    bool hasWings = mp.WingSpread > 0.02f && !player.dead;
                    bool hasTrails = mp.Trails.Count > 0;
                    if (!hasWings && !hasTrails) {
                        continue;
                    }

                    if (!deviceReady) {
                        deviceReady = true;
                        origBlend = graphicsDevice.BlendState;
                        origRaster = graphicsDevice.RasterizerState;
                        graphicsDevice.BlendState = BlendState.Additive;
                        graphicsDevice.RasterizerState = RasterizerState.CullNone;
                    }

                    if (hasWings && PlayerOnScreen(player)) {
                        DrawWings(graphicsDevice, player, mp);
                    }
                    if (hasTrails) {
                        DrawTrails(graphicsDevice, mp);
                    }
                }
            } finally {
                //绘制中途抛异常也要还原混合态，防泄漏 Additive 污染后续玩家层
                if (deviceReady) {
                    graphicsDevice.BlendState = origBlend;
                    graphicsDevice.RasterizerState = origRaster;
                }
            }
        }

        private static bool PlayerOnScreen(Player player) {
            const float Pad = 260f;
            Vector2 screen = Main.screenPosition;
            return player.Center.X + Pad >= screen.X && player.Center.X - Pad <= screen.X + Main.screenWidth
                && player.Center.Y + Pad >= screen.Y && player.Center.Y - Pad <= screen.Y + Main.screenHeight;
        }

        /// <summary>
        /// 程序化极光双翼：每侧5根羽条quad，展开角/羽长随 WingSpread 缓动，
        /// 扑动波从内羽向外羽传播；材质参数打进顶点色（R=色相 G=昼白金 B=收拢褶皱 A=强度）
        /// </summary>
        private static void DrawWings(GraphicsDevice device, Player player, WingsOfInterferencePlayer mp) {
            Effect effect = EffectLoader.BRelicAuroraWing?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            float spread = mp.WingSpread;
            float grav = player.gravDir;
            Vector2 anchor = player.MountedCenter + new Vector2(-player.direction * 7f, -4f * grav);
            float baseHue = mp.WingFlap * 0.018f;
            float whiten = mp.DayBlend;
            float fold = 1f - spread;
            float flapAmp = 0.05f + 0.11f * spread;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                for (int i = 0; i < FeatherCount; i++) {
                    float t = i / (float)(FeatherCount - 1);
                    //收拢窄扇贴背，展开成近水平的宽扇
                    float offFold = MathHelper.Lerp(0.50f, 0.92f, t);
                    float offOpen = MathHelper.Lerp(0.22f, 1.54f, t);
                    float off = MathHelper.Lerp(offFold, offOpen, spread);
                    //扑动波内羽先动向外传播
                    off += (float)Math.Sin(mp.WingFlap - i * 0.45f) * flapAmp;

                    float angle = -MathHelper.PiOver2 - player.direction * off;
                    Vector2 dir = new((float)Math.Cos(angle), (float)Math.Sin(angle) * grav);
                    Vector2 perp = new(-dir.Y, dir.X);

                    float len = MathHelper.Lerp(88f, 48f, t) * (0.5f + 0.5f * spread);
                    len *= 1f + (float)Math.Sin(mp.WingFlap * 0.7f + i * 0.6f) * 0.045f;
                    float rootHalf = 3.5f;
                    float tipHalf = 14f + 8f * spread;

                    Vector2 tip = anchor + dir * len;
                    float hue = (baseHue + i * 0.085f) % 1f;
                    if (hue < 0f) {
                        hue += 1f;
                    }
                    Color data = new(hue, whiten, fold, MathHelper.Clamp(spread * 0.95f, 0f, 1f));

                    featherVerts[0] = new VertexPositionColorTexture((anchor + perp * rootHalf).ToVector3(), data, new Vector2(0f, 0f));
                    featherVerts[1] = new VertexPositionColorTexture((anchor - perp * rootHalf).ToVector3(), data, new Vector2(0f, 1f));
                    featherVerts[2] = new VertexPositionColorTexture((tip + perp * tipHalf).ToVector3(), data, new Vector2(1f, 0f));
                    featherVerts[3] = new VertexPositionColorTexture((tip - perp * tipHalf).ToVector3(), data, new Vector2(1f, 1f));
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, featherVerts, 0, 2);
                }
            }
        }

        /// <summary>
        /// 干涉光径：轨迹条带套 EmpressLanceBeam 预告折光线材质
        /// （细亮芯+色散侧纹+沿线奔跑的装填光头），uv.x 0旧端→1弹幕端
        /// </summary>
        private static void DrawTrails(GraphicsDevice device, WingsOfInterferencePlayer mp) {
            Effect effect = EffectLoader.EmpressLanceBeam?.Value;
            if (effect == null) {
                return;
            }
            effect.CurrentTechnique = effect.Techniques["TelegraphTech"];
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            //低充能档：光径是过程线不是发射预告，不推白
            effect.Parameters["uProgress"]?.SetValue(0.3f);

            foreach (InterferenceTrail trail in mp.Trails) {
                int count = trail.Points.Count;
                if (count < 2 || !TrailOnScreen(trail)) {
                    continue;
                }
                float env = trail.Alive ? 1f : 1f - trail.LingerTimer / (float)WingsOfInterferencePlayer.TrailLingerFrames;
                if (env <= 0.02f) {
                    continue;
                }

                effect.Parameters["uHue"]?.SetValue(trail.Hue);
                effect.Parameters["uOpacity"]?.SetValue(env * 0.85f);

                if (trailVerts.Length < count * 2) {
                    trailVerts = new VertexPositionColorTexture[count * 2 + 32];
                }
                Vector2 prevNormal = default;
                for (int i = 0; i < count; i++) {
                    Vector2 pos = trail.Points[i];
                    //中心差分切向，折返翻转法线防条带打结
                    Vector2 dirA = i > 0 ? pos - trail.Points[i - 1] : trail.Points[i + 1] - pos;
                    Vector2 dirB = i < count - 1 ? trail.Points[i + 1] - pos : pos - trail.Points[i - 1];
                    Vector2 normal = (dirA + dirB).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                    if (i > 0 && Vector2.Dot(normal, prevNormal) < 0f) {
                        normal = -normal;
                    }
                    prevNormal = normal;

                    float u = i / (float)(count - 1);
                    Vector2 off = normal * TrailHalfWidth;
                    trailVerts[i * 2] = new VertexPositionColorTexture((pos + off).ToVector3(), Color.White, new Vector2(u, 0f));
                    trailVerts[i * 2 + 1] = new VertexPositionColorTexture((pos - off).ToVector3(), Color.White, new Vector2(u, 1f));
                }

                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, trailVerts, 0, count * 2 - 2);
                }
            }
        }

        private static bool TrailOnScreen(InterferenceTrail trail) {
            const float Pad = 60f;
            Vector2 screen = Main.screenPosition;
            return trail.Max.X + Pad >= screen.X && trail.Min.X - Pad <= screen.X + Main.screenWidth
                && trail.Max.Y + Pad >= screen.Y && trail.Min.Y - Pad <= screen.Y + Main.screenHeight;
        }
    }
}
