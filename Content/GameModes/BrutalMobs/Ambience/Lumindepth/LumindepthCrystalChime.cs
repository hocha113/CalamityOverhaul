using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Lumindepth
{
    /// <summary>
    /// 晶铃共鸣（纯美观反馈，客户端本地）：靠近海蓝晶或水晶簇时奏一记清铃，
    /// 光纹涟漪自晶簇处扩散（涟漪由 <see cref="LumindepthAmbientRender"/> 描画）。
    /// 灾厄海蓝晶瓦块 ID 运行期懒解析，缺席时退化为只认原版水晶簇；
    /// 已鸣晶簇进冷却表，加全局最短间隔，站着不动不会被复读
    /// </summary>
    internal static class LumindepthCrystalChime
    {
        internal struct Ripple
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
            internal int MaxLife;
        }

        private struct Rung
        {
            internal bool Active;
            internal Point Pos;
            internal int Cooldown;
        }

        /// <summary>光纹涟漪池（只在本文件写入，描画层只读）</summary>
        internal static readonly Ripple[] Ripples = new Ripple[10];
        private static readonly Rung[] rungTable = new Rung[14];
        private static int scanIn;
        private static int chimeGate;
        /// <summary>-2 未解析 / -1 灾厄缺席或无此瓦 / 其余为瓦块 ID</summary>
        private static int seaPrismTileId = -2;

        private const int ScanInterval = 20;
        /// <summary>共鸣触达半径（像素，约 6.5 格）</summary>
        private const float RingRange = 104f;
        /// <summary>全局最短鸣响间隔</summary>
        private const int ChimeGateFrames = 90;
        /// <summary>单簇冷却基础值</summary>
        private const int PerCrystalCooldown = 840;

        internal static void Update(Player lp, float presence) {
            if (chimeGate > 0) {
                chimeGate--;
            }
            for (int i = 0; i < rungTable.Length; i++) {
                if (rungTable[i].Active && --rungTable[i].Cooldown <= 0) {
                    rungTable[i].Active = false;
                }
            }
            for (int i = 0; i < Ripples.Length; i++) {
                if (Ripples[i].Active && ++Ripples[i].Life >= Ripples[i].MaxLife) {
                    Ripples[i].Active = false;
                }
            }
            if (presence < 0.5f || --scanIn > 0) {
                return;
            }
            scanIn = ScanInterval;
            if (seaPrismTileId == -2) {
                //懒解析：CalamityMod.Tiles.SunkenSea.SeaPrism（海蓝晶结晶）
                seaPrismTileId = ModContent.TryFind("CalamityMod", "SeaPrism", out ModTile prism) ? prism.Type : -1;
            }
            if (chimeGate <= 0) {
                ScanAround(lp);
            }
        }

        /// <summary>以玩家为心的低频窗扫，一次至多鸣一簇</summary>
        private static void ScanAround(Player lp) {
            Point c = lp.Center.ToTileCoordinates();
            for (int dx = -7; dx <= 7; dx++) {
                for (int dy = -6; dy <= 6; dy++) {
                    int x = c.X + dx;
                    int y = c.Y + dy;
                    if (!WorldGen.InWorld(x, y, 10)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile) {
                        continue;
                    }
                    bool crystal = tile.TileType == TileID.Crystals
                        || (seaPrismTileId >= 0 && tile.TileType == seaPrismTileId);
                    if (!crystal) {
                        continue;
                    }
                    Vector2 wpos = new(x * 16f + 8f, y * 16f + 8f);
                    if (lp.Distance(wpos) > RingRange || IsRecentlyRung(x, y)) {
                        continue;
                    }
                    RingAt(wpos, x, y);
                    return;
                }
            }
        }

        /// <summary>同簇去重：4 格曼哈顿半径内视作同一簇</summary>
        private static bool IsRecentlyRung(int x, int y) {
            for (int i = 0; i < rungTable.Length; i++) {
                if (rungTable[i].Active
                    && Math.Abs(rungTable[i].Pos.X - x) + Math.Abs(rungTable[i].Pos.Y - y) < 5) {
                    return true;
                }
            }
            return false;
        }

        private static void RingAt(Vector2 wpos, int x, int y) {
            chimeGate = ChimeGateFrames;
            //记入冷却表；满员时顶替剩余冷却最短的一格
            int slot = 0;
            int minCd = int.MaxValue;
            for (int i = 0; i < rungTable.Length; i++) {
                if (!rungTable[i].Active) {
                    slot = i;
                    break;
                }
                if (rungTable[i].Cooldown < minCd) {
                    minCd = rungTable[i].Cooldown;
                    slot = i;
                }
            }
            rungTable[slot] = new Rung {
                Active = true,
                Pos = new Point(x, y),
                Cooldown = PerCrystalCooldown + Main.rand.Next(240),
            };

            //清铃：钟音提亮随机音高，垫一层晶体微光声
            SoundEngine.PlaySound(SoundID.Item35 with {
                Volume = 0.32f,
                Pitch = 0.25f + Main.rand.NextFloat(0.4f),
                MaxInstances = 3
            }, wpos);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.14f, Pitch = 0.5f, MaxInstances = 3 }, wpos);

            for (int i = 0; i < Ripples.Length; i++) {
                if (!Ripples[i].Active) {
                    Ripples[i] = new Ripple { Active = true, Pos = wpos, Life = 0, MaxLife = 52 };
                    break;
                }
            }
            for (int i = 0; i < 3; i++) {
                Dust glint = Dust.NewDustPerfect(wpos + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.DungeonSpirit, Main.rand.NextVector2Circular(0.5f, 0.5f), 150,
                    new Color(150, 230, 255), 0.9f);
                glint.noGravity = true;
            }
        }

        internal static void Reset() {
            chimeGate = 0;
            scanIn = 0;
            seaPrismTileId = -2;
            for (int i = 0; i < rungTable.Length; i++) {
                rungTable[i].Active = false;
            }
            for (int i = 0; i < Ripples.Length; i++) {
                Ripples[i].Active = false;
            }
        }
    }
}
