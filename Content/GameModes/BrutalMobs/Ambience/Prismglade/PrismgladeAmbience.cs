using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Prismglade.Projectiles;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Prismglade
{
    /// <summary>
    /// 残酷模式神圣之地环境氛围层（地表与地下同管，ZoneHallow 检测）。
    /// 三个具名特色：「虹尘」常态氛围（彩虹微尘+风铃泛音+夜间妖精光球群舞）、
    /// 「棱光审判」（夜间低频天降光柱，地下变奏为晶簇共鸣脉冲）、
    /// 「圣晶折射」（白天水晶簇刺目闪光，纯视觉）；另有低频甜头「独角尘迹」远景彩虹尘线。
    /// 权威端只做审判调度与弹幕生成，一切视觉与声音在客户端本地驱动；
    /// 档位只调光束频率，机制形状不随档位改变
    /// </summary>
    internal class PrismgladeAmbience : ModSystem
    {
        //==== 「棱光审判」权威调度 ====
        /// <summary>审判间隔（帧），档位只调频率不换机制</summary>
        private static readonly int[] JudgmentIntervalByTier = [2700, 2100, 1560];
        /// <summary>触发条件不满足时的复查提前量</summary>
        private const int JudgmentRetryFrames = 240;
        /// <summary>神圣之地原版敌怪接触伤害基准（妖精50/独角兽55/混沌精灵70 附近取中）</summary>
        private const int JudgmentContactBase = 55;
        /// <summary>审判经典档目标实收 = 基准接触伤害 × 此值（微量伤害，家族 DamageFrac 写法）</summary>
        private const float JudgmentDamageFrac = 0.42f;
        /// <summary>审判全局并发上限，超限跳过本次触发</summary>
        private const int JudgmentCap = 3;
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownCalmRange = 960f;
        /// <summary>向下寻找地表的最大瓦格数（超出视为目标悬空，放弃）</summary>
        private const int GroundSearchTiles = 12;
        /// <summary>地表变体要求头顶净空的瓦格数（有顶盖不降光柱）</summary>
        private const int SkyClearTiles = 50;
        /// <summary>地表光柱判定高</summary>
        private const float SurfaceBeamHeight = 210f;
        /// <summary>地下变体向上探顶的瓦格数</summary>
        private const int CeilingSearchTiles = 14;
        /// <summary>地下共鸣柱的最小净空，低于此不触发（太憋屈）</summary>
        private const float MinCaveBeamHeight = 96f;

        /// <summary>审判计时，权威端决策私产</summary>
        private static int judgmentTimer;
        /// <summary>上一位被点名的玩家。多于一人可选时排除上一位（永不连续点名同一玩家）</summary>
        private static int lastJudgedWho = -1;

        private struct JudgeCandidate
        {
            public int Who;
            public Vector2 BasePos;
            public int Variant;
            public float HeightPx;
        }

        /// <summary>候选缓存，冷路径重用避免反复分配</summary>
        private static readonly List<JudgeCandidate> candidates = new(8);

        //==== 客户端氛围（全部是本机演出量，不进任何逻辑判定） ====
        /// <summary>本地在场强度 0~1，进出群系缓入缓出，Boss 在场压到 0.45</summary>
        internal static float Presence { get; private set; }
        /// <summary>0 地表 / 1 地下的过渡量（地下水晶洞变奏用）</summary>
        private static float caveBlend;
        private static int moteIn;
        private static int fairyIn = 300;
        private static int flashIn = 200;
        private static int chimeIn = 300;
        private static int chimeEchoIn;
        private static float chimeEchoPitch;
        private static Vector2 chimeEchoPos;
        private static int streakIn = 1400;
        private static int streakLife;
        private static int streakLifeMax = 1;
        private static Vector2 streakPos;
        private static Vector2 streakVel;
        private static float streakHue;

        /// <summary>圣晶扫描缓存</summary>
        private static readonly List<Point> crystalSpots = new(40);

        public override void ClearWorld() {
            judgmentTimer = 0;
            lastJudgedWho = -1;
            candidates.Clear();
            Presence = 0f;
            caveBlend = 0f;
            moteIn = 0;
            fairyIn = 300;
            flashIn = 200;
            chimeIn = 300;
            chimeEchoIn = 0;
            streakIn = 1400;
            streakLife = 0;
            crystalSpots.Clear();
        }

        public override void PostUpdateEverything() {
            UpdateJudgmentAuthority();
            if (!Main.dedServ) {
                UpdateClientAmbience();
            }
        }

        //======================== 权威端：棱光审判调度 ========================

        private static void UpdateJudgmentAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;//决策与敌对弹幕生成只在权威端
            }
            int tier = GameModeSystem.EffectiveTier;
            if (!GameModeSystem.BrutalActive || tier <= 0) {
                judgmentTimer = 0;
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场：伤害机制暂停，计时冻结
            }

            int interval = JudgmentIntervalByTier[Math.Clamp(tier, 1, 3) - 1];
            if (++judgmentTimer < interval) {
                return;
            }
            judgmentTimer = interval - JudgmentRetryFrames;//本次不成则 240 帧后复查

            if (CountBeams() >= JudgmentCap) {
                return;
            }
            BuildCandidates();
            if (candidates.Count == 0) {
                return;
            }
            //永不连续点名同一玩家：多于一人可选时排除上一位；单人世界唯一人选不受此限
            if (candidates.Count > 1) {
                for (int i = candidates.Count - 1; i >= 0; i--) {
                    if (candidates[i].Who == lastJudgedWho) {
                        candidates.RemoveAt(i);
                    }
                }
            }

            JudgeCandidate pick = candidates[Main.rand.Next(candidates.Count)];
            //已预除原版敌对弹幕命中玩家的 ×2 结算系数（弹幕常量 = 经典档目标实收 ÷ 2，引擎自做难度缩放）：
            //damage = 55 × 0.42 ÷ 2 ≈ 11，经典实收 ≈ 22 / 专家 ≈ 44 / 大师 ≈ 66（防御前）
            int damage = (int)(JudgmentContactBase * JudgmentDamageFrac / 2f);
            //owner 传 Main.myPlayer：单人=本地玩家，服务端=255 恰好触发原生 SyncProjectile 广播（镜像 Wastes）
            Projectile.NewProjectile(new EntitySource_Misc("CWRPrismgladeJudgment"),
                pick.BasePos, Vector2.Zero, ModContent.ProjectileType<PrismgladeJudgmentProj>(),
                damage, 2f, Main.myPlayer, pick.Variant, 0f, pick.HeightPx);
            lastJudgedWho = pick.Who;
            judgmentTimer = 0;
        }

        /// <summary>逐玩家资格审查：群系、昼夜、城镇安宁、地面锚点、净空，全过才进候选</summary>
        private static void BuildCandidates() {
            candidates.Clear();
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.active || player.dead || player.ghost || !player.ZoneHallow) {
                    continue;
                }
                bool surface = player.ZoneOverworldHeight || player.ZoneSkyHeight;
                if (surface && Main.dayTime) {
                    continue;//地表审判限夜；地下晶簇共鸣不看天色
                }
                if (TownNpcNear(player.Center)) {
                    continue;//城镇安宁：60 格内有城镇 NPC 不触发伤害机制
                }
                if (!TryFindGround(player, out Vector2 basePos)) {
                    continue;//悬空放弃，圈必须落在实地上
                }
                if (surface) {
                    if (!SkyClearAbove(basePos)) {
                        continue;//有顶盖时天光降不下来
                    }
                    candidates.Add(new JudgeCandidate {
                        Who = i, BasePos = basePos, Variant = 0, HeightPx = SurfaceBeamHeight,
                    });
                }
                else {
                    float room = CeilingRoom(basePos);
                    if (room < MinCaveBeamHeight) {
                        continue;
                    }
                    candidates.Add(new JudgeCandidate {
                        Who = i, BasePos = basePos, Variant = 1, HeightPx = room,
                    });
                }
            }
        }

        /// <summary>统计在场审判数（只在冷却尽头调用，非每帧）</summary>
        private static int CountBeams() {
            int type = ModContent.ProjectileType<PrismgladeJudgmentProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && ++count >= JudgmentCap) {
                    break;
                }
            }
            return count;
        }

        private static bool TownNpcNear(Vector2 center) {
            float rangeSq = TownCalmRange * TownCalmRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.DistanceSQ(center) < rangeSq) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>从目标脚下向下找可站立地表，返回柱底锚点（镜像 Wastes 的地面锚定）</summary>
        private static bool TryFindGround(Player target, out Vector2 basePos) {
            basePos = default;
            Point feet = target.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, tileY)) {
                    basePos = new Vector2(feet.X * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>圈心上方是否直通天空（地表变体的资格线）</summary>
        private static bool SkyClearAbove(Vector2 basePos) {
            Point p = basePos.ToTileCoordinates();
            for (int dy = 2; dy <= SkyClearTiles; dy++) {
                int tileY = p.Y - dy;
                if (tileY < 10) {
                    break;//已到世界顶，视为通天
                }
                if (WorldGen.SolidTile(p.X, tileY)) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>地下变体的头顶净空（px），共鸣柱高按它定标</summary>
        private static float CeilingRoom(Vector2 basePos) {
            Point p = basePos.ToTileCoordinates();
            for (int dy = 2; dy <= CeilingSearchTiles; dy++) {
                int tileY = p.Y - dy;
                if (tileY < 10) {
                    break;
                }
                if (WorldGen.SolidTile(p.X, tileY)) {
                    return dy * 16f - 20f;
                }
            }
            return SurfaceBeamHeight;
        }

        //======================== 客户端：常态氛围 ========================

        private static void UpdateClientAmbience() {
            Player lp = Main.LocalPlayer;
            bool inHallow = !Main.gameMenu && lp.active && !lp.dead
                && GameModeSystem.BrutalActive && lp.ZoneHallow;
            //Boss 在场：纯视觉氛围保留但减弱
            float target = inHallow ? (CWRWorld.HasBoss ? 0.45f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.008f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);
            if (Presence <= 0.01f) {
                streakLife = 0;
                return;
            }
            bool surface = lp.ZoneOverworldHeight || lp.ZoneSkyHeight;
            caveBlend = MathHelper.Lerp(caveBlend, surface ? 0f : 1f, 0.08f);

            UpdateMotes();
            UpdateFairies(lp);
            UpdateCrystalFlashes();
            UpdateChimes(lp);
            UpdateStreak();
        }

        /// <summary>「虹尘」主体：屏内空气里的彩虹微尘（满在场约 8~10 粒/秒，地下略疏略亮）</summary>
        private static void UpdateMotes() {
            if (--moteIn > 0) {
                return;
            }
            moteIn = (int)((6f + 4f * caveBlend) / Math.Max(Presence, 0.2f)) + Main.rand.Next(3);
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(-40f, Main.screenWidth + 40f),
                Main.rand.NextFloat(-40f, Main.screenHeight + 40f));
            Point tp = pos.ToTileCoordinates();
            if (!WorldGen.InWorld(tp.X, tp.Y, 10) || WorldGen.SolidTile(tp.X, tp.Y)) {
                return;//只在空气中飘
            }
            PRTLoader.NewParticle<PRT_PrismgladeMote>(pos,
                new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.02f, 0.14f)),
                default, Main.rand.NextFloat(0.2f, 0.34f) * (1f + 0.25f * caveBlend));
        }

        /// <summary>「虹尘」夜戏：妖精光球群舞。一群 4~6 只共享出生点，各自绕圈缓飞</summary>
        private static void UpdateFairies(Player lp) {
            if (Main.dayTime || caveBlend > 0.5f) {
                return;//妖精只在夜间地表现身
            }
            if (--fairyIn > 0) {
                return;
            }
            fairyIn = Main.rand.Next(420, 700);
            Vector2 anchor = lp.Center + new Vector2(
                Main.rand.NextFloat(-650f, 650f), Main.rand.NextFloat(-320f, 40f));
            Point ap = anchor.ToTileCoordinates();
            if (!WorldGen.InWorld(ap.X, ap.Y, 10) || WorldGen.SolidTile(ap.X, ap.Y)
                || WorldGen.SolidTile(ap.X - 3, ap.Y) || WorldGen.SolidTile(ap.X + 3, ap.Y)) {
                fairyIn = 90;//落点憋屈，稍后另寻
                return;
            }
            int count = Main.rand.Next(4, 7);
            float radius = Main.rand.NextFloat(42f, 78f);
            float dir = Main.rand.NextBool() ? 1f : -1f;
            Vector2 drift = Main.rand.NextVector2Circular(0.28f, 0.2f);
            int life = Main.rand.Next(380, 500);
            int hueOff = Main.rand.Next(PrismgladeFX.FairyHues.Length);
            for (int i = 0; i < count; i++) {
                var fairy = PRTLoader.NewParticle<PRT_PrismgladeFairy>(anchor, Vector2.Zero,
                    default, Main.rand.NextFloat(0.4f, 0.58f));
                if (fairy == null) {
                    continue;
                }
                fairy.orbitCenter = anchor;
                fairy.orbitR = radius * Main.rand.NextFloat(0.85f, 1.15f);
                fairy.angle = MathHelper.TwoPi * i / count;
                fairy.angSpeed = dir * Main.rand.NextFloat(0.018f, 0.034f);
                fairy.hue = PrismgladeFX.FairyHues[(i + hueOff) % PrismgladeFX.FairyHues.Length];
                fairy.centerDrift = drift;
                fairy.Lifetime = life + Main.rand.Next(40);
            }
        }

        /// <summary>「圣晶折射」：白天扫描屏内水晶簇，随机一簇打出刺目星芒+棱镜色散粒子（纯视觉）</summary>
        private static void UpdateCrystalFlashes() {
            if (!Main.dayTime || Presence < 0.4f) {
                return;
            }
            if (--flashIn > 0) {
                return;
            }
            flashIn = Main.rand.Next(210, 380);

            crystalSpots.Clear();
            int x0 = Math.Clamp((int)(Main.screenPosition.X / 16f) - 1, 10, Main.maxTilesX - 10);
            int x1 = Math.Clamp(x0 + Main.screenWidth / 16 + 2, 10, Main.maxTilesX - 10);
            int y0 = Math.Clamp((int)(Main.screenPosition.Y / 16f) - 1, 10, Main.maxTilesY - 10);
            int y1 = Math.Clamp(y0 + Main.screenHeight / 16 + 2, 10, Main.maxTilesY - 10);
            for (int x = x0; x < x1 && crystalSpots.Count < 40; x += 2) {
                for (int y = y0; y < y1; y++) {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.Crystals) {
                        crystalSpots.Add(new Point(x, y));
                        if (crystalSpots.Count >= 40) {
                            break;
                        }
                    }
                }
            }
            if (crystalSpots.Count == 0) {
                return;
            }

            Point spot = crystalSpots[Main.rand.Next(crystalSpots.Count)];
            Vector2 world = new(spot.X * 16f + 8f, spot.Y * 16f + 8f);
            var flash = PRTLoader.NewParticle<PRT_PrismgladeFlash>(world, Vector2.Zero,
                default, Main.rand.NextFloat(1.0f, 1.5f));
            if (flash != null) {
                flash.hue = Main.rand.NextFloat();
            }
            //棱镜色散：固定色相扇（红→紫依次排开）向上散出
            for (int k = 0; k < 4; k++) {
                var mote = PRTLoader.NewParticle<PRT_PrismgladeMote>(world,
                    new Vector2((k - 1.5f) * 0.7f, -Main.rand.NextFloat(0.8f, 1.8f)),
                    default, Main.rand.NextFloat(0.15f, 0.22f));
                if (mote != null) {
                    mote.hue = k / 4f;
                    mote.Lifetime = Main.rand.Next(30, 46);
                }
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.22f, Pitch = 0.65f, MaxInstances = 3 }, world);
        }

        /// <summary>「虹尘」听觉层：低频风铃/水晶泛音点缀，四成概率带一记高两度的回声次音</summary>
        private static void UpdateChimes(Player lp) {
            if (chimeEchoIn > 0 && --chimeEchoIn == 0) {
                PlayChimeNote(chimeEchoPos, chimeEchoPitch, 0.7f);
            }
            if (--chimeIn > 0) {
                return;
            }
            chimeIn = Main.rand.Next(430, 920);
            if (Presence < 0.5f) {
                return;//淡入未满不敲铃
            }
            float note = Main.rand.Next(5) * 0.2f;//五声位
            Vector2 pos = lp.Center + Main.rand.NextVector2CircularEdge(220f, 220f) * Main.rand.NextFloat(0.6f, 1.6f);
            PlayChimeNote(pos, note, 1f);
            if (Main.rand.NextFloat() < 0.4f) {
                chimeEchoIn = Main.rand.Next(9, 16);
                chimeEchoPitch = Math.Min(note + 0.2f, 1f);
                chimeEchoPos = pos;
            }
        }

        private static void PlayChimeNote(Vector2 pos, float note, float mult) {
            float vol = (0.2f + 0.1f * note) * mult * Presence;
            if (caveBlend > 0.5f) {
                //地下：水晶泛音，更低更润
                SoundEngine.PlaySound(SoundID.Item26 with { Volume = vol, Pitch = -0.3f + note * 0.5f, MaxInstances = 3 }, pos);
            }
            else {
                //地表：清脆风铃
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = vol * 0.9f, Pitch = 0.4f + note * 0.5f, MaxInstances = 3 }, pos);
            }
        }

        /// <summary>「独角尘迹」（氛围甜头）：低频一道彩虹尘线掠过远景上空，色相沿线渐变</summary>
        private static void UpdateStreak() {
            if (streakLife > 0) {
                streakLife--;
                streakPos += streakVel;
                if (streakLife % 2 == 0) {
                    //隔帧撒尘：事件期约 30 粒/秒，事件级预算不进常态
                    var mote = PRTLoader.NewParticle<PRT_PrismgladeMote>(streakPos,
                        streakVel * 0.06f, default, Main.rand.NextFloat(0.16f, 0.24f));
                    if (mote != null) {
                        mote.hue = streakHue + (1f - streakLife / (float)streakLifeMax) * 0.5f;
                        mote.stretch = 3.4f;
                        mote.Lifetime = Main.rand.Next(26, 40);
                    }
                }
                return;
            }
            if (caveBlend > 0.5f) {
                return;//远景尘线只属于地表天际
            }
            if (--streakIn > 0) {
                return;
            }
            streakIn = Main.rand.Next(1500, 2700);
            if (Presence < 0.5f) {
                return;
            }
            bool leftToRight = Main.rand.NextBool();
            float y = Main.screenPosition.Y + Main.rand.NextFloat(60f, 260f);
            streakPos = new Vector2(leftToRight
                ? Main.screenPosition.X - 40f
                : Main.screenPosition.X + Main.screenWidth + 40f, y);
            streakVel = new Vector2((leftToRight ? 1f : -1f) * Main.rand.NextFloat(10f, 14f),
                Main.rand.NextFloat(-1.2f, 1.2f));
            streakLife = streakLifeMax = (int)((Main.screenWidth + 120f) / Math.Abs(streakVel.X));
            streakHue = Main.rand.NextFloat();
        }
    }
}
