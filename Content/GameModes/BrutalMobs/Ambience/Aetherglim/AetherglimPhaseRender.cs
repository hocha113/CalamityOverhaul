using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim
{
    /// <summary>
    /// 「相位闪」：微光空域里低频出现的地形幻影错位——附近某小块地形的半透明重影
    /// 偏移半格浮现、微微抖动数十帧后归位，纯视觉的空间异质感，无任何判定。
    /// 画在物块之后实体之前：幻影贴着地形本体，人和弹幕从它前面走过。
    /// 加色批只加光，画不出暗形——幻影恒为发光重影，不冒充阴影
    /// </summary>
    internal sealed class AetherglimPhaseRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.72</summary>
        public override float Weight => 1.72f;

        private const int MaxGhostTiles = 14;
        /// <summary>错位半格（像素）</summary>
        private const float OffsetPx = 8f;

        private readonly Point[] ghostTiles = new Point[MaxGhostTiles];
        private int ghostCount;
        private int cooldown = 500;
        private int life;
        private int lifeMax;
        private Vector2 offsetDir;
        private float hueSeed;

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            if (AetherglimAmbience.Presence < 0.55f) {
                life = 0;
                return;
            }
            if (life > 0) {
                life--;
                return;
            }
            if (--cooldown > 0) {
                return;
            }
            cooldown = 420 + Main.rand.Next(360);
            if (TryPickCluster()) {
                lifeMax = 38 + Main.rand.Next(12);
                life = lifeMax;
                //错位方向取四正向：像素世界的相位滑移沿轴走才读得出"错开半格"
                offsetDir = Main.rand.Next(4) switch {
                    0 => new Vector2(OffsetPx, 0f),
                    1 => new Vector2(-OffsetPx, 0f),
                    2 => new Vector2(0f, -OffsetPx),
                    _ => new Vector2(0f, OffsetPx),
                };
                hueSeed = Main.rand.NextFloat(6f);
            }
        }

        /// <summary>在视野内随机选一小簇整实体块（6×4 窗，取满 5 块才算成簇）</summary>
        private bool TryPickCluster() {
            ghostCount = 0;
            int originX = (int)((Main.screenPosition.X + Main.rand.NextFloat(120f, Main.screenWidth - 120f)) / 16f);
            int originY = (int)((Main.screenPosition.Y + Main.rand.NextFloat(120f, Main.screenHeight - 120f)) / 16f);
            if (!WorldGen.InWorld(originX, originY, 12)) {
                return false;
            }
            for (int dx = 0; dx < 6 && ghostCount < MaxGhostTiles; dx++) {
                for (int dy = 0; dy < 4 && ghostCount < MaxGhostTiles; dy++) {
                    int x = originX + dx;
                    int y = originY + dy;
                    Tile tile = Main.tile[x, y];
                    //只挑素整实体块：斜坡/半砖/平台的帧型偏移会让重影读作破图
                    if (!tile.HasTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]
                        || tile.Slope != SlopeType.Solid || tile.IsHalfBlock) {
                        continue;
                    }
                    ghostTiles[ghostCount++] = new Point(x, y);
                }
            }
            return ghostCount >= 5;
        }

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || life <= 0 || ghostCount == 0) {
                return;
            }
            float presence = AetherglimAmbience.Presence;
            if (presence < 0.3f) {
                return;
            }

            //包络：快速浮现 → 驻留明灭 → 尾段错位收敛归位
            float t = 1f - life / (float)lifeMax;
            float env = MathHelper.Clamp(t / 0.12f, 0f, 1f);
            float snapBack = 1f;
            if (t > 0.75f) {
                float back = (t - 0.75f) / 0.25f;
                snapBack = 1f - back * back;
                env *= MathHelper.Clamp((1f - t) / 0.08f, 0f, 1f) * 0.6f + 0.4f;
            }
            //明灭：3 帧一桶的相位哈希，闪烁而不是呼吸
            int bucket = life / 3;
            float flicker = 0.65f + 0.35f * MathF.Abs(MathF.Sin(bucket * 2.39f + hueSeed));
            //微抖：4 帧一跳的量化侧向抖动
            int jitterBucket = life / 4;
            Vector2 side = new(-offsetDir.Y / OffsetPx, offsetDir.X / OffsetPx);
            Vector2 jitter = side * (MathF.Sin(jitterBucket * 3.7f + hueSeed * 2f) * 1.5f);
            Vector2 offset = offsetDir * snapBack + jitter * snapBack;

            float alpha = 0.34f * env * flicker * presence;
            Color hueA = AetherglimFX.Iridescent(hueSeed + t * 1.5f);
            Color hueB = AetherglimFX.Iridescent(hueSeed + t * 1.5f + 2.4f);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < ghostCount; i++) {
                Point p = ghostTiles[i];
                Tile tile = Main.tile[p.X, p.Y];
                if (!tile.HasTile) {
                    continue;//闪烁途中被挖掉：这一格幻影随本体消失
                }
                Main.instance.LoadTiles(tile.TileType);
                Texture2D tex = TextureAssets.Tile[tile.TileType].Value;
                var src = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);
                Vector2 basePos = new Vector2(p.X * 16f, p.Y * 16f) - Main.screenPosition;
                //主重影+反向半程的冷色残影：色散双影读作相位错层
                spriteBatch.Draw(tex, basePos + offset, src, AetherglimFX.Tint(hueA, alpha),
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos - offset * 0.45f, src, AetherglimFX.Tint(hueB, alpha * 0.5f),
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
