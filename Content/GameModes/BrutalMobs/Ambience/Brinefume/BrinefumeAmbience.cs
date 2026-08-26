using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Brinefume.Projectiles;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Brinefume
{
    /// <summary>
    /// 残酷模式硫磺海环境氛围「腐澜雾」总控（灾厄群系，CWRRef.Has 守门，无灾厄时整系统静默）。
    /// 客户端：黄绿酸雾+水面酸沫粒子、闷雷与气泡底噪双循环、「渊鸣」深海远响、轻度压色；
    /// 权威端：按玩家调度「酸沸区」「毒霾潮」两类环境机制实体。
    /// 灾厄酸雨事件活跃时让位（密度减半、暂停新机制）；Boss 在场时纯视觉减弱、伤害机制停摆
    /// </summary>
    internal class BrinefumeAmbience : ModSystem
    {
        /// <summary>本地玩家的群系在场包络 0~1（进出硫磺海缓升缓降，不硬切）</summary>
        internal static float Presence { get; private set; }

        /// <summary>实际演出密度 = Presence × 让位系数（酸雨/Boss），粒子与音量统一读这个</summary>
        internal static float EffectDensity { get; private set; }

        //==== 色板（酸雾黄绿系）====
        /// <summary>酸雾体色（暗黄绿）</summary>
        internal static readonly Color MistDeep = new(146, 158, 82);
        /// <summary>酸沫亮色（灰黄泛白）</summary>
        internal static readonly Color FoamPale = new(206, 214, 150);
        /// <summary>酸性辉光（加色敷料，绘制时 A 置 0）</summary>
        internal static readonly Color AcidGlow = new(168, 196, 74);
        /// <summary>浑浊水体（暗橄榄）</summary>
        internal static readonly Color WaterMurk = new(64, 72, 30);

        //==== 「酸沸区」调度（档位只调频率，机制形状不变）====
        private static readonly int[] BoilCooldownByTier = [840, 640, 480];
        /// <summary>酸沸区全局并发上限</summary>
        private const int BoilCap = 3;
        /// <summary>
        /// 酸沸区接触伤害：锚定硫磺海代表敌怪接触伤（约 30~50）×0.4 ≈ 15，
        /// 落在合同带 0.4~0.7 下沿；受击无敌帧节流下表现为多跳微伤（恒定不随档位）
        /// </summary>
        private const int BoilDamage = 15;

        //==== 「毒霾潮」调度（频率恒定：档位只动沸区频率与减益等级）====
        private const int HazeCooldownBase = 2700;
        /// <summary>毒霾潮全局并发上限</summary>
        private const int HazeCap = 2;

        //==== 通用 ====
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 30;
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownSafeRadius = 960f;

        //环境声循环槽（镜像 GhostRainAmbience/OldNetAmbience 的槽位管理）
        private static SlotId gurgleSlot;
        private static SlotId fizzSlot;
        private static readonly SoundStyle GurgleStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle FizzStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //一次性远响计时（本机演出量，非逐玩家游戏状态）
        private static int thunderIn = 700;
        private static int moanIn = 3600;
        private static int moanEchoIn;
        private static Vector2 moanPos;
        /// <summary>最近的毒霾/沸区逼近度 0~1，喂给嘶鸣循环当"脚步声"</summary>
        private static float hazardSwell;

        /// <summary>整系统总闸：灾厄在场且残酷模式开启</summary>
        internal static bool SystemEnabled => CWRRef.Has && GameModeSystem.BrutalActive;

        /// <summary>玩家在硫磺海（含总闸判定）</summary>
        internal static bool InSulphur(Player player) => SystemEnabled && player.GetPlayerZoneSulphur();

        public override void PostUpdateEverything() {
            if (VaultUtils.isServer || VaultUtils.isSinglePlayer) {
                UpdateHazardScheduler();
            }
            if (Main.dedServ) {
                return;
            }
            UpdateClientAmbience();
        }

        public override void ClearWorld() {
            Presence = 0f;
            EffectDensity = 0f;
            hazardSwell = 0f;
            thunderIn = 700;
            moanIn = 3600;
            moanEchoIn = 0;
            if (!Main.dedServ) {
                BrinefumeAmbientRender.ClearBubbles();
            }
        }

        //轻度压色：硫磺黄绿灰调，强度克制，不与灾厄自身的硫磺海观感打架
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float veil = EffectDensity;
            if (veil <= 0.003f) {
                return;
            }
            Color sulTile = new(128, 128, 88);
            Color sulBg = new(104, 110, 66);
            tileColor = Color.Lerp(tileColor, sulTile, veil * 0.16f);
            backgroundColor = Color.Lerp(backgroundColor, sulBg, veil * 0.22f);
        }

        //==================== 客户端氛围 ====================

        private static void UpdateClientAmbience() {
            Player localPlayer = Main.LocalPlayer;
            float target = !Main.gameMenu && localPlayer.active && !localPlayer.dead
                && InSulphur(localPlayer) ? 1f : 0f;
            Presence = Math.Abs(target - Presence) < 0.008f
                ? target : MathHelper.Lerp(Presence, target, 0.05f);

            //让位系数：灾厄酸雨活跃时减密让位，Boss 在场时纯视觉减弱
            float yield = 1f;
            if (CWRRef.GetAcidRainEventIsOngoing()) {
                yield *= 0.45f;
            }
            if (CWRWorld.HasBoss) {
                yield *= 0.55f;
            }
            float densityTarget = Presence * yield;
            EffectDensity = Math.Abs(densityTarget - EffectDensity) < 0.008f
                ? densityTarget : MathHelper.Lerp(EffectDensity, densityTarget, 0.04f);

            if (Presence <= 0.004f) {
                moanEchoIn = 0;
                return;
            }

            UpdateHazardSwell(localPlayer);
            UpdateLoops();
            UpdateOneShots(localPlayer);
            UpdateAmbientDust(localPlayer);
        }

        //嘶鸣随最近的毒霾/沸区逼近而增强（不给每个实体挂音源，统一走 fizz 循环）
        private static void UpdateHazardSwell(Player localPlayer) {
            float swell = 0f;
            int hazeType = ModContent.ProjectileType<BrinefumeHazeTideProj>();
            int boilType = ModContent.ProjectileType<BrinefumeBoilZoneProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == hazeType) {
                    float dist = localPlayer.Distance(proj.Center);
                    swell = Math.Max(swell, 1f - MathHelper.Clamp((dist - 240f) / 760f, 0f, 1f));
                }
                else if (proj.type == boilType && proj.hostile) {
                    float dist = localPlayer.Distance(proj.Center);
                    swell = Math.Max(swell, 0.8f * (1f - MathHelper.Clamp((dist - 180f) / 620f, 0f, 1f)));
                }
            }
            hazardSwell = MathHelper.Lerp(hazardSwell, swell, 0.08f);
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(gurgleSlot, out _)) {
                gurgleSlot = SoundEngine.PlaySound(GurgleStyle, null, UpdateGurgle);
            }
            if (!SoundEngine.TryGetActiveSound(fizzSlot, out _)) {
                fizzSlot = SoundEngine.PlaySound(FizzStyle, null, UpdateFizz);
            }
        }

        //气泡底噪：低频咕噜声随缓慢双正弦脉动，像海底在换气
        private static bool UpdateGurgle(ActiveSound sound) {
            if (Presence <= 0.004f || Main.gameMenu) {
                return false;
            }
            float t = (float)Main.timeForVisualEffects * 0.016f;
            float pulse = 0.78f + 0.22f * MathF.Sin(t * 0.9f + MathF.Sin(t * 0.37f) * 1.7f);
            sound.Volume = 0.34f * EffectDensity * pulse;
            sound.Pitch = -0.82f;
            sound.Position = null;
            return true;
        }

        //酸海嘶鸣：底噪常低，毒霾/沸区逼近时嘶声上量（这就是它们的"脚步声"）
        private static bool UpdateFizz(ActiveSound sound) {
            if (Presence <= 0.004f || Main.gameMenu) {
                return false;
            }
            sound.Volume = (0.08f + 0.26f * hazardSwell) * EffectDensity;
            sound.Pitch = 0.32f - 0.14f * hazardSwell;
            sound.Position = null;
            return true;
        }

        //闷雷远响（酸雨传统的余韵）+「渊鸣」深海哀鸣（含一次回响，暗示硫磺海深处的存在）
        private static void UpdateOneShots(Player localPlayer) {
            if (--thunderIn <= 0) {
                thunderIn = 900 + Main.rand.Next(1500);
                if (Presence > 0.4f) {
                    Vector2 pos = localPlayer.Center + new Vector2(
                        Main.rand.NextFloat(-1600f, 1600f), -Main.rand.NextFloat(300f, 700f));
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Volume = (0.26f + Main.rand.NextFloat(0.18f)) * EffectDensity,
                        Pitch = -0.85f + Main.rand.NextFloat(0.3f),
                        MaxInstances = 2,
                    }, pos);
                }
            }

            if (--moanIn <= 0) {
                moanIn = 2700 + Main.rand.Next(2700);
                if (Presence > 0.5f) {
                    moanPos = localPlayer.Center + new Vector2(
                        Main.rand.NextFloat(500f, 1100f) * (Main.rand.NextBool() ? 1f : -1f),
                        700f + Main.rand.NextFloat(500f));
                    SoundEngine.PlaySound(SoundID.Roar with {
                        Volume = 0.20f * EffectDensity, Pitch = -0.92f, MaxInstances = 1,
                    }, moanPos);
                    moanEchoIn = 42;
                }
            }
            if (moanEchoIn > 0 && --moanEchoIn == 0 && Presence > 0.3f) {
                SoundEngine.PlaySound(SoundID.Roar with {
                    Volume = 0.11f * EffectDensity, Pitch = -0.98f, MaxInstances = 1,
                }, moanPos + new Vector2(Main.rand.NextFloat(-220f, 220f), 120f));
            }
        }

        //常态酸雾+水面酸沫（满密度合计约 25 粒/秒；酸雨/Boss 让位时随 EffectDensity 自动减量）
        private static void UpdateAmbientDust(Player localPlayer) {
            float density = EffectDensity;
            if (density <= 0.03f) {
                return;
            }
            //黄绿酸雾：贴着风向缓漂的悬浮尘（只在空气里出生）
            if (Main.rand.NextFloat() < 0.26f * density) {
                Vector2 pos = localPlayer.Center + new Vector2(
                    Main.rand.NextFloat(-900f, 900f), Main.rand.NextFloat(-460f, 340f));
                Point pt = pos.ToTileCoordinates();
                if (WorldGen.InWorld(pt.X, pt.Y, 40) && !WorldGen.SolidTile(pt.X, pt.Y)
                    && Framing.GetTileSafely(pt.X, pt.Y).LiquidAmount == 0) {
                    Dust mist = Dust.NewDustPerfect(pos, DustID.TintableDust, new Vector2(
                        Main.windSpeedCurrent * 0.8f + Main.rand.NextFloat(-0.12f, 0.12f),
                        Main.rand.NextFloat(-0.06f, 0.02f)),
                        216, MistDeep, Main.rand.NextFloat(1.0f, 1.7f));
                    mist.noGravity = true;
                    mist.noLight = true;
                }
            }
            //水面酸沫：沿浪线冒起的灰黄泡沫
            if (Main.rand.NextFloat() < 0.16f * density) {
                int tileX = (int)(localPlayer.Center.X / 16f) + Main.rand.Next(-46, 47);
                if (TryFindWaterSurface(new Point(tileX, (int)(localPlayer.Center.Y / 16f) - 22), 46,
                    out Vector2 surface)) {
                    Dust foam = Dust.NewDustPerfect(surface + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                        DustID.TintableDust, new Vector2(
                            Main.windSpeedCurrent * 1.4f + Main.rand.NextFloat(-0.2f, 0.2f),
                            -Main.rand.NextFloat(0.05f, 0.25f)),
                        170, FoamPale, Main.rand.NextFloat(0.7f, 1.1f));
                    foam.noGravity = true;
                }
            }
        }

        //==================== 权威端调度 ====================

        private static void UpdateHazardScheduler() {
            if (!SystemEnabled) {
                return;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.GetPlayerZoneSulphur()) {
                    continue;
                }
                BrinefumePlayer brine = player.GetModPlayer<BrinefumePlayer>();
                if (brine.BoilCooldown > 0) {
                    brine.BoilCooldown--;
                }
                else {
                    TryStartBoil(player, brine);
                }
                if (brine.HazeCooldown > 0) {
                    brine.HazeCooldown--;
                }
                else {
                    TryStartHaze(player, brine);
                }
            }
        }

        /// <summary>伤害/减益机制的统一放行闸：Boss 在场暂停、灾厄酸雨让位、城镇安宁</summary>
        private static bool HazardAllowedAt(Vector2 pos) {
            if (CWRWorld.HasBoss || CWRRef.GetAcidRainEventIsOngoing()) {
                return false;
            }
            return !TownNear(pos);
        }

        private static bool TownNear(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownSafeRadius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（只在冷却尽头调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        private static bool AnyOfTypeNear(int projType, Vector2 pos, float range) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && proj.Distance(pos) < range) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>「酸沸区」：在目标附近的开放水面锁点起一片沸腾水域（预告 ≥45 帧由实体保证）</summary>
        private static void TryStartBoil(Player target, BrinefumePlayer brine) {
            brine.BoilCooldown = RetryFrames;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0 || !HazardAllowedAt(target.Center)) {
                return;
            }
            int type = ModContent.ProjectileType<BrinefumeBoilZoneProj>();
            if (CountActive(type) >= BoilCap) {
                return;
            }
            for (int attempt = 0; attempt < 6; attempt++) {
                float offset = Main.rand.NextFloat(140f, 640f) * (Main.rand.NextBool() ? 1f : -1f);
                int tileX = (int)((target.Center.X + offset) / 16f);
                if (!TryFindWaterSurface(new Point(tileX, (int)(target.Center.Y / 16f) - 28), 66,
                    out Vector2 surface)) {
                    continue;
                }
                if (!WaterDeepEnough(tileX, (int)(surface.Y / 16f))) {
                    continue;
                }
                if (AnyOfTypeNear(type, surface, 320f)) {
                    continue;//不与现存沸区堆叠，区位可绕
                }
                Projectile.NewProjectile(target.GetSource_Misc("CWR_BrinefumeBoil"), surface, Vector2.Zero,
                    type, BoilDamage, 1f, Main.myPlayer);
                //档位只调出现频率与减益等级，机制形状不变
                brine.BoilCooldown = BoilCooldownByTier[tier - 1] + Main.rand.Next(-90, 121);
                return;
            }
        }

        /// <summary>「毒霾潮」：在上风处生成随风缓移的雾墙，漂过目标所在水域（频率不随档位）</summary>
        private static void TryStartHaze(Player target, BrinefumePlayer brine) {
            brine.HazeCooldown = RetryFrames * 4;
            if (GameModeSystem.EffectiveTier <= 0 || !HazardAllowedAt(target.Center)) {
                return;
            }
            int type = ModContent.ProjectileType<BrinefumeHazeTideProj>();
            if (CountActive(type) >= HazeCap || AnyOfTypeNear(type, target.Center, 1500f)) {
                return;
            }
            float wind = Main.windSpeedCurrent;
            int dir = wind >= 0.05f ? 1 : wind <= -0.05f ? -1 : Main.rand.NextBool() ? 1 : -1;
            //逆风上游生成，让它缓缓漂过玩家所在水域；找得到水面就贴水面锚定
            Vector2 spawn = target.Center - new Vector2(dir * 1150f, 80f);
            int tileX = (int)(spawn.X / 16f);
            if (TryFindWaterSurface(new Point(tileX, (int)(target.Center.Y / 16f) - 32), 70,
                out Vector2 surface)) {
                spawn = surface + new Vector2(0f, -BrinefumeHazeTideProj.AnchorLift);
            }
            Projectile.NewProjectile(target.GetSource_Misc("CWR_BrinefumeHaze"), spawn, Vector2.Zero,
                type, 0, 0f, Main.myPlayer, dir);
            brine.HazeCooldown = HazeCooldownBase + Main.rand.Next(900);
        }

        /// <summary>沸腾要有水体厚度：水面往下三格都是足量水才配沸腾</summary>
        private static bool WaterDeepEnough(int tileX, int surfaceRow) {
            for (int dy = 1; dy <= 3; dy++) {
                int y = surfaceRow + dy;
                if (!WorldGen.InWorld(tileX, y, 40) || WorldGen.SolidTile(tileX, y)) {
                    return false;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.LiquidAmount < 200 || tile.LiquidType != LiquidID.Water) {
                    return false;
                }
            }
            return true;
        }

        //==================== 水面探测（共用工具） ====================

        /// <summary>
        /// 找指定列的开放水面：起点在水中则向上回溯，否则向下扫描；
        /// 先碰到实心方块视为此列无开放水面。返回水面像素点（列中心）
        /// </summary>
        internal static bool TryFindWaterSurface(Point start, int maxDown, out Vector2 surface) {
            surface = default;
            int x = start.X;
            if (!WorldGen.InWorld(x, start.Y, 40)) {
                return false;
            }
            if (IsWater(x, start.Y)) {
                int y = start.Y;
                for (int up = 0; up < 90; up++) {
                    if (!WorldGen.InWorld(x, y - 1, 40) || !IsWater(x, y - 1)) {
                        break;
                    }
                    y--;
                }
                if (WorldGen.SolidTile(x, y - 1)) {
                    return false;//顶被封死，不算开放水面
                }
                surface = SurfacePixel(x, y);
                return true;
            }
            for (int dy = 0; dy < maxDown; dy++) {
                int y = start.Y + dy;
                if (!WorldGen.InWorld(x, y, 40) || WorldGen.SolidTile(x, y)) {
                    return false;
                }
                if (IsWater(x, y)) {
                    surface = SurfacePixel(x, y);
                    return true;
                }
            }
            return false;
        }

        private static bool IsWater(int x, int y) {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
        }

        private static Vector2 SurfacePixel(int x, int y) {
            Tile tile = Framing.GetTileSafely(x, y);
            return new Vector2(x * 16f + 8f, y * 16f + 16f - tile.LiquidAmount / 255f * 16f);
        }
    }
}
