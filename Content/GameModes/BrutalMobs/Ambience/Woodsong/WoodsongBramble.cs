using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 「荆棘丛」权威端布点：残酷纯净森林、战斗打响时在玩家与来敌之间
    /// 拱出可破坏灌木，封走位不封生路。氛围层 <see cref="WoodsongAmbience"/> 不碰判定
    /// </summary>
    internal class WoodsongBrambleSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "GameModes";

        /// <summary>踩进荆棘的死亡句（{0}=玩家名）</summary>
        internal static LocalizedText BrambleDeathReason { get; private set; }

        /// <summary>布点资格重扫间隔（帧）</summary>
        private const int ScanInterval = 30;
        /// <summary>触发后的每玩家冷却（帧）</summary>
        private const int PlayerCooldown = 960;
        /// <summary>战斗感知半径：有敌对个体锁定玩家且进圈才算战斗</summary>
        private const float CombatSenseRange = 900f;
        /// <summary>世界并发上限（布点循环真正读取）</summary>
        private const int WorldCap = 3;
        /// <summary>距玩家的最小落位间距（像素，公平阀门：不许贴脸拱刺）</summary>
        private const float MinPlayerGapPx = 90f;
        /// <summary>沿敌向的落位距离窗口（像素）</summary>
        private const float PlaceMin = 120f;
        private const float PlaceMax = 250f;
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownPeaceRange = 960f;
        /// <summary>伤害 = 原版森林标兵接触伤 × 此系数 × 0.5</summary>
        private const float DamageFrac = 0.8f;
        /// <summary>踩上弹出，避免站在盒子里连吃</summary>
        internal const float PlaceKnockback = 4.5f;

        private static readonly int[] cooldowns = new int[Main.maxPlayers];
        private static int scanTimer;

        public override void SetStaticDefaults() {
            BrambleDeathReason = this.GetLocalization(nameof(BrambleDeathReason),
                () => "{0} stepped into the forest bramble");
        }

        public override void ClearWorld() {
            Array.Clear(cooldowns);
            scanTimer = 0;
        }

        public override void PostUpdateEverything() {
            //决策与生成只在权威端；客户端看到的一切来自已同步的荆棘实体
            if (VaultUtils.isClient || !GameModeSystem.BrutalActive || CWRWorld.HasBoss) {
                return;
            }
            if (++scanTimer < ScanInterval) {
                return;
            }
            scanTimer = 0;

            foreach (Player player in Main.ActivePlayers) {
                int idx = player.whoAmI;
                if (cooldowns[idx] > 0) {
                    cooldowns[idx] -= ScanInterval;
                    continue;
                }
                if (player.dead || !WoodsongAmbience.LocalInPureForest(player) || NearTown(player.Center)) {
                    continue;
                }
                NPC threat = FindThreat(player);
                if (threat == null) {
                    continue;
                }
                if (CountAlive() >= WorldCap) {
                    return;//全局闸满，本轮整体作罢
                }
                if (TryPlace(player, threat)) {
                    cooldowns[idx] = PlayerCooldown + Main.rand.Next(300);
                }
            }
        }

        /// <summary>960px 内有存活城镇 NPC 则不投放</summary>
        internal static bool NearTown(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.life > 0 && npc.Distance(pos) < TownPeaceRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>最近的锁定该玩家的敌对个体；无战斗则 null</summary>
        private static NPC FindThreat(Player player) {
            NPC best = null;
            float bestDist = CombatSenseRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.damage <= 0 || npc.lifeMax <= 5 || npc.SpawnedFromStatue) {
                    continue;
                }
                if (!npc.HasValidTarget || npc.target != player.whoAmI) {
                    continue;
                }
                float dist = npc.Distance(player.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>在场荆棘计数（仅触发时扫描，自愈无漂移）</summary>
        private static int CountAlive() {
            int type = ModContent.ProjectileType<WoodsongBrambleProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>在玩家与来敌之间的露天地表落一丛荆棘：封路不贴脸，落位一次成型不追踪</summary>
        private static bool TryPlace(Player player, NPC threat) {
            float side = threat.Center.X >= player.Center.X ? 1f : -1f;
            float dist = Math.Max(MinPlayerGapPx, Main.rand.NextFloat(PlaceMin, PlaceMax));
            int tileX = (int)((player.Center.X + side * dist) / 16f);
            if (!WoodsongAmbience.TryFindOutdoorSurfaceFor(player, tileX, out int surfY)) {
                return false;
            }
            Vector2 pos = new(tileX * 16f + 8f, surfY * 16f - WoodsongBrambleProj.HitHeight * 0.5f);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), pos, Vector2.Zero,
                ModContent.ProjectileType<WoodsongBrambleProj>(), AnchorDamage(DamageFrac),
                PlaceKnockback, Main.myPlayer);
            return true;
        }

        /// <summary>
        /// 伤害锚定原版森林标兵的接触伤（僵尸/困难后附魔盔甲），随难度自动跟走，
        /// 禁止再叠手动难度乘数。读取异常时用具名常量兜底（镜像 Rotmire 口径）
        /// </summary>
        private static int AnchorDamage(float frac) {
            int baseDamage = Main.hardMode ? 40 : 14;
            int anchorType = Main.hardMode ? NPCID.PossessedArmor : NPCID.Zombie;
            if (ContentSamples.NpcsByNetId.TryGetValue(anchorType, out NPC sample) && sample.damage > 0) {
                baseDamage = sample.damage;
            }
            return Math.Max(1, (int)(baseDamage * frac * 0.5f));
        }
    }

    /// <summary>
    /// 可破坏落叶灌木：48 帧无害拱出（伤害窗=可见窗），落位后静止不追踪；
    /// 被玩家攻击命中 3 拍即碎（近战贴身挥击同样计数），寿命尽头自枯
    /// </summary>
    internal class WoodsongBrambleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>无害拱出帧（公平契约 ≥45，档位不缩短）</summary>
        internal const int SproutFrames = 48;
        private const int ActiveFrames = 540;
        private const int FadeFrames = 24;
        /// <summary>破坏所需命中拍数：砍三下就碎</summary>
        private const int BreakHits = 3;
        /// <summary>命中拍最小间隔（帧），防同一发弹幕连帧连计</summary>
        private const int HitTickGap = 8;
        /// <summary>近战挥击计拍的贴身距离（像素）</summary>
        private const float MeleeReach = 96f;
        internal const int HitWidth = 80;
        internal const int HitHeight = 52;

        private const int LeafCols = 32;
        private const int LeafRows = 8;

        private int Age => SproutFrames + ActiveFrames + FadeFrames - Projectile.timeLeft;
        private float Growth => MathHelper.Clamp(Age / (float)SproutFrames, 0f, 1f);
        private bool Fading => Projectile.timeLeft <= FadeFrames;

        /// <summary>命中拍计数（权威端私产，碎裂经 Kill 原生同步）</summary>
        private int hitTicks;
        private int hitGapTimer;

        public override void SetDefaults() {
            Projectile.width = HitWidth;
            Projectile.height = HitHeight;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SproutFrames + ActiveFrames + FadeFrames;
            Projectile.knockBack = WoodsongBrambleSystem.PlaceKnockback;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=可见窗：拱出期与枯萎期都无害</summary>
        public override bool? CanDamage() => Growth >= 1f && !Fading ? null : false;

        public override bool ShouldUpdatePosition() => false;

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void AI() {
            if (!Main.dedServ) {
                if (Age == 1) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.42f, Pitch = -0.18f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.28f, Pitch = -0.45f, MaxInstances = 2 }, Projectile.Center);
                }
                else if (Age == SproutFrames) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.34f, Pitch = -0.22f, MaxInstances = 2 }, Projectile.Center);
                }
                if (Growth < 1f && Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustDirect(
                        new Vector2(Projectile.position.X, Projectile.Bottom.Y - 8f),
                        Projectile.width, 8, DustID.GrassBlades, 0f, -1.2f, 100, default, 0.9f);
                    dust.noGravity = true;
                }
            }

            //破坏判定只在权威端；碎裂走 Kill 原生同步
            if (VaultUtils.isClient || Growth < 1f || Fading) {
                return;
            }
            if (hitGapTimer > 0) {
                hitGapTimer--;
                return;
            }
            bool hit = false;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.friendly && proj.damage > 0 && proj.Hitbox.Intersects(Projectile.Hitbox)) {
                    hit = true;
                    break;
                }
            }
            if (!hit) {
                //近战贴身挥击同样计拍：itemAnimation 已随玩家原生同步
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && player.itemAnimation > 0 && player.HeldItem.damage > 0
                        && player.Distance(Projectile.Center) < MeleeReach) {
                        hit = true;
                        break;
                    }
                }
            }
            if (!hit) {
                return;
            }
            hitGapTimer = HitTickGap;
            if (++hitTicks >= BreakHits) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GrassBlades, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2.5f, -0.5f),
                    80, default, 1.1f);
                dust.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_WoodsongLeaf>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 8f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-1.2f, 0.2f)),
                    Color.White, Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(Main.windSpeedCurrent, Main.rand.Next(90, 140));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float grow = 1f - (1f - Growth) * (1f - Growth);
            float fade = Fading ? Projectile.timeLeft / (float)FadeFrames : 1f;
            if (fade < 0.02f) {
                return false;
            }
            float visual = grow * (0.72f + 0.28f * fade);
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.35f + Projectile.identity) * 0.05f
                + Main.windSpeedCurrent * 0.10f;
            Vector2 ground = new Vector2(Projectile.Center.X, Projectile.Bottom.Y) - Main.screenPosition;
            Color lit = lightColor * fade;
            Color bark = Color.Lerp(lit, new Color(68, 52, 34), 0.55f);
            Color needle = Color.Lerp(lit, new Color(44, 66, 34), 0.45f);
            int seed = Projectile.identity;

            Main.instance.LoadProjectile(ProjectileID.NettleBurstLeft);
            Main.instance.LoadProjectile(ProjectileID.NettleBurstEnd);
            Main.instance.LoadGore(GoreID.TreeLeaf_Normal);
            Texture2D stemTex = TextureAssets.Projectile[ProjectileID.NettleBurstLeft].Value;
            Texture2D tipTex = TextureAssets.Projectile[ProjectileID.NettleBurstEnd].Value;
            Texture2D leafTex = TextureAssets.Gore[GoreID.TreeLeaf_Normal].Value;
            if (stemTex == null || tipTex == null || leafTex == null) {
                return false;
            }

            Rectangle stemFrame = stemTex.Frame(1, Math.Max(1, Main.projFrames[ProjectileID.NettleBurstLeft]), 0, 0);
            Rectangle tipFrame = tipTex.Frame(1, Math.Max(1, Main.projFrames[ProjectileID.NettleBurstEnd]), 0, 0);
            Vector2 stemOrigin = new(stemFrame.Width * 0.5f, stemFrame.Height);
            Vector2 tipOrigin = new(tipFrame.Width * 0.5f, tipFrame.Height);

            for (int i = 0; i < 6; i++) {
                float t = Mix(seed, i);
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-0.85f, 0.85f, i / 5f)
                    + (t - 0.5f) * 0.16f + sway;
                float len = MathHelper.Lerp(0.70f, 1.06f, Mix(seed, i + 17));
                Main.EntitySpriteDraw(stemTex, ground, stemFrame, bark, ang + MathHelper.PiOver2,
                    stemOrigin, new Vector2(0.88f, visual * len), SpriteEffects.None, 0);
            }

            for (int i = 0; i < 7; i++) {
                float t = Mix(seed, i + 40);
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-1.05f, 1.05f, i / 6f)
                    + (t - 0.5f) * 0.20f + sway * 1.15f;
                float len = MathHelper.Lerp(0.55f, 0.92f, Mix(seed, i + 61));
                Main.EntitySpriteDraw(tipTex, ground, tipFrame, needle, ang + MathHelper.PiOver2,
                    tipOrigin, new Vector2(0.82f, visual * len), SpriteEffects.None, 0);
            }

            for (int i = 0; i < 8; i++) {
                float t = Mix(seed, i + 90);
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-0.58f, 0.58f, i / 7f)
                    + (t - 0.5f) * 0.18f + sway;
                float rise = MathHelper.Lerp(22f, 42f, Mix(seed, i + 110)) * visual;
                Vector2 pos = ground + ang.ToRotationVector2() * rise;
                int row = (seed + i * 3) & (LeafRows - 1);
                Rectangle src = leafTex.Frame(LeafCols, LeafRows, 0, row, -2, -2);
                SpriteEffects flip = Mix(seed, i + 130) > 0.5f ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                Main.EntitySpriteDraw(leafTex, pos, src, lit, (t - 0.5f) * 0.7f + sway * 1.6f,
                    src.Size() * 0.5f, MathHelper.Lerp(0.85f, 1.15f, Mix(seed, i + 150)) * visual,
                    flip, 0);
            }
            return false;
        }

        private static float Mix(int seed, int i) {
            uint h = (uint)(seed * 374761393 + i * 668265263);
            h ^= h >> 13;
            h *= 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }
    }

    internal class WoodsongBramblePlayer : ModPlayer
    {
        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (damageSource.SourceProjectileType == ModContent.ProjectileType<WoodsongBrambleProj>()
                && WoodsongBrambleSystem.BrambleDeathReason != null) {
                damageSource = PlayerDeathReason.ByCustomReason(
                    WoodsongBrambleSystem.BrambleDeathReason.ToNetworkText(Player.name));
            }
            return true;
        }
    }
}
