using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 「荆棘丛」权威端布点系统：残酷模式的纯净森林里，战斗打响时在玩家与来敌之间
    /// 零星拱出可破坏的荆棘丛，封走位不封生路。纯客户端的 <see cref="WoodsongAmbience"/>
    /// 不碰判定，危害层单独走权威端（镜像 RotmireVentSystem 的门控与伤害锚定）
    /// </summary>
    internal class WoodsongBrambleSystem : ModSystem
    {
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
        /// <summary>伤害 = 原版森林标兵接触伤 × 此系数 × 0.5</summary>
        private const float DamageFrac = 0.8f;

        private static readonly int[] cooldowns = new int[Main.maxPlayers];
        private static int scanTimer;

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
                if (player.dead || !WoodsongAmbience.LocalInPureForest(player)) {
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
            Vector2 pos = new(tileX * 16f + 8f, surfY * 16f - 14f);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), pos, Vector2.Zero,
                ModContent.ProjectileType<WoodsongBrambleProj>(), AnchorDamage(DamageFrac), 0f, Main.myPlayer);
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
    /// 可破坏荆棘丛：40 帧无害拱出（伤害窗=可见窗），落位后静止不追踪；
    /// 被玩家攻击命中 3 拍即碎（近战贴身挥击同样计数），寿命尽头自枯
    /// </summary>
    internal class WoodsongBrambleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_98 = null;

        /// <summary>无害拱出帧（公平契约 ≥30，档位不缩短）</summary>
        internal const int SproutFrames = 40;
        private const int ActiveFrames = 540;
        private const int FadeFrames = 24;
        /// <summary>破坏所需命中拍数：砍三下就碎</summary>
        private const int BreakHits = 3;
        /// <summary>命中拍最小间隔（帧），防同一发弹幕连帧连计</summary>
        private const int HitTickGap = 8;
        /// <summary>近战挥击计拍的贴身距离（像素）</summary>
        private const float MeleeReach = 78f;

        //真 alpha 暗绿外壳 + 苔色亮芯（M5 双层配方，暗层必须 A>0）
        private static readonly Color ThornDark = new(30, 44, 24);
        private static readonly Color MossCore = new(96, 128, 62);

        private int Age => SproutFrames + ActiveFrames + FadeFrames - Projectile.timeLeft;
        private float Growth => MathHelper.Clamp(Age / (float)SproutFrames, 0f, 1f);
        private bool Fading => Projectile.timeLeft <= FadeFrames;

        /// <summary>命中拍计数（权威端私产，碎裂经 Kill 原生同步）</summary>
        private int hitTicks;
        private int hitGapTimer;

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SproutFrames + ActiveFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=可见窗：拱出期与枯萎期都无害</summary>
        public override bool? CanDamage() => Growth >= 1f && !Fading ? null : false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //拱出期的破土绿尘（纯客户端表现）
            if (!Main.dedServ && Growth < 1f && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GrassBlades, 0f, -1.2f, 100, default, 0.9f);
                dust.noGravity = true;
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
            Texture2D tex = Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            //拱出缓出曲线 + 末期枯萎收缩
            float grow = 1f - (1f - Growth) * (1f - Growth);
            float fade = Fading ? Projectile.timeLeft / (float)FadeFrames : 1f;
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Projectile.whoAmI) * 0.05f;

            //三丛主体：暗绿外壳（A>0 遮挡层）+ 苔色亮芯
            Span<Vector2> lobes = [new(-15f, 3f), new(0f, -4f), new(15f, 4f)];
            Span<float> lobeScale = [0.17f, 0.21f, 0.16f];
            for (int i = 0; i < lobes.Length; i++) {
                Vector2 pos = basePos + lobes[i] * grow;
                float rot = sway * (i - 1);
                Vector2 scale = new Vector2(lobeScale[i], lobeScale[i] * 0.72f) * grow;
                Main.EntitySpriteDraw(tex, pos, null, ThornDark * (0.95f * fade), rot,
                    origin, scale * 1.18f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos, null, MossCore with { A = 0 } * (0.5f * fade), rot,
                    origin, scale * 0.66f, SpriteEffects.None, 0);
            }
            //棘刺：细长暗签自丛体斜出（同为 A>0 暗层）
            for (int i = 0; i < 5; i++) {
                float ang = -MathHelper.PiOver2 + (i - 2) * 0.45f + sway;
                Vector2 pos = basePos + new Vector2((i - 2) * 9f, -4f) * grow;
                Main.EntitySpriteDraw(tex, pos, null, ThornDark * (0.9f * fade), ang,
                    origin, new Vector2(0.028f, 0.14f) * grow, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
