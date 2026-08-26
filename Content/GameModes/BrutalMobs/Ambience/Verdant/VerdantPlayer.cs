using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant
{
    /// <summary>
    /// 「湿身」：残酷模式丛林地表的湿气累积。雨中淋透或水中久待缓慢积攒湿气
    /// （衣角滴水粒子随湿度加密），攒满则短暂原版虚弱；靠近火把/篝火/壁炉可烘干（伴蒸汽）。
    /// 湿度从各端可见的同步状态（位置/入水/天气）确定性推得，逐端自算不发包；
    /// 虚弱只由所有者本机施加，走原版减益同步
    /// </summary>
    internal class VerdantPlayer : ModPlayer
    {
        /// <summary>湿气 0~1</summary>
        private float damp;
        /// <summary>低频环境采样计时（淋雨暴露/热源探测）</summary>
        private int scanTimer;
        private bool rainExposed;
        private bool nearHeat;
        /// <summary>虚弱重触发冷却（攒满一次后总有喘息）</summary>
        private int weakCooldown;

        /// <summary>雨中攒满约 15s</summary>
        private const float RainGainPerTick = 1f / 900f;
        /// <summary>水中攒满约 5s</summary>
        private const float WaterGainPerTick = 1f / 300f;
        /// <summary>丛林里自然阴干约 25s</summary>
        private const float AmbientDryPerTick = 1f / 1500f;
        /// <summary>热源烘干约 3.5s（伴蒸汽）</summary>
        private const float HeatDryPerTick = 1f / 210f;
        /// <summary>离开丛林后的干燥速度</summary>
        private const float OutsideDryPerTick = 1f / 450f;
        /// <summary>虚弱时长（帧），档位不参与（档位只调雾团）</summary>
        private const int WeakTicks = 420;
        private const int WeakRetriggerCooldown = 600;
        /// <summary>攒满触发后余留的湿气（要再湿一阵才会二触）</summary>
        private const float DampAfterTrigger = 0.35f;
        /// <summary>热源探测半径（瓦格）</summary>
        private const int HeatScanTiles = 6;

        public override void PostUpdate() {
            if (weakCooldown > 0) {
                weakCooldown--;
            }
            if (!GameModeSystem.BrutalActive) {
                damp = MathF.Max(damp - OutsideDryPerTick * 3f, 0f);
                return;
            }

            bool inVerdant = VerdantAmbience.InVerdant(Player);
            if (--scanTimer <= 0) {
                scanTimer = 12;
                rainExposed = inVerdant && Main.raining && ComputeRainExposed();
                nearHeat = ComputeNearHeat();
            }

            bool inWater = Player.wet && !Player.lavaWet && !Player.honeyWet && !Player.shimmerWet;

            //积攒：只在丛林地表；Boss 在场暂停积攒（减益机制随之停摆）
            if (inVerdant && !CWRWorld.HasBoss) {
                if (inWater) {
                    damp = MathF.Min(damp + WaterGainPerTick, 1f);
                }
                else if (rainExposed) {
                    damp = MathF.Min(damp + RainGainPerTick, 1f);
                }
            }

            //干燥：热源最快，其次自然阴干
            if (nearHeat) {
                damp = MathF.Max(damp - HeatDryPerTick, 0f);
            }
            else if (!inWater && !rainExposed) {
                damp = MathF.Max(damp - (inVerdant ? AmbientDryPerTick : OutsideDryPerTick), 0f);
            }

            //攒满：湿气回落到余量（各端确定性同走），虚弱只在所有者本机施加
            if (damp >= 1f && weakCooldown <= 0) {
                damp = DampAfterTrigger;
                weakCooldown = WeakRetriggerCooldown;
                bool suppressed = CWRWorld.HasBoss || VerdantAmbience.TownSanctuary(Player.Center);
                if (!suppressed && Player.whoAmI == Main.myPlayer) {
                    Player.AddBuff(BuffID.Weak, WeakTicks);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Player.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust drop = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                            DustID.Water, Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2f),
                            60, default, Main.rand.NextFloat(0.9f, 1.3f));
                        drop.noGravity = false;
                    }
                }
            }

            UpdateVisuals(inWater);
        }

        public override void UpdateDead() {
            damp = 0f;
            weakCooldown = 0;
        }

        /// <summary>衣角滴水与烘干蒸汽（纯本地演出，各端按各自湿气副本画）</summary>
        private void UpdateVisuals(bool inWater) {
            if (Main.dedServ || Main.gamePaused) {
                return;
            }
            //滴水：湿度越高越密；泡在水里时不画（本就浸没）
            if (damp > 0.3f && !Player.wet
                && Main.rand.NextFloat() < 0.02f + damp * 0.10f) {
                Vector2 pos = new(Player.position.X + Main.rand.NextFloat(Player.width),
                    Player.Center.Y + Main.rand.NextFloat(0f, Player.height * 0.5f));
                Dust drop = Dust.NewDustPerfect(pos, DustID.Water,
                    new Vector2(Player.velocity.X * 0.2f, Main.rand.NextFloat(0.8f, 1.6f)),
                    70, default, Main.rand.NextFloat(0.8f, 1.15f));
                drop.noGravity = false;
            }
            //烘干蒸汽：靠近热源且身上还有湿气
            if (nearHeat && damp > 0.08f && !inWater && Main.rand.NextBool(7)) {
                Dust steam = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                    DustID.Smoke, 0f, -0.8f, 200, default, Main.rand.NextFloat(0.55f, 0.85f));
                steam.noGravity = true;
                steam.velocity *= 0.4f;
            }
        }

        /// <summary>头顶 46 格内无实体块视为暴露在雨里（低频采样缓存）</summary>
        private bool ComputeRainExposed() {
            int tileX = (int)(Player.Center.X / 16f);
            int headY = (int)(Player.position.Y / 16f) - 1;
            for (int dy = 0; dy < 46; dy++) {
                int ty = headY - dy;
                if (!WorldGen.InWorld(tileX, ty, 24)) {
                    break;
                }
                if (WorldGen.SolidTile(tileX, ty)) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>周边 ±6 格内是否有火把/篝火/壁炉（不辨点燃态，熄灭篝火属边角情形）</summary>
        private bool ComputeNearHeat() {
            Point center = Player.Center.ToTileCoordinates();
            for (int dx = -HeatScanTiles; dx <= HeatScanTiles; dx++) {
                for (int dy = -HeatScanTiles; dy <= HeatScanTiles; dy++) {
                    int tx = center.X + dx;
                    int ty = center.Y + dy;
                    if (!WorldGen.InWorld(tx, ty, 24)) {
                        continue;
                    }
                    Tile tile = Main.tile[tx, ty];
                    if (!tile.HasTile) {
                        continue;
                    }
                    if (tile.TileType == TileID.Torches || tile.TileType == TileID.Campfire
                        || tile.TileType == TileID.Fireplace) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
