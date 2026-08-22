using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 物块征用：把命中格周围同种物块拆成最多八块护甲板绕身环绕，
    /// 每块替玩家挡下一次命中即碎，右键或到期朝光标依次射出。<br/>
    /// 「改了要还」的账本就是板块弹幕本身：射出与挡刀是正常消耗不退；
    /// 仍在环绕时夭折（施术者倒下 / 未发射先亡）由权威端放回原格或掉出物品。
    /// 世界卸载时仍在环绕的板块随弹幕表一起消失，物块随之放弃
    /// 窗口只有十秒且不写坏任何世界数据，属于安全放弃
    /// </summary>
    internal class TileConscript : QuickHackDef
    {
        //征用持续（帧，10 秒），板块的环绕自计时与此对齐
        internal const int ConscriptDuration = 60 * 10;
        //最大征用板块数
        internal const int MaxPlates = 8;
        //选块范围（以命中格为心的方形半径，5×5）
        private const int GatherRadius = 2;
        //默认镐力，无镐也能拆常见块（对齐 TileDetonate）
        private const int BasePickPower = 50;

        public override void SetDefaults() {
            UploadTime = 140;
            RamCost = 5;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => ConscriptDuration;

        #region 挖掘力与硬度（镜像 TileDetonate，其成员为 private 无法直接复用）

        private static void AccumulatePickFromArray(Item[] items, ref int max) {
            if (items == null) return;
            for (int i = 0; i < items.Length; i++) {
                Item it = items[i];
                if (it != null && !it.IsAir && it.pick > max) {
                    max = it.pick;
                }
            }
        }

        //背包最高镐力，与默认取大，含存钱罐
        private static int GetEffectivePickPower(Player player) {
            int max = BasePickPower;
            if (player == null) return max;
            AccumulatePickFromArray(player.inventory, ref max);
            AccumulatePickFromArray(player.bank?.item, ref max);
            AccumulatePickFromArray(player.bank2?.item, ref max);
            AccumulatePickFromArray(player.bank3?.item, ref max);
            AccumulatePickFromArray(player.bank4?.item, ref max);
            return max;
        }

        //模组读 MinPick，原版打表（复刻 Player.PickTile）
        internal static int GetTileMinPick(int type) {
            ModTile modTile = TileLoader.GetTile(type);
            if (modTile != null) {
                return modTile.MinPick;
            }
            if (type == TileID.Meteorite) return 50;
            if (type == TileID.Demonite || type == TileID.Crimtane) return 55;
            if (type == TileID.Ebonstone || type == TileID.Crimstone
                || type == TileID.Pearlstone || type == TileID.Hellstone) return 65;
            if (type == TileID.Cobalt || type == TileID.Palladium) return 100;
            if (type == TileID.Mythril || type == TileID.Orichalcum) return 110;
            if (type == TileID.Adamantite || type == TileID.Titanium) return 150;
            if (type == TileID.Chlorophyte) return 200;
            if (type == TileID.LihzahrdBrick) return 210;
            return 0;
        }

        /// <summary>
        /// 射出伤害的硬度表：泥石 30 / 黑曜石地狱石 90 / 硬模式矿 140；
        /// 灾厄矿在 CWRID 里没有物块表可查，按 MinPick 阶梯折算并保底 110
        /// </summary>
        internal static int PlateDamageFor(int type) {
            if (type == TileID.Obsidian || type == TileID.Hellstone) return 90;
            int minPick = GetTileMinPick(type);
            int tier = minPick >= 200 ? 180 : minPick >= 100 ? 140 : minPick >= 50 ? 60 : 30;
            return type >= TileID.Count ? Math.Max(110, tier) : tier;
        }

        #endregion

        #region 选块

        //只收平整实心地形块：家具/多格结构的帧数据拆走装不回，容器与祭坛类照旧禁碰
        private static bool IsConscriptableType(int type) {
            if (type < 0 || type >= TileLoader.TileCount) return false;
            if (!Main.tileSolid[type] || Main.tileSolidTop[type]) return false;
            if (Main.tileFrameImportant[type]) return false;
            if (type == TileID.LihzahrdBrick || type == TileID.LihzahrdAltar
                || type == TileID.DemonAltar || type == TileID.LunarMonolith) return false;
            //冷凝固化的薄冰有自己的账，征走会跟它的拆账互相踩
            if (type == TileID.BreakableIce) return false;
            if (Main.tileContainer[type]) return false;
            if (Main.tileDungeon[type] && !NPC.downedBoss3) return false;
            return true;
        }

        private static bool CanConscriptTile(int x, int y, int anchorType) {
            if (!HackTargets.InWorld(x, y)) return false;
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || tile.IsActuated || tile.TileType != anchorType) return false;
            return WorldGen.CanKillTile(x, y);
        }

        //锚点自己不拆（拆了效果立刻失锚，对齐 Cryostasis 的锚点规则），收周围一圈同种块
        private static void GatherCandidates(int anchorX, int anchorY, int anchorType,
            List<Point> result) {
            result.Clear();
            for (int dx = -GatherRadius; dx <= GatherRadius; dx++) {
                for (int dy = -GatherRadius; dy <= GatherRadius; dy++) {
                    if (dx == 0 && dy == 0) continue;
                    int tx = anchorX + dx;
                    int ty = anchorY + dy;
                    if (CanConscriptTile(tx, ty, anchorType)) {
                        result.Add(new Point(tx, ty));
                    }
                }
            }
            //近处优先，环上的板块序号也按这个顺序排
            result.Sort((a, b) =>
                (Math.Abs(a.X - anchorX) + Math.Abs(a.Y - anchorY))
                .CompareTo(Math.Abs(b.X - anchorX) + Math.Abs(b.Y - anchorY)));
        }

        private static int CountLivePlates(int ownerIndex) {
            int plateType = ModContent.ProjectileType<ConscriptPlateProj>();
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == ownerIndex
                    && proj.type == plateType) {
                    count++;
                }
            }
            return count;
        }

        #endregion

        public override bool CanApplyTo(IHackTarget target)
            => CanApplyToPlayer(target, Main.LocalPlayer);

        public override bool CanApplyTo(IHackTarget target, Player caster)
            => CanApplyToPlayer(target, caster);

        private static readonly List<Point> gatherBuffer = [];

        private static bool CanApplyToPlayer(IHackTarget target, Player caster) {
            if (caster == null || target is not TileScannable s || !s.IsValid) return false;
            int type = Main.tile[s.TileCoordX, s.TileCoordY].TileType;
            if (!IsConscriptableType(type)) return false;
            if (GetEffectivePickPower(caster) < GetTileMinPick(type)) return false;
            //环上还有板块时不重复征用，两圈叠一起挡刀账算不清
            if (CountLivePlates(caster.whoAmI) > 0) return false;
            GatherCandidates(s.TileCoordX, s.TileCoordY, type, gatherBuffer);
            return gatherBuffer.Count > 0;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            int anchorX = s.TileCoordX;
            int anchorY = s.TileCoordY;
            int anchorType = Main.tile[anchorX, anchorY].TileType;
            int spawned = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                GatherCandidates(anchorX, anchorY, anchorType, gatherBuffer);
                int projType = ModContent.ProjectileType<ConscriptPlateProj>();
                int damage = PlateDamageFor(anchorType);
                foreach (Point p in gatherBuffer) {
                    if (spawned >= MaxPlates) break;
                    //先记应掉何物再拆，拆完格子上就没数据了
                    int dropType = Main.tile[p.X, p.Y].GetTileDrop(p.X, p.Y);
                    WorldGen.KillTile(p.X, p.Y, fail: false, effectOnly: false, noItem: true);
                    if (Main.tile[p.X, p.Y].HasTile) continue;

                    int idx = Projectile.NewProjectile(
                        caster.GetSource_Misc("CWR_TileConscript"),
                        HackTargets.TileWorldCenter(p.X, p.Y), Vector2.Zero,
                        projType, damage, 2f, caster.whoAmI,
                        0f, anchorType, spawned);
                    if (idx < 0 || idx >= Main.maxProjectiles) continue;

                    if (Main.projectile[idx].ModProjectile is ConscriptPlateProj plate) {
                        //权威端私账：只有权威端要做「还块」结算，客户端副本不需要
                        plate.OriginX = p.X;
                        plate.OriginY = p.Y;
                        plate.DropItemType = dropType;
                    }
                    //服务器代玩家生成的弹幕不会自动同步，权威端补一发全量包
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, idx);
                    }
                    spawned++;
                }

                if (spawned > 0 && Main.netMode != NetmodeID.SinglePlayer) {
                    int span = GatherRadius * 2 + 1;
                    NetMessage.SendTileSquare(-1, anchorX - GatherRadius,
                        anchorY - GatherRadius, span, span);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                EmitConscriptVisual(anchorX, anchorY);
            }
            return spawned > 0;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is not TileScannable s) return;
            if (!HackTargets.InWorld(s.TileCoordX, s.TileCoordY)) return;
            EmitConscriptVisual(s.TileCoordX, s.TileCoordY);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server && target is TileScannable s) {
                EmitTickVisual(s.TileCoordX, s.TileCoordY, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (target is TileScannable s) {
                EmitTickVisual(s.TileCoordX, s.TileCoordY, elapsed);
            }
        }

        //板块的发射由拥有者端自计时驱动（服务端推不动玩家所属弹幕），OnRemove 只收表现
        public override void OnRemove(IHackTarget target) {
            if (Main.netMode != NetmodeID.Server && target is TileScannable s) {
                EmitReleaseVisual(s.TileCoordX, s.TileCoordY);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (target is TileScannable s) {
                EmitReleaseVisual(s.TileCoordX, s.TileCoordY);
            }
        }

        #region 表现

        private static void EmitConscriptVisual(int tileX, int tileY) {
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);
            for (int i = 0; i < 18; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(
                    GatherRadius * 16f, GatherRadius * 16f);
                Vector2 vel = (center - pos).SafeNormalize(Vector2.Zero)
                    * Main.rand.NextFloat(1.5f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, HackTheme.Accent, 0.9f)
                    ?.Configure(false, 26);
            }
            for (int i = 0; i < 6; i++) {
                var square = PRTLoader.NewParticle<PRT_CyberSquare>(
                    center + Main.rand.NextVector2Circular(24f, 24f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f), HackTheme.Accent, 1.1f);
                square?.Configure(HackTheme.AccentAlt, 24);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.5f, Pitch = 0.2f },
                    center);
            }
        }

        private static void EmitTickVisual(int tileX, int tileY, int elapsed) {
            if (elapsed % 40 != 0) return;
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);
            PRTLoader.NewParticle<PRT_Spark>(center,
                new Vector2(0f, Main.rand.NextFloat(-1f, -0.2f)),
                HackTheme.Accent, 0.5f)?.Configure(false, 20);
        }

        private static void EmitReleaseVisual(int tileX, int tileY) {
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, HackTheme.AccentAlt, 0.8f)
                    ?.Configure(false, 22);
            }
        }

        #endregion
    }

    /// <summary>
    /// 征用板块。ai[0] 状态：0 环绕 / 1 射出 / 2 挡刀碎裂；
    /// ai[1] 物块类型（贴图、还原、伤害语义都认它）；ai[2] 环位序号。<br/>
    /// 状态迁移全部由拥有者端决定并靠 netUpdate 广播；
    /// 原格座标与掉落物类型是权威端私账，「还块」只在权威端结算
    /// </summary>
    internal class ConscriptPlateProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float OrbitRadius = 56f;
        private const float OrbitAngularSpeed = 0.03f;
        private const float LaunchSpeed = 22f;
        //挡刀碎裂前的滞留帧：等拥有者的状态包先落地，权威端才不会把消耗误判成环绕退还
        private const int ConsumeLinger = 10;
        //发射后的飞行寿命
        private const int FlightLife = 150;

        private const float StateOrbit = 0f;
        private const float StateLaunched = 1f;
        private const float StateConsumed = 2f;

        /// <summary>征用时的原格 X，客户端副本恒为 -1</summary>
        internal int OriginX = -1;
        /// <summary>征用时的原格 Y</summary>
        internal int OriginY = -1;
        /// <summary>该格应掉何物，退还走掉落分支时用</summary>
        internal int DropItemType;

        private ref float State => ref Projectile.ai[0];
        private int PlateTileType => (int)Projectile.ai[1];
        private ref float OrbitSlot => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];
        private ref float DelayTimer => ref Projectile.localAI[1];

        private bool launchCuePlayed;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = TileConscript.ConscriptDuration + 90 + FlightLife;
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Generic;
        }

        //环绕与碎裂阶段是护甲不是武器
        public override bool? CanHitNPC(NPC target)
            => State == StateLaunched ? null : false;

        public override void AI() {
            Age++;
            if (State == StateOrbit) {
                TickOrbit();
            }
            else if (State == StateLaunched) {
                TickFlight();
            }
            else {
                TickConsumed();
            }
        }

        private void TickOrbit() {
            Player owner = Owner;
            if (owner == null || !owner.active || owner.dead) {
                //施术者倒下，板块作废，走 OnKill 的退还
                Projectile.Kill();
                return;
            }

            float angle = OrbitSlot / TileConscript.MaxPlates * MathHelper.TwoPi
                + Age * OrbitAngularSpeed;
            //前 12 帧从贴身撑到全环，读作「列队归位」
            float radius = OrbitRadius * MathHelper.Clamp(Age / 12f, 0.2f, 1f);
            Projectile.Center = owner.Center + angle.ToRotationVector2() * radius;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.04f;

            //发射决策只归拥有者端；服务端与旁观端等状态包
            if (Projectile.owner != Main.myPlayer) return;

            if (DelayTimer > 0f) {
                if (--DelayTimer <= 0f) {
                    Launch();
                }
                return;
            }
            if (Age >= TileConscript.ConscriptDuration) {
                //到期打出去（协议描述的承诺），依环位错帧成串
                DelayTimer = 1f + OrbitSlot * 4f;
                return;
            }
            //右键提前引爆；骇入界面开着时右键归界面，不抢
            if (Main.mouseRight && Main.mouseRightRelease
                && !owner.mouseInterface && !HackTime.Active) {
                DelayTimer = 1f + OrbitOrderAmongAlive() * 5f;
            }
        }

        private void TickFlight() {
            Projectile.tileCollide = true;
            Projectile.friendly = true;
            Projectile.rotation += 0.35f;
            if (!launchCuePlayed) {
                launchCuePlayed = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f, Volume = 0.7f },
                        Projectile.Center);
                }
            }
            if (Main.netMode != NetmodeID.Server && Age % 3 == 0) {
                var trail = PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    -Projectile.velocity * 0.06f, HackTheme.Accent, 0.8f);
                trail?.Configure(HackTheme.AccentAlt, 14);
            }
        }

        private void TickConsumed() {
            Projectile.velocity = Vector2.Zero;
            DelayTimer++;
            if (DelayTimer >= ConsumeLinger && Projectile.owner == Main.myPlayer) {
                Projectile.Kill();
                return;
            }
            //拥有者掉线的兜底，权威端晚一点自己收
            if (DelayTimer >= ConsumeLinger * 6
                && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.Kill();
            }
        }

        //本圈里排在我前面的存活环绕板数，右键齐射按它错帧
        private int OrbitOrderAmongAlive() {
            int order = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Projectile.owner
                    && proj.type == Projectile.type && proj.ai[0] == StateOrbit
                    && proj.ai[2] < OrbitSlot) {
                    order++;
                }
            }
            return order;
        }

        //仅拥有者端调用，光标即弹道
        private void Launch() {
            Vector2 dir = Projectile.Center.To(Main.MouseWorld)
                .SafeNormalize(Vector2.UnitY);
            State = StateLaunched;
            Projectile.velocity = dir * LaunchSpeed;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, FlightLife);
            Projectile.netUpdate = true;
        }

        /// <summary>替玩家挡下一次命中，转入碎裂滞留；仅拥有者端调用</summary>
        internal void BeginConsume() {
            if (State != StateOrbit) return;
            State = StateConsumed;
            DelayTimer = 0f;
            Projectile.netUpdate = true;
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.8f },
                    Projectile.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                EmitShatter();
            }
            //射出与挡刀是正常消耗；环绕中夭折的板块由权威端退还：
            //优先放回原格，放不回（原格被占/规则拒绝）就掉出物品。
            //拥有者的状态包若晚于击杀包到达，权威端会按环绕多退一块，窗口一个 RTT，接受
            if (Main.netMode == NetmodeID.MultiplayerClient || State != StateOrbit) return;
            if (!HackTargets.InWorld(OriginX, OriginY)) return;

            int type = PlateTileType;
            bool restored = false;
            if (!Main.tile[OriginX, OriginY].HasTile
                && type >= 0 && type < TileLoader.TileCount) {
                restored = WorldGen.PlaceTile(OriginX, OriginY, type,
                    mute: true, forced: false);
                if (restored && Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendTileSquare(-1, OriginX, OriginY);
                }
            }
            if (!restored && DropItemType > 0) {
                Item.NewItem(Projectile.GetSource_Death(), Projectile.Hitbox,
                    DropItemType);
            }
        }

        private void EmitShatter() {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    HackTheme.Accent, 0.8f)?.Configure(true, 24);
            }
            var square = PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                Vector2.Zero, Color.White, 1.4f);
            square?.Configure(HackTheme.Accent, 16);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            int type = PlateTileType;
            if (type < 0 || type >= TileLoader.TileCount) return false;
            Main.instance.LoadTiles(type);
            Texture2D tex = TextureAssets.Tile[type].Value;
            //取「四面有邻」的标准帧，图集不够大就退回左上帧
            Rectangle source = tex.Width >= 34 && tex.Height >= 34
                ? new Rectangle(18, 18, 16, 16)
                : new Rectangle(0, 0, Math.Min(16, tex.Width), Math.Min(16, tex.Height));
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = source.Size() * 0.5f;
            float scale = Projectile.scale * 1.35f;

            Color body = Color.Lerp(lightColor, Color.White, 0.35f);
            if (State == StateConsumed) {
                body = Color.Lerp(body, Color.White, 0.7f);
            }
            //A=0 的描边层在 AlphaBlend 下等效加色，读作「被征用的全息块」
            Color rim = new Color(HackTheme.Accent.R, HackTheme.Accent.G,
                HackTheme.Accent.B, 0) * 0.45f;
            Main.EntitySpriteDraw(tex, drawPos, source, rim, Projectile.rotation,
                origin, scale * 1.18f, SpriteEffects.None);
            Main.EntitySpriteDraw(tex, drawPos, source, body, Projectile.rotation,
                origin, scale, SpriteEffects.None);
            return false;
        }
    }

    /// <summary>环绕板块替玩家挡刀：受伤管线里优先碎一块并完全免伤</summary>
    internal class TileConscriptPlayer : ModPlayer
    {
        public override bool ConsumableDodge(Player.HurtInfo info) {
            //受伤结算跑在被打玩家自己的端上，板块归属同一人，本机直接消耗
            if (Player.whoAmI != Main.myPlayer) return false;
            int plateType = ModContent.ProjectileType<ConscriptPlateProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active != true || proj.owner != Player.whoAmI
                    || proj.type != plateType || proj.ai[0] != 0f) {
                    continue;
                }
                if (proj.ModProjectile is not ConscriptPlateProj plate) continue;
                plate.BeginConsume();
                //挡刀附带一段全类型无敌，免得一串弹幕一帧吃穿整圈
                Player.SetImmuneTimeForAllTypes(40);
                return true;
            }
            return false;
        }
    }
}
