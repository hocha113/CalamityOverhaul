using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>英雄无冕：时装饰品，行走时身后铺开一路黑白棋格</summary>
    internal class Unsunghero : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "Unsunghero";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.vanity = true;
            Item.value = Item.buyPrice(0, 15, 22, 0);
            Item.rare = ItemRarityID.Orange;
        }

        //时装饰品放进功能栏走这里，尊重可见性开关
        public override void UpdateAccessory(Player player, bool hideVisual) {
            if (!hideVisual) {
                player.GetModPlayer<UnsungheroPlayer>().TrailActive = true;
            }
        }

        //时装栏
        public override void UpdateVanity(Player player)
            => player.GetModPlayer<UnsungheroPlayer>().TrailActive = true;
    }

    /// <summary>
    /// 棋格拖尾的逐玩家路径记录：等距采样让格列均匀、寿命走 <see cref="Main.GameUpdateCount"/>
    /// 时间戳(暂停即冻结、死亡期间照常流逝)、单帧大位移视为传送直接斩断。
    /// 装备状态经原版装备同步在各端可见，路径由各客户端本地自采，无需网络包
    /// </summary>
    internal class UnsungheroPlayer : ModPlayer
    {
        /// <summary>格边长(px)，uv.x 每 +1 即一列格</summary>
        public const float CellSize = 18f;
        /// <summary>横向格行数，与 UnsungheroChess.fx 内 Rows 保持一致</summary>
        public const int Rows = 3;
        public const float HalfWidth = CellSize * Rows * 0.5f;
        /// <summary>单点寿命(tick)</summary>
        public const int Lifetime = 150;
        /// <summary>路径采样步长(px)</summary>
        private const float SampleStep = 6f;
        /// <summary>单帧位移超过该值视为传送</summary>
        private const float TeleportBreak = 130f;
        private const int MaxPoints = 220;

        public struct TrailPoint
        {
            public Vector2 Pos;
            /// <summary>累计弧长(px)</summary>
            public float Dist;
            /// <summary>过期时刻(GameUpdateCount)</summary>
            public long DeathAt;
        }

        /// <summary>本帧是否处于生效装备状态，由物品钩子逐帧点亮</summary>
        public bool TrailActive;
        /// <summary>旧点在前新点在后</summary>
        public readonly List<TrailPoint> Points = new(MaxPoints + 4);

        public override void ResetEffects() => TrailActive = false;

        public override void UpdateDead() => Prune();

        private void Prune() {
            long now = Main.GameUpdateCount;
            while (Points.Count > 0 && Points[0].DeathAt <= now) {
                Points.RemoveAt(0);
            }
        }

        public override void PostUpdate() {
            if (Main.dedServ) {
                return;
            }

            Prune();

            if (!TrailActive || Player.dead) {
                return;
            }

            long deathAt = Main.GameUpdateCount + Lifetime;
            Vector2 anchor = Player.Center;
            if (Points.Count == 0) {
                Points.Add(new TrailPoint { Pos = anchor, Dist = 0f, DeathAt = deathAt });
                return;
            }

            TrailPoint last = Points[^1];
            float move = Vector2.Distance(last.Pos, anchor);
            if (move > TeleportBreak) {
                Points.Clear();
                Points.Add(new TrailPoint { Pos = anchor, Dist = 0f, DeathAt = deathAt });
                return;
            }

            //一帧跨多个步长时补插中间点，保证格列间距恒定
            while (move >= SampleStep) {
                Vector2 dir = (anchor - last.Pos) / move;
                last = new TrailPoint {
                    Pos = last.Pos + dir * SampleStep,
                    Dist = last.Dist + SampleStep,
                    DeathAt = deathAt
                };
                Points.Add(last);
                move = Vector2.Distance(last.Pos, anchor);
            }

            if (Points.Count > MaxPoints) {
                Points.RemoveRange(0, Points.Count - MaxPoints);
            }
        }
    }

    /// <summary>
    /// 棋格拖尾顶点绘制层。<see cref="RenderHandle.DrawBeforePlayers"/> 每帧被
    /// BehindNPCs 与主玩家层各触发一次，用 <see cref="RenderHandle.DrawAfterTiles"/>
    /// 上膛、首次消费的闩锁保证只画一次，且落在 NPC/弹幕/玩家之下的地面贴花层
    /// </summary>
    internal sealed class UnsungheroTrailRender : RenderHandle
    {
        private static bool armed;
        private static VertexPositionColorTexture[] vertexBuf = new VertexPositionColorTexture[128];

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu) {
                return;
            }
            armed = false;

            Effect effect = EffectLoader.UnsungheroChess?.Value;
            if (effect == null) {
                return;
            }

            bool deviceReady = false;
            BlendState origBlend = null;
            RasterizerState origRaster = null;

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active
                    || !player.TryGetModPlayer(out UnsungheroPlayer mp) || mp.Points.Count < 2) {
                    continue;
                }
                if (!TrailOnScreen(mp.Points)) {
                    continue;
                }

                if (!deviceReady) {
                    deviceReady = true;
                    origBlend = graphicsDevice.BlendState;
                    origRaster = graphicsDevice.RasterizerState;
                    graphicsDevice.BlendState = BlendState.AlphaBlend;
                    graphicsDevice.RasterizerState = RasterizerState.CullNone;

                    float zoom = Main.GameViewMatrix.Zoom.X;
                    float aa = MathHelper.Clamp(1.2f / (UnsungheroPlayer.CellSize * zoom), 0.01f, 0.12f);
                    effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                    effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    effect.Parameters["uAA"]?.SetValue(aa);
                }

                DrawTrail(graphicsDevice, effect, mp.Points);
            }

            if (deviceReady) {
                graphicsDevice.BlendState = origBlend;
                graphicsDevice.RasterizerState = origRaster;
            }
        }

        /// <summary>包围盒粗剔除，整条拖尾在屏外(含条带宽度余量)则跳过</summary>
        private static bool TrailOnScreen(List<UnsungheroPlayer.TrailPoint> pts) {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++) {
                Vector2 p = pts[i].Pos;
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            float pad = UnsungheroPlayer.HalfWidth + 40f;
            Vector2 screen = Main.screenPosition;
            return maxX + pad >= screen.X && minX - pad <= screen.X + Main.screenWidth
                && maxY + pad >= screen.Y && minY - pad <= screen.Y + Main.screenHeight;
        }

        private static void DrawTrail(GraphicsDevice device, Effect effect, List<UnsungheroPlayer.TrailPoint> pts) {
            int count = pts.Count;
            if (vertexBuf.Length < count * 2) {
                vertexBuf = new VertexPositionColorTexture[count * 2 + 32];
            }

            long now = Main.GameUpdateCount;
            Vector2 prevNormal = default;
            for (int i = 0; i < count; i++) {
                Vector2 pos = pts[i].Pos;
                //中心差分切向，路径折返时翻转法线保持条带连续不打结
                Vector2 dirA = i > 0 ? pos - pts[i - 1].Pos : pts[i + 1].Pos - pos;
                Vector2 dirB = i < count - 1 ? pts[i + 1].Pos - pos : pos - pts[i - 1].Pos;
                Vector2 normal = (dirA + dirB).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                if (i > 0 && Vector2.Dot(normal, prevNormal) < 0f) {
                    normal = -normal;
                }
                prevNormal = normal;

                float lifeT = MathHelper.Clamp((pts[i].DeathAt - now) / (float)UnsungheroPlayer.Lifetime, 0f, 1f);
                Color light = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                int lum = light.R > light.G ? light.R : light.G;
                if (light.B > lum) {
                    lum = light.B;
                }
                //顶点色 R=剩余寿命 G=光照亮度，与 fx 契约一致
                Color data = new(lifeT, lum / 255f, 0f, 1f);

                float u = pts[i].Dist / UnsungheroPlayer.CellSize;
                Vector2 off = normal * UnsungheroPlayer.HalfWidth;
                vertexBuf[i * 2] = new VertexPositionColorTexture(
                    new Vector3(pos.X + off.X, pos.Y + off.Y, 0f), data, new Vector2(u, 0f));
                vertexBuf[i * 2 + 1] = new VertexPositionColorTexture(
                    new Vector3(pos.X - off.X, pos.Y - off.Y, 0f), data, new Vector2(u, 1f));
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexBuf, 0, count * 2 - 2);
            }
        }
    }
}
