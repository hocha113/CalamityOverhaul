using CalamityOverhaul.Content.Scenarios.Dungeonworld.Machines;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    /// <summary>
    /// 七层空气签名总线（WAVE2-ATMOSPHERE E-1）：随机探针制环境粒子 + 尘埃光锥槽 +
    /// 烛光呼吸 + L6 机器挂钩 + L3 书架扬尘。纯客户端表现层，零网络包、零 tile 写入，
    /// 各端探针随机独立（与雾同款口径）。驱动挂 PostUpdateEverything（镜像 FogSystem），
    /// 不进 Dungeonworld.Update 总线
    /// </summary>
    internal class DungeonworldAmbientFX : ModSystem
    {
        //==== Debug 静态口（TestItem 片段用）====
        /// <summary>主世界强制开启（验收用）</summary>
        internal static bool DebugForce;
        /// <summary>发射概率热调（0~3；硬帽 2 粒/tick 不受它影响，只能降不能破帽）</summary>
        internal static float RateMul = 1f;
        /// <summary>关闭尘埃光锥（含登记与绘制）</summary>
        internal static bool DisableShafts;

        //每 tick 探针数：窗口内均匀随机，纯读，亚微秒级/针
        private const int ProbesPerTick = 32;
        //光锥槽数量硬帽
        internal const int MaxShafts = 6;
        //光锥 TTL 与灯态复核周期
        private const int ShaftTtl = 90;
        private const int ShaftRecheck = 10;
        //光锥柱长硬帽（tile）
        private const int ShaftMaxLenTiles = 12;

        private static float presence;
        internal static float Presence => presence;

        //Boss 在场自动降密（镜像 FogSystem 的按名扫描；未接 AmbientQuiet 前的兜底）
        private static int bossScanTimer;
        private static int wraithType = -2;
        private static float bossMul = 1f;

        private static int machineType = -1;

        internal struct ShaftSlot
        {
            internal bool Active;
            internal Point LampTile;
            internal int LampType;
            internal Vector2 TopPx;      //柱顶（灯具下沿）
            internal float WidthPx;
            internal float LengthPx;
            internal int Ttl;
            internal int RecheckIn;
            internal float Bright;       //灯位光照 0~1
            internal float Phase;        //摆动/呼吸相位
        }

        internal static readonly ShaftSlot[] Shafts = new ShaftSlot[MaxShafts];

        //一次性音延迟队列（纸声三连等）
        private struct PendingCue
        {
            internal bool Active;
            internal SoundStyle Style;
            internal Vector2 Pos;
            internal int Delay;
        }

        private static readonly PendingCue[] pendingCues = new PendingCue[6];
        private static int dashCooldown;

        private static readonly Color CandleWarm = new(233, 185, 102);

        public override void OnWorldLoad() => HardReset();
        public override void OnWorldUnload() => HardReset();

        public override void Unload() {
            HardReset();
            DebugForce = false;
            RateMul = 1f;
            DisableShafts = false;
            wraithType = -2;
            machineType = -1;
        }

        private static void HardReset() {
            presence = 0f;
            bossMul = 1f;
            bossScanTimer = 0;
            dashCooldown = 0;
            AmbientQuiet.Clear();
            for (int i = 0; i < Shafts.Length; i++) {
                Shafts[i].Active = false;
            }
            for (int i = 0; i < pendingCues.Length; i++) {
                pendingCues[i].Active = false;
            }
        }

        private static bool Want => (Dungeonworld.Active || DebugForce) && !Main.gameMenu;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool want = Want;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, want ? 0.07f : 0.10f);
            if (!want && presence < 0.004f) {
                if (presence != 0f) {
                    HardReset();
                }
                return;
            }

            AmbientQuiet.Update();
            AmbientBudget.NewTick();
            UpdateBossMul();
            UpdateShafts();
            RunProbes();
            HookMachines();
            UpdateDashDust();
            FlushPendingCues();
        }

        //==================== 探针 ====================

        private static void RunProbes() {
            int left = (int)(Main.screenPosition.X / 16f) - 6;
            int top = (int)(Main.screenPosition.Y / 16f) - 6;
            int right = (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + 6;
            int bottom = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + 6;
            left = (int)MathHelper.Clamp(left, 1, Main.maxTilesX - 2);
            right = (int)MathHelper.Clamp(right, left + 1, Main.maxTilesX - 2);
            top = (int)MathHelper.Clamp(top, 1, Main.maxTilesY - 2);
            bottom = (int)MathHelper.Clamp(bottom, top + 1, Main.maxTilesY - 2);

            float mul = presence * MathHelper.Clamp(RateMul, 0f, 3f) * bossMul;

            for (int i = 0; i < ProbesPerTick; i++) {
                int x = Main.rand.Next(left, right);
                int y = Main.rand.Next(top, bottom);
                //灯具登记优先（灯是非实心家具，发射表不会消费它）
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && LampLit(tile)) {
                    TryRegisterShaft(x, y, tile.TileType);
                    continue;
                }
                AmbientEmitters.FireProbe(x, y, mul);
            }
        }

        /// <summary>灯具点亮判据（对齐 L3Lights 的帧语义，只读不写）</summary>
        private static bool LampLit(Tile tile) {
            return tile.TileType switch {
                //吊灯熄灭=全格 frameX+=54（54 宽一态，108 一循环）
                TileID.Chandeliers => tile.TileFrameX % 108 < 54,
                //灯笼熄灭=frameX=18
                TileID.HangingLanterns => tile.TileFrameX < 18,
                TileID.Candles => tile.TileFrameX < 18,
                TileID.Torches => true,
                _ => false
            };
        }

        //==================== 光锥槽 ====================

        private static void TryRegisterShaft(int x, int y, int tileType) {
            if (DisableShafts) {
                return;
            }
            Vector2 lampPx = new(x * 16f + 8f, y * 16f + 8f);
            if (AmbientQuiet.Evaluate(lampPx) < 0.5f) {
                return;
            }

            //去重：命中既有槽（吊灯 3x3 的任意格都算同一盏）就只续订
            for (int i = 0; i < Shafts.Length; i++) {
                if (!Shafts[i].Active) {
                    continue;
                }
                int dx = Math.Abs(Shafts[i].LampTile.X - x);
                int dy = Math.Abs(Shafts[i].LampTile.Y - y);
                if (dx <= 2 && dy <= 2) {
                    Shafts[i].Ttl = ShaftTtl;
                    return;
                }
            }

            for (int i = 0; i < Shafts.Length; i++) {
                if (Shafts[i].Active) {
                    continue;
                }
                Shafts[i] = new ShaftSlot {
                    Active = true,
                    LampTile = new Point(x, y),
                    LampType = tileType,
                    TopPx = lampPx + new Vector2(0f, 10f),
                    WidthPx = tileType == TileID.Chandeliers ? 46f
                        : tileType == TileID.HangingLanterns ? 22f : 18f,
                    LengthPx = RaycastShaftLength(x, y),
                    Ttl = ShaftTtl,
                    RecheckIn = ShaftRecheck,
                    Bright = AmbientPRTUtil.SafeBright(lampPx),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi)
                };
                return;
            }
        }

        private static float RaycastShaftLength(int x, int y) {
            int len = 0;
            for (int dy = 1; dy <= ShaftMaxLenTiles; dy++) {
                Tile t = Framing.GetTileSafely(x, y + dy);
                if (t.HasTile && Main.tileSolid[t.TileType]) {
                    break;
                }
                len++;
            }
            return MathF.Max(len, 1f) * 16f;
        }

        private static void UpdateShafts() {
            for (int i = 0; i < Shafts.Length; i++) {
                if (!Shafts[i].Active) {
                    continue;
                }
                if (DisableShafts) {
                    Shafts[i].Active = false;
                    continue;
                }
                Shafts[i].Ttl--;
                Shafts[i].RecheckIn--;

                Vector2 lampPx = new(Shafts[i].LampTile.X * 16f + 8f, Shafts[i].LampTile.Y * 16f + 8f);
                if (Shafts[i].RecheckIn <= 0) {
                    Shafts[i].RecheckIn = ShaftRecheck;
                    Tile t = Framing.GetTileSafely(Shafts[i].LampTile.X, Shafts[i].LampTile.Y);
                    //灭灯/拆灯/静默区 → 立即熄柱（验收：灭灯后 ≤2s 光柱消失，这里是 ≤10f）
                    if (!t.HasTile || t.TileType != Shafts[i].LampType || !LampLit(t)
                        || AmbientQuiet.Evaluate(lampPx) < 0.5f) {
                        Shafts[i].Active = false;
                        continue;
                    }
                    //屏内才续订；出窗后靠 TTL 自然过期
                    if (OnScreenPad(lampPx, 200f)) {
                        Shafts[i].Ttl = ShaftTtl;
                    }
                    Shafts[i].Bright = AmbientPRTUtil.SafeBright(lampPx);
                    Shafts[i].LengthPx = RaycastShaftLength(Shafts[i].LampTile.X, Shafts[i].LampTile.Y);
                }
                if (Shafts[i].Ttl <= 0) {
                    Shafts[i].Active = false;
                    continue;
                }

                //烛光呼吸：AddLight 只能加不能减，"摇曳"=微幅加光抖动（纯客户端幻觉，不改灯帧）
                float breath = 0.5f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13.8f + Shafts[i].Phase)
                    + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6.6f + Shafts[i].Phase * 1.7f);
                float k = 0.05f + 0.04f * breath;
                Lighting.AddLight(lampPx, CandleWarm.R / 255f * k, CandleWarm.G / 255f * k, CandleWarm.B / 255f * k);
            }
        }

        private static bool OnScreenPad(Vector2 worldPx, float pad) {
            return worldPx.X > Main.screenPosition.X - pad
                && worldPx.X < Main.screenPosition.X + Main.screenWidth + pad
                && worldPx.Y > Main.screenPosition.Y - pad
                && worldPx.Y < Main.screenPosition.Y + Main.screenHeight + pad;
        }

        /// <summary>点是否在某光锥柱体内（书尘柱内加密用）</summary>
        internal static bool IsInShaftLight(Vector2 worldPx) {
            for (int i = 0; i < Shafts.Length; i++) {
                if (!Shafts[i].Active) {
                    continue;
                }
                float halfW = Shafts[i].WidthPx * 0.6f;
                if (MathF.Abs(worldPx.X - Shafts[i].TopPx.X) <= halfW
                    && worldPx.Y >= Shafts[i].TopPx.Y
                    && worldPx.Y <= Shafts[i].TopPx.Y + Shafts[i].LengthPx) {
                    return true;
                }
            }
            return false;
        }

        //==================== Boss 在场降密 ====================

        private static void UpdateBossMul() {
            if (wraithType == -1) {
                return;
            }
            if (wraithType == -2) {
                if (!NPCs.DeepGaolWraithGate.Enabled) {
                    wraithType = -1;
                    return;
                }
                wraithType = ModContent.NPCType<NPCs.DeepGaolWraith>();
            }
            if (++bossScanTimer < 4) {
                return;
            }
            bossScanTimer = 0;
            bossMul = 1f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == wraithType) {
                    bossMul = 0.3f;
                    return;
                }
            }
        }

        //==================== L6 机器挂钩 ====================

        private static void HookMachines() {
            if (machineType == -1) {
                machineType = ModContent.ProjectileType<L6MachineStrike>();
            }
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != machineType) {
                    continue;
                }
                //相位由 timeLeft 反推（生成包同步过 timeLeft 初值，各端一致衰减）
                bool roller = p.ai[0] >= 0.5f;
                int total = roller ? 92 : 68;
                int wind = roller ? 34 : 30;
                int life = total - p.timeLeft;
                if (life > wind && life % 3 == 0) {
                    Vector2 head = roller ? p.Bottom + new Vector2(0f, -6f)
                        : p.Center + new Vector2(0f, p.height * 0.4f);
                    AmbientEmitters.MachineSparkAt(head);
                }
                if (p.timeLeft <= 1) {
                    AmbientEmitters.MachineSteamAt(p.Center);
                }
            }
        }

        //==================== L3 书架扬尘 ====================

        private static void UpdateDashDust() {
            if (dashCooldown > 0) {
                dashCooldown--;
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return;
            }
            if (player.velocity.Length() < 4.5f) {
                return;
            }
            int row = (int)(player.Center.Y / 16f);
            if (AmbientEmitters.BandIndexForRow(row) != 2) {
                return;
            }
            //贴近书架列才扬尘：±4 列 ×±3 行小窗扫描
            int px = (int)(player.Center.X / 16f);
            bool nearShelf = false;
            for (int x = px - 4; x <= px + 4 && !nearShelf; x++) {
                for (int y = row - 3; y <= row + 3; y++) {
                    Tile t = Framing.GetTileSafely(x, y);
                    if (t.HasTile && t.TileType == TileID.Bookcases) {
                        nearShelf = true;
                        break;
                    }
                }
            }
            if (!nearShelf) {
                return;
            }
            AmbientEmitters.DashPuffAt(player.Bottom + new Vector2(0f, -6f), player.velocity);
            //纸页窸窣：极轻 Drip 高音三连（实现期试听可换）
            SoundStyle paper = SoundID.Drip with { Volume = 0.12f, Pitch = 0.9f, MaxInstances = 3 };
            QueueCue(paper, player.Center, 0);
            QueueCue(paper, player.Center, 6);
            QueueCue(paper, player.Center, 12);
            dashCooldown = 45;
        }

        //==================== 延迟音队列 ====================

        private static void QueueCue(SoundStyle style, Vector2 pos, int delay) {
            for (int i = 0; i < pendingCues.Length; i++) {
                if (pendingCues[i].Active) {
                    continue;
                }
                pendingCues[i] = new PendingCue { Active = true, Style = style, Pos = pos, Delay = delay };
                return;
            }
        }

        private static void FlushPendingCues() {
            for (int i = 0; i < pendingCues.Length; i++) {
                if (!pendingCues[i].Active) {
                    continue;
                }
                if (--pendingCues[i].Delay > 0) {
                    continue;
                }
                SoundEngine.PlaySound(pendingCues[i].Style, pendingCues[i].Pos);
                pendingCues[i].Active = false;
            }
        }

        //==================== 调试 ====================

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine() {
            int shaftCount = 0;
            for (int i = 0; i < Shafts.Length; i++) {
                if (Shafts[i].Active) {
                    shaftCount++;
                }
            }
            int alive = 0;
            var inds = InnoVault.PRT.PRTLoader.PRT_InGame_World_Inds;
            if (inds != null) {
                foreach (var prt in inds) {
                    if (prt != null && prt.active && prt is PRT_DwMote or PRT_DwScrap or PRT_DwDrip
                        or PRT_DwRipple or PRT_DwGlint or PRT_DwMist or PRT_DwAsh or PRT_DwSpark) {
                        alive++;
                    }
                }
            }
            return $"[空气签名] presence{presence:F2} 帽[{AmbientBudget.Line}] 光锥{shaftCount}/{MaxShafts}"
                + $" 存活{alive} RateMul{RateMul:F2} Boss因子{bossMul:F1}";
        }
    }
}
