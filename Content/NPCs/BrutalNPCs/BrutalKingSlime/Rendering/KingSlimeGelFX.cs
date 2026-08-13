using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering
{
    /// <summary>皇家凝胶视效派发：调色板、飞溅包、音效包、地面扫描、世界四边形着色器绘制</summary>
    internal static class KingSlimeGelFX
    {
        #region 调色板
        /// <summary>深层凝胶，暗蓝紫</summary>
        public static readonly Color GelDeep = new(30, 42, 130);
        /// <summary>中层皇家蓝</summary>
        public static readonly Color GelMid = new(70, 112, 235);
        /// <summary>泡沫淡蓝白</summary>
        public static readonly Color GelFoam = new(178, 214, 255);
        /// <summary>王冠金</summary>
        public static readonly Color CrownGold = new(255, 208, 96);
        /// <summary>王冠深金</summary>
        public static readonly Color CrownAmber = new(226, 148, 42);
        /// <summary>原版史莱姆王尘埃蓝(带透明)</summary>
        public static readonly Color DustBlue = new(78, 136, 255, 80);
        #endregion

        #region 通用工具

        /// <summary>从给定点向下扫到实心地表，返回地表顶点；扫不到回退原点下方</summary>
        public static Vector2 FindGroundBelow(Vector2 worldPos, int maxTiles = 120) {
            Point tile = worldPos.ToTileCoordinates();
            int x = Math.Clamp(tile.X, 10, Main.maxTilesX - 10);
            int y = Math.Clamp(tile.Y, 10, Main.maxTilesY - 10);
            for (int i = 0; i < maxTiles && y + i < Main.maxTilesY - 10; i++) {
                Tile t = Main.tile[x, y + i];
                if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return new Vector2(x * 16f + 8f, (y + i) * 16f);
                }
            }
            return worldPos + new Vector2(0f, maxTiles * 16f);
        }

        /// <summary>是否屏内(含边距)</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>定向震屏，服务端与配置关闭时忽略</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames, string id, Vector2? dir = null) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 direction = dir ?? Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, direction, strength, 7f, frames, 2200f, id);
            Main.instance.CameraModifiers.Add(modifier);
        }

        #endregion

        #region 音效包

        /// <summary>凝胶重落地：湿击+低频闷响</summary>
        public static void ThudSound(Vector2 pos, float power) {
            float p = MathHelper.Clamp(power / 20f, 0f, 1f);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.7f + p * 0.15f, Volume = 0.7f + p * 0.5f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.5f, Volume = 0.5f + p * 0.4f, MaxInstances = 3 }, pos);
            if (p > 0.55f) {
                SoundEngine.PlaySound(SoundID.Item167 with { Pitch = -0.9f, Volume = 0.32f + p * 0.25f, MaxInstances = 2 }, pos);
            }
        }

        /// <summary>凝胶弹性挤压声</summary>
        public static void SquishSound(Vector2 pos, float pitch = 0f, float volume = 0.7f) {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = pitch, Volume = volume, MaxInstances = 4 }, pos);
        }

        /// <summary>王冠金属鸣响</summary>
        public static void CrownChime(Vector2 pos, float pitch = 0.2f, float volume = 0.8f) {
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = pitch, Volume = volume, MaxInstances = 3 }, pos);
        }

        #endregion

        #region 飞溅包(客户端)

        /// <summary>落地凝胶爆裂：两侧扇形珠+贴地渍+尘+泡</summary>
        public static void LandingBurst(Vector2 pos, float power, float sizeMul = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            float p = MathHelper.Clamp(power / 16f, 0.3f, 1.6f);
            int beads = (int)(10 * p * sizeMul);
            for (int i = 0; i < beads; i++) {
                //落地飞溅主要往两侧上方走
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 vel = new Vector2(side * Main.rand.NextFloat(2f, 7.5f) * p, -Main.rand.NextFloat(3f, 9f) * p);
                Color c = Color.Lerp(GelMid, GelDeep, Main.rand.NextFloat()) * 0.85f;
                PRTLoader.NewParticle<PRT_BKSGelBead>(pos + new Vector2(side * Main.rand.NextFloat(10f, 46f) * sizeMul, -6f),
                    vel, c, Main.rand.NextFloat(0.8f, 1.5f) * sizeMul)?.Configure(Main.rand.Next(26, 44));
            }
            int splats = (int)(4 * p * sizeMul);
            for (int i = 0; i < splats; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 vel = new Vector2(side * Main.rand.NextFloat(3f, 8f) * p, -Main.rand.NextFloat(2f, 6f) * p);
                PRTLoader.NewParticle<PRT_BKSGelSplat>(pos + new Vector2(0, -8f), vel,
                    Color.Lerp(GelMid, GelDeep, Main.rand.NextFloat()) * 0.9f,
                    Main.rand.NextFloat(0.9f, 1.6f) * sizeMul)?.Configure(Main.rand.Next(30, 46), 0.42f, Main.rand.Next(40, 60));
            }
            for (int i = 0; i < (int)(14 * p); i++) {
                Dust d = Dust.NewDustDirect(pos - new Vector2(40f * sizeMul, 10f), (int)(80 * sizeMul), 12,
                    DustID.TintableDust, 0, 0, 150, DustBlue, Main.rand.NextFloat(1.1f, 2f));
                d.noGravity = true;
                d.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f) * p, -Main.rand.NextFloat(1f, 4f) * p);
            }
            BubbleFizz(pos - new Vector2(0, 12f), 46f * sizeMul, (int)(4 * p));
        }

        /// <summary>定向凝胶飞溅锥</summary>
        public static void GelSplatter(Vector2 pos, Vector2 dir, int count, float speed, float sizeMul = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(0.6) * Main.rand.NextFloat(0.5f, 1f) * speed;
                Color c = Color.Lerp(GelMid, GelDeep, Main.rand.NextFloat()) * 0.85f;
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_BKSGelSplat>(pos, vel, c, Main.rand.NextFloat(0.8f, 1.4f) * sizeMul)
                        ?.Configure(Main.rand.Next(26, 42), 0.42f, Main.rand.Next(36, 54));
                }
                else {
                    PRTLoader.NewParticle<PRT_BKSGelBead>(pos, vel, c, Main.rand.NextFloat(0.7f, 1.4f) * sizeMul)
                        ?.Configure(Main.rand.Next(22, 40));
                }
            }
        }

        /// <summary>区域冒泡</summary>
        public static void BubbleFizz(Vector2 center, float radius, int count) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(radius, radius * 0.5f);
                PRTLoader.NewParticle<PRT_BKSBubble>(pos, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.4f)),
                    GelFoam * 0.7f, Main.rand.NextFloat(0.7f, 1.5f));
            }
        }

        /// <summary>王冠金屑迸溅</summary>
        public static void GoldGlint(Vector2 pos, int count, float speed = 5f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, speed);
                PRTLoader.NewParticle<PRT_BKSGoldSpark>(pos, vel,
                    Color.Lerp(CrownGold, Color.White, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.9f, 1.7f))
                    ?.Configure(Main.rand.Next(14, 28), Main.rand.NextBool());
            }
        }

        #endregion

        #region 世界四边形着色器绘制

        /// <summary>
        /// 以世界坐标画一个过 shader 的四边形；顶点接收世界坐标(变换矩阵内含屏偏移)
        /// </summary>
        public static void DrawShaderQuad(Effect effect, Texture2D noise, Vector2 worldCenter, Vector2 size, float alphaEnvelope) {
            if (effect == null || noise == null) {
                return;
            }

            Vector2 half = size * 0.5f;
            Color vc = Color.White * alphaEnvelope;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(worldCenter.X - half.X, worldCenter.Y - half.Y, 0f), vc, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(worldCenter.X + half.X, worldCenter.Y - half.Y, 0f), vc, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(worldCenter.X - half.X, worldCenter.Y + half.Y, 0f), vc, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(worldCenter.X + half.X, worldCenter.Y + half.Y, 0f), vc, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 画一条凝胶潮体条带：沿 dir 方向 length 长、高 height，
        /// uv.x 尾0→头1，crestEnergy 写入顶点色R供波峰强调
        /// </summary>
        public static void DrawSurgeStrip(Effect effect, Texture2D noise, Vector2 groundHead, Vector2 dir,
            float length, float height, float crestEnergy, float alphaEnvelope, int segments = 14) {
            if (effect == null || noise == null || length < 8f || height < 4f) {
                return;
            }

            segments = Math.Clamp(segments, 2, 40);
            Vector2 up = -Vector2.UnitY;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[(segments + 1) * 2];
            for (int i = 0; i <= segments; i++) {
                float t = i / (float)segments;
                //尾在头后方 length 处
                Vector2 basePos = groundHead - dir * (1f - t) * length;
                //沿地形贴地：逐段向下找地表
                Vector2 ground = FindGroundBelow(basePos - new Vector2(0f, 24f), 14);
                //波体高度：头部隆起高，尾部矮
                float h = height * (0.35f + 0.65f * MathF.Pow(t, 1.6f));
                float crest = crestEnergy * MathF.Pow(t, 2.2f);
                Color topColor = new Color(crest, h / Math.Max(height, 1f), 0f, alphaEnvelope);
                verts[i * 2] = new VertexPositionColorTexture((ground + up * h).ToVector3(), topColor, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(ground.ToVector3(), topColor, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, segments * 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>设置 BKSGelSurge 的公共参数</summary>
        public static void SetSurgeParams(Effect effect, float flow, float foam, float alpha, float edgeGlow, float churn, float seed) {
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFlow"]?.SetValue(flow);
            effect.Parameters["uFoam"]?.SetValue(foam);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uEdgeGlow"]?.SetValue(edgeGlow);
            effect.Parameters["uChurn"]?.SetValue(churn);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uColorDeep"]?.SetValue(GelDeep.ToVector3());
            effect.Parameters["uColorMid"]?.SetValue(GelMid.ToVector3());
            effect.Parameters["uColorFoam"]?.SetValue(GelFoam.ToVector3());
        }

        /// <summary>设置 BKSGelPool 的公共参数</summary>
        public static void SetPoolParams(Effect effect, float spread, float drain, float alpha, float boil, float seed) {
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSpread"]?.SetValue(spread);
            effect.Parameters["uDrain"]?.SetValue(drain);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uBoil"]?.SetValue(boil);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uColorDeep"]?.SetValue(GelDeep.ToVector3());
            effect.Parameters["uColorMid"]?.SetValue(GelMid.ToVector3());
            effect.Parameters["uColorFoam"]?.SetValue(GelFoam.ToVector3());
        }

        #endregion
    }
}
