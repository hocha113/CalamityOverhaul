using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// S.D.M.G. 重铸：终局特权三档。基数极强，全部加成克制。<br/>
    /// [海豚狂涛]：原版极速原样，青色浪尾稀疏点缀。<br/>
    /// [声呐点射]：3 连点射，链末发命中挂「声呐标记」4 秒；
    /// 被标记目标吃全队远程伤害 +8%（联机协作亮点，标记为弹幕实体全端可见）。<br/>
    /// [轨道微导]：子弹轻微追踪、伤害 -10% 的懒人扫场形态
    /// </summary>
    internal class GsSDMG : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.SDMG;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to cycle three fire modes\n" +
            "Dolphin Tide keeps the legendary speed; Sonar Burst tags the third hit so the whole team's ranged shots dig 8% deeper\n" +
            "Orbital Guide gently homes for 10% less punch";

        /// <summary>声呐弹私有 flag</summary>
        private const int FlagSonar = 1;

        /// <summary>海豚青</summary>
        internal static readonly Color DolphinCyan = new(92, 206, 255);

        /// <summary>本次射击为声呐弹的世界帧（打标窗口消费）</summary>
        private uint sonarShotTick = uint.MaxValue;

        /// <summary>狂涛枪口演出节流（每 3 发一次，高射速性能红线）</summary>
        private int muzzleParity;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeDolphinTide", EnName = "Dolphin Tide",
            },
            new GsFireMode {
                Key = "ModeSonarBurst", EnName = "Sonar Burst",
                DamageMul = 1.10f, Converge = 0.6f,
                BurstCount = 3, BurstRest = 24,
            },
            new GsFireMode {
                Key = "ModeOrbitalGuide", EnName = "Orbital Guide",
                DamageMul = 0.90f,
            },
        ];

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (mp.ModeIndex == 1 && mp.BurstShots == mode.BurstCount - 1) {
                sonarShotTick = Main.GameUpdateCount;
            }
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //狂涛枪口浪花每 3 发一次（5t 射速的粒子预算红线）
            if (!VaultUtils.isServer && mp.ModeIndex == 0 && ++muzzleParity % 3 == 0) {
                Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
                PRTLoader.NewParticle<PRT_Spark>(position + unit * 18f,
                    unit.RotatedByRandom(0.3) * Main.rand.NextFloat(2f, 4f),
                    DolphinCyan, Main.rand.NextFloat(0.26f, 0.4f))?.Configure(false, Main.rand.Next(7, 12));
            }
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            router.MarkData = PackMark(mp.ModeIndex, sonarShotTick == Main.GameUpdateCount ? FlagSonar : 0);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            int mode = MarkModeOf(router.MarkData);
            //轨道微导：轻追踪（各端同源找最近目标，微转率漂移由弹幕同步纠正）
            if (mode == 2) {
                NPC target = FindHomingTarget(proj);
                if (target != null) {
                    float speed = proj.velocity.Length();
                    Vector2 want = (target.Center - proj.Center).SafeNormalize(Vector2.UnitX) * speed;
                    proj.velocity = Vector2.Lerp(proj.velocity, want, 0.03f).SafeNormalize(Vector2.UnitX) * speed;
                }
            }
            //声呐弹飞行相：青色声纹稀疏尾
            if (MarkFlagOf(router.MarkData) == FlagSonar && !VaultUtils.isServer && proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f, DolphinCyan,
                    Main.rand.NextFloat(0.24f, 0.36f))?.Configure(false, Main.rand.Next(7, 11));
            }
        }

        private static NPC FindHomingTarget(Projectile proj) {
            NPC best = null;
            float bestDist = 260f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(proj)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, proj.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：声呐弹命中挂标记（owner 生成标记弹幕，全端可见）
            if (MarkFlagOf(router.MarkData) != FlagSonar || target.friendly || !target.active) {
                return;
            }
            int markType = ModContent.ProjectileType<GsSonarMarkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active && other.type == markType && (int)other.ai[0] == target.whoAmI) {
                    other.timeLeft = GsSonarMarkProj.Duration;
                    other.netUpdate = true;
                    return;
                }
            }
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                markType, 0, 0f, proj.owner, target.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.45f, Pitch = 0.55f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 声呐标记：跟随目标的 4 秒标记弹幕（ai0 = 目标 NPC 序号）。
    /// 每帧向本端声呐表刷写有效期；任何端上任何玩家的远程弹幕命中被标记目标时 +8%，
    /// 结算走 <see cref="GsSonarBoostGlobalProj"/> 查表（命中判定端与标记弹幕同步共存，天然一致）
    /// </summary>
    internal class GsSonarMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>标记时长（4 秒）</summary>
        public const int Duration = 240;

        /// <summary>本端声呐表：NPC 序号 → 标记有效期（世界帧），由在场标记弹幕逐帧刷新</summary>
        internal static uint[] SonarUntil = new uint[Main.maxNPCs + 1];

        private NPC Target {
            get {
                int idx = (int)Projectile.ai[0];
                return idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            }
        }

        public override void SetStaticDefaults()
            => GsGunsHardSystem.UnloadActions.Add(() => SonarUntil = new uint[Main.maxNPCs + 1]);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
        }

        public override void AI() {
            NPC target = Target;
            if (target == null || !target.active || target.life <= 0) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;
            //刷表：各端本地表由同步在场的标记弹幕驱动，查询端零遍历
            SonarUntil[(int)Projectile.ai[0]] = Main.GameUpdateCount + 2;
        }

        public override bool? CanHitNPC(NPC target) => false;

        public override bool PreDraw(ref Color lightColor) {
            //声呐扩散环：identity 定相位，禁随机
            NPC target = Target;
            if (target == null) {
                return false;
            }
            float phase = (Main.GlobalTimeWrappedHourly * 1.4f + Projectile.identity * 0.31f) % 1f;
            float radius = MathHelper.Lerp(14f, target.width * 0.9f + 34f, phase);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 4f,
                Color.Lerp(GsSDMG.DolphinCyan, Color.White, 0.4f), GsSDMG.DolphinCyan,
                GsSDMG.DolphinCyan * 0.4f, (1f - phase) * 0.5f, timeSeed: Projectile.identity * 0.53f);
            return false;
        }
    }

    /// <summary>声呐增伤结算：任何远程弹幕命中被标记目标 +8%（查本地声呐表，O(1) 零遍历）</summary>
    internal class GsSonarBoostGlobalProj : GlobalProjectile
    {
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (!GameModeSystem.GodSmithActive
                || !projectile.CountsAsClass(DamageClass.Ranged)
                || target.whoAmI >= GsSonarMarkProj.SonarUntil.Length) {
                return;
            }
            if (GsSonarMarkProj.SonarUntil[target.whoAmI] > Main.GameUpdateCount) {
                modifiers.FinalDamage *= 1.08f;
            }
        }
    }
}
