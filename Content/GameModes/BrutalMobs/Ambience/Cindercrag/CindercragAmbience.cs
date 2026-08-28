using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag.Projectiles;
using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag
{
    /// <summary>
    /// 残酷模式硫火之崖环境层中枢。
    /// 「烬羽」：硫火烬羽上飘 + 低频女妖远嚎与岩层呻吟底噪（客户端，崖壁红光在 <see cref="CindercragGlowRender"/>）；
    /// 「崖口喷焰」「恸嚎波」：权威端逐玩家冷却调度，机制实体走同步弹幕，客户端只看实体演出。
    /// 检测走 CWRRef 反射扩展（上游成员名 ZoneCalamity 即硫火之崖），全部逻辑先以 CWRRef.Has 守门；
    /// 崖壁喷口原型归本槽位独有：源头在崖壁裂口，与地狱岩浆池液面熔泡严格分野
    /// </summary>
    internal class CindercragAmbience : ModSystem
    {
        //==== 档位与公平性 ====
        /// <summary>喷焰逐玩家冷却，档位只调频率不换机制形状（1 残酷 / 2 修罗 / 3 毁灭）</summary>
        private static readonly int[] VentCooldownByTier = [640, 520, 410];
        /// <summary>喷焰全局并发上限</summary>
        private const int VentCap = 4;
        /// <summary>恸嚎全局并发上限（纯声压演出，频率不吃档位）</summary>
        private const int WailCap = 2;
        private const int WailCooldownMin = 1500;
        private const int WailCooldownVar = 800;
        /// <summary>入崖热身帧：不许一进门就挨打</summary>
        private const int WarmupMin = 300;
        private const int WarmupVar = 300;
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>城镇安宁半径（约 60 格）：附近有存活城镇 NPC 时伤害/减益机制不触发</summary>
        private const float TownSafeRange = 960f;

        /// <summary>
        /// 喷焰基伤（微量）：崖内灾厄敌怪接触伤约 30~80，取 ~0.5 倍再压一档；
        /// 专家/大师对敌对弹幕的倍率由原版结算自动叠加，此处只给普通档基数
        /// </summary>
        private static int VentDamage => NPC.downedMoonlord ? 40 : Main.hardMode ? 26 : 14;

        //==== 客户端氛围（屏幕级私产，逐客户端一份） ====
        /// <summary>本地在场强度 0~1（进出崖约 1s 缓升缓降，离开有淡出不硬切）</summary>
        internal static float Presence { get; private set; }

        private static SlotId groanSlot;
        private static SlotId windSlot;
        /// <summary>岩层呻吟底噪：低频嗡鸣压到极低音高</summary>
        private static readonly SoundStyle GroanStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>崖风底噪：闷风裹着哀音</summary>
        private static readonly SoundStyle WindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        private static int distantWailIn;
        private static int rockGroanIn;
        private static float featherCarry;

        /// <summary>本槽位总门：残酷模式 + 灾厄在场 + 玩家在硫火之崖</summary>
        internal static bool ZoneOf(Player player)
            => GameModeSystem.BrutalActive && CWRRef.Has && player.GetPlayerZoneCalamity();

        /// <summary>统计某类弹幕活动实例数（只在冷却尽头调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 16) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>城镇安宁：玩家附近有存活城镇 NPC</summary>
        internal static bool TownSafe(Player player) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.life > 0 && player.Distance(npc.Center) < TownSafeRange) {
                    return true;
                }
            }
            return false;
        }

        public override void ClearWorld() {
            Presence = 0f;
            featherCarry = 0f;
            distantWailIn = 0;
            rockGroanIn = 0;
        }

        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                UpdateClientAmbience();
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                UpdateAuthority();
            }
        }

        //==================== 客户端：烬羽氛围 ====================

        private static void UpdateClientAmbience() {
            bool inCrag = !Main.gameMenu && Main.LocalPlayer.active && ZoneOf(Main.LocalPlayer);
            float target = inCrag ? 1f : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.045f);
            if (Presence < 0.004f) {
                Presence = 0f;
                if (!inCrag) {
                    return;
                }
            }

            if (inCrag) {
                UpdateAmbientLoops();
                UpdateOneShots();
                UpdateFeathers();
            }
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走，离崖随 Presence 淡出
        private static void UpdateAmbientLoops() {
            if (!SoundEngine.TryGetActiveSound(groanSlot, out _)) {
                groanSlot = SoundEngine.PlaySound(GroanStyle, null, UpdateGroanLoop);
            }
            if (!SoundEngine.TryGetActiveSound(windSlot, out _)) {
                windSlot = SoundEngine.PlaySound(WindStyle, null, UpdateWindLoop);
            }
        }

        /// <summary>岩层呻吟：极低音高的持续闷鸣，Boss 在场收敛让位战斗</summary>
        private static bool UpdateGroanLoop(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.004f) {
                return false;
            }
            sound.Volume = 0.22f * Presence * (CWRWorld.HasBoss ? 0.55f : 1f);
            sound.Pitch = -0.8f;
            sound.Position = null;
            return true;
        }

        /// <summary>崖风：闷风底噪，哀伤基调的铺底</summary>
        private static bool UpdateWindLoop(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.004f) {
                return false;
            }
            sound.Volume = 0.13f * Presence * (CWRWorld.HasBoss ? 0.55f : 1f);
            sound.Pitch = -0.35f;
            sound.Position = null;
            return true;
        }

        //远嚎与岩鸣的间发点缀：远处女妖嚎（低音高、远定位）+ 岩层错动呻吟
        private static void UpdateOneShots() {
            if (Presence < 0.3f) {
                return;
            }
            bool boss = CWRWorld.HasBoss;
            if (--distantWailIn <= 0) {
                distantWailIn = Main.rand.Next(480, 960) * (boss ? 2 : 1);
                Vector2 pos = Main.LocalPlayer.Center
                    + Main.rand.NextVector2CircularEdge(1f, 0.7f) * Main.rand.NextFloat(700f, 1150f);
                SoundEngine.PlaySound(SoundID.Zombie103 with {
                    Volume = 0.34f * (boss ? 0.6f : 1f),
                    Pitch = -0.45f + Main.rand.NextFloat(-0.12f, 0.12f),
                    MaxInstances = 2,
                }, pos);
            }
            if (--rockGroanIn <= 0) {
                rockGroanIn = Main.rand.Next(700, 1200);
                Vector2 pos = Main.LocalPlayer.Center
                    + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(300f, 700f);
                SoundEngine.PlaySound(SoundID.WormDig with {
                    Volume = 0.32f * (boss ? 0.6f : 1f),
                    Pitch = -0.85f,
                    MaxInstances = 2,
                }, pos);
            }
        }

        //烬羽密度预算：满在场约 12 片/秒，Boss 在场约减半；只出生在开阔空气格
        private static void UpdateFeathers() {
            featherCarry += 0.20f * Presence * (CWRWorld.HasBoss ? 0.55f : 1f);
            int spawn = (int)featherCarry;
            featherCarry -= spawn;
            for (int i = 0; i < spawn; i++) {
                Vector2 pos = Main.screenPosition + new Vector2(
                    Main.rand.NextFloat(-80f, Main.screenWidth + 80f),
                    Main.rand.NextFloat(-60f, Main.screenHeight + 100f));
                Point tile = pos.ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10)) {
                    continue;
                }
                Tile cell = Main.tile[tile.X, tile.Y];
                if ((cell.HasTile && Main.tileSolid[cell.TileType]) || cell.LiquidAmount > 0) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_CindercragFeather>(pos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.35f, 0.9f)),
                    default, Main.rand.NextFloat(0.7f, 1.15f));
            }
        }

        //==================== 权威端：喷焰与恸嚎调度 ====================

        private static void UpdateAuthority() {
            if (!GameModeSystem.BrutalActive || !CWRRef.Has) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }

            foreach (Player player in Main.ActivePlayers) {
                CindercragPlayer cp = player.GetModPlayer<CindercragPlayer>();
                if (player.dead || !player.GetPlayerZoneCalamity()) {
                    cp.WasInCrag = false;
                    continue;
                }
                if (!cp.WasInCrag) {
                    cp.WasInCrag = true;
                    cp.VentCooldown = Math.Max(cp.VentCooldown, WarmupMin + Main.rand.Next(WarmupVar));
                    cp.WailCooldown = Math.Max(cp.WailCooldown, WailCooldownMin / 2 + Main.rand.Next(WailCooldownVar));
                }
                if (cp.VentCooldown > 0) {
                    cp.VentCooldown--;
                }
                if (cp.WailCooldown > 0) {
                    cp.WailCooldown--;
                }
                if (cp.VentCooldown > 0 && cp.WailCooldown > 0) {
                    continue;//冷却未到不做任何扫描，热路径零负担
                }
                //Boss 在场 / 城镇安宁：伤害与减益机制暂停，只在冷却尽头查一次
                if (CWRWorld.HasBoss || TownSafe(player)) {
                    if (cp.VentCooldown <= 0) {
                        cp.VentCooldown = RetryFrames;
                    }
                    if (cp.WailCooldown <= 0) {
                        cp.WailCooldown = RetryFrames;
                    }
                    continue;
                }
                if (cp.VentCooldown <= 0) {
                    cp.VentCooldown = TryVent(player)
                        ? VentCooldownByTier[tier - 1] + Main.rand.Next(120)
                        : RetryFrames;
                }
                if (cp.WailCooldown <= 0) {
                    cp.WailCooldown = TryWail(player)
                        ? WailCooldownMin + Main.rand.Next(WailCooldownVar)
                        : RetryFrames;
                }
            }
        }

        /// <summary>
        /// 「崖口喷焰」落点采样：附近砖岩里挑一块带开阔面的实心瓦，喷口开在面中心、朝开阔方向喷。
        /// 喷向在生成帧锁死（预告即承诺），预告期即可读
        /// </summary>
        private static bool TryVent(Player target) {
            if (CountActive(ModContent.ProjectileType<CindercragVentProj>()) >= VentCap) {
                return false;
            }
            for (int attempt = 0; attempt < 14; attempt++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(200f, 560f);
                Point tp = (target.Center + ang.ToRotationVector2() * dist).ToTileCoordinates();
                if (!WorldGen.InWorld(tp.X, tp.Y, 10) || !WorldGen.SolidTile(tp.X, tp.Y)) {
                    continue;
                }
                if (!TryPickFace(tp, out int face)) {
                    continue;
                }
                Vector2 normal = CindercragVentProj.FaceNormal(face);
                Vector2 mouth = new Vector2(tp.X * 16f + 8f, tp.Y * 16f + 8f) + normal * 9f;
                float jet = RollJetAngle(face);
                float clearance = MeasureClearance(mouth, jet);
                if (clearance < 110f) {
                    continue;
                }
                //公平：喷口不贴任何玩家的脸
                if (AnyPlayerWithin(mouth, 130f)) {
                    continue;
                }
                float len = Math.Min(clearance - 12f, CindercragVentProj.JetMaxLen);
                Projectile.NewProjectile(new EntitySource_Misc("CWR_CindercragVent"), mouth, Vector2.Zero,
                    ModContent.ProjectileType<CindercragVentProj>(), VentDamage, 1.5f, Main.myPlayer,
                    jet, len, face);
                return true;
            }
            return false;
        }

        /// <summary>「恸嚎波」：在目标侧向远处起一道向内推进的声压波前，逼近本身就是预告</summary>
        private static bool TryWail(Player target) {
            if (CountActive(ModContent.ProjectileType<CindercragWailWaveProj>()) >= WailCap) {
                return false;
            }
            int side = Main.rand.NextBool() ? 1 : -1;
            Vector2 spawn = target.Center - new Vector2(side * CindercragWailWaveProj.ApproachDist, 30f);
            Projectile.NewProjectile(new EntitySource_Misc("CWR_CindercragWail"), spawn,
                new Vector2(side * CindercragWailWaveProj.WaveSpeed, 0f),
                ModContent.ProjectileType<CindercragWailWaveProj>(), 0, 0f, Main.myPlayer);
            return true;
        }

        /// <summary>裂口面挑选：水平面优先（崖壁裂口的身份），没有水平面才用上下面（斜喷）</summary>
        private static bool TryPickFace(Point tp, out int face) {
            Span<int> open = stackalloc int[4];
            int n = 0;
            if (AirAt(tp.X + 1, tp.Y)) {
                open[n++] = 0;
            }
            if (AirAt(tp.X - 1, tp.Y)) {
                open[n++] = 1;
            }
            if (AirAt(tp.X, tp.Y - 1)) {
                open[n++] = 2;
            }
            if (AirAt(tp.X, tp.Y + 1)) {
                open[n++] = 3;
            }
            face = -1;
            if (n == 0) {
                return false;
            }
            //先收水平候选
            Span<int> horiz = stackalloc int[2];
            int h = 0;
            for (int i = 0; i < n; i++) {
                if (open[i] <= 1) {
                    horiz[h++] = open[i];
                }
            }
            face = h > 0 ? horiz[Main.rand.Next(h)] : open[Main.rand.Next(n)];
            return true;
        }

        /// <summary>开阔格：无实心且非液体（火舌不往液体里喷）</summary>
        private static bool AirAt(int x, int y) {
            if (!WorldGen.InWorld(x, y, 10)) {
                return false;
            }
            Tile tile = Main.tile[x, y];
            return !(tile.HasTile && Main.tileSolid[tile.TileType]) && tile.LiquidAmount < 100;
        }

        /// <summary>喷向：水平面 ±28° 内摆，上下面强制斜向（离竖直 ≥28°），绝无正上正下</summary>
        private static float RollJetAngle(int face) {
            float tilt;
            switch (face) {
                case 0:
                    return Main.rand.NextFloat(-0.5f, 0.5f);
                case 1:
                    return MathHelper.Pi + Main.rand.NextFloat(-0.5f, 0.5f);
                case 2:
                    tilt = Main.rand.NextFloat(0.5f, 0.95f) * (Main.rand.NextBool() ? 1f : -1f);
                    return -MathHelper.PiOver2 + tilt;
                default:
                    tilt = Main.rand.NextFloat(0.5f, 0.95f) * (Main.rand.NextBool() ? 1f : -1f);
                    return MathHelper.PiOver2 + tilt;
            }
        }

        /// <summary>沿喷向量净空：撞实心即止，喷长永远短于净空（火舌不穿岩）</summary>
        private static float MeasureClearance(Vector2 mouth, float jetAngle) {
            Vector2 dir = jetAngle.ToRotationVector2();
            for (float d = 12f; d <= CindercragVentProj.JetMaxLen + 12f; d += 12f) {
                Point tp = (mouth + dir * d).ToTileCoordinates();
                if (!WorldGen.InWorld(tp.X, tp.Y, 10) || WorldGen.SolidTile(tp.X, tp.Y)) {
                    return d - 12f;
                }
            }
            return CindercragVentProj.JetMaxLen + 12f;
        }

        private static bool AnyPlayerWithin(Vector2 pos, float range) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Distance(pos) < range) {
                    return true;
                }
            }
            return false;
        }
    }
}
