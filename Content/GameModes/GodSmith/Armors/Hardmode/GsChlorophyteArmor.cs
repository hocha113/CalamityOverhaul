using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【叶绿套·共生寄藤 ★A】丛林活金属的共生秘术：①命中积攒孢子，满八层后下一击把寄生藤种进目标
    /// ②藤缠五秒，期间你每次击打宿主，藤便鞭抽一道传导藤锋咬向近旁第二个敌人
    /// ③藤谢时炸成驻场孢子云继续侵蚀。原版套装奖励（叶绿水晶叶）保留，神赋叠加
    /// </summary>
    internal class GsChlorophyteArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.ChlorophyteMask, ItemID.ChlorophyteHelmet, ItemID.ChlorophyteHeadgear];

        public override int BodyID => ItemID.ChlorophytePlateMail;

        public override int LegsID => ItemID.ChlorophyteGreaves;

        protected override string EndowLineFallback =>
            "Symbiotic Vine: strikes build spores; at 8 stacks the next strike plants a parasite vine, and every strike on the host lashes a vine-blade at a second foe; the vine bursts into a spore cloud when it withers";

        //叶绿色板
        internal static readonly Color ChloroMain = new(112, 220, 84);
        internal static readonly Color SporeLime = new(174, 255, 94);

        protected override int FullCharge => 8;

        protected override Color ThemeMain => ChloroMain;

        protected override Color ThemeBright => SporeLime;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsChlorophyteVineParasiteProj>()
            || proj.type == ModContent.ProjectileType<GsChlorophyteVineLashProj>();

        private static Projectile FindParasiteOn(Player player, int npcIndex) {
            int type = ModContent.ProjectileType<GsChlorophyteVineParasiteProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type
                    && proj.ai[0] == 0f && (int)proj.ai[1] == npcIndex) {
                    return proj;
                }
            }
            return null;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            //宿主在藤上：每次击打传导鞭抽（佩戴者端裁定）
            Projectile parasite = FindParasiteOn(player, target.whoAmI);
            if (parasite != null) {
                if (player.whoAmI == Main.myPlayer
                    && parasite.ModProjectile is GsChlorophyteVineParasiteProj vine) {
                    vine.TryLash();
                }
                //传导期间照常积攒，藤谢后无缝接力
                if (state.EndowCharge < FullCharge) {
                    state.EndowCharge++;
                }
                return;
            }
            base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.15f }, target.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int lashDamage = Math.Clamp((int)(damageDone * 0.25f), 6, 100);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithChlorophyteEndow"),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsChlorophyteVineParasiteProj>(),
                lashDamage, 0f, player.whoAmI, 0f, target.whoAmI);
        }
    }

    /// <summary>
    /// 寄生藤：种进宿主的活体藤蔓，缠附宿主躯体；
    /// 受主人号令向近旁第二敌鞭出传导藤锋（20 帧内至多一次）；
    /// 藤谢或宿主先亡即化作驻场孢子云，两秒内持续侵蚀过客。借原版叶绿水晶叶贴图默认绘制
    /// </summary>
    internal class GsChlorophyteVineParasiteProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalLeaf;

        /// <summary>0=缠附 1=孢子云</summary>
        private ref float State => ref Projectile.ai[0];

        private ref float HostIndex => ref Projectile.ai[1];

        /// <summary>鞭抽冷却（帧）</summary>
        private ref float LashCooldown => ref Projectile.localAI[1];

        /// <summary>孢子云时长</summary>
        private const int CloudFrames = 120;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        /// <summary>缠附态不判定，孢子云态才侵蚀</summary>
        public override bool? CanDamage() => State == 1f;

        /// <summary>受主人号令鞭出传导藤锋（佩戴者端调用）</summary>
        internal void TryLash() {
            if (State != 0f || LashCooldown > 0f || Projectile.owner != Main.myPlayer) {
                return;
            }
            NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[(int)HostIndex] : null;
            //找近旁第二个敌人
            NPC second = null;
            float bestDist = 320f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == (int)HostIndex || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    second = npc;
                }
            }
            if (second == null || host == null) {
                return;
            }
            LashCooldown = 20f;
            Vector2 vel = (second.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 24f;
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                Projectile.Center, vel,
                ModContent.ProjectileType<GsChlorophyteVineLashProj>(),
                Projectile.damage, 1f, Projectile.owner);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
            }
        }

        public override void AI() {
            if (LashCooldown > 0f) {
                LashCooldown--;
            }

            if (State == 0f) {
                NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[(int)HostIndex] : null;
                if (host == null || !host.active || Projectile.timeLeft <= 2) {
                    //藤谢/宿主亡：化作孢子云
                    State = 1f;
                    Projectile.timeLeft = CloudFrames;
                    Projectile.netUpdate = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                    }
                    return;
                }
                //缠附宿主
                Projectile.Center = host.Center;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            //孢子云：驻场缓漂
            Projectile.velocity *= 0.96f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 180);
    }

    /// <summary>
    /// 传导藤锋：藤上鞭出的一道绿锋，快甩慢收，命中挂毒；借原版水晶叶飞弹贴图默认绘制
    /// </summary>
    internal class GsChlorophyteVineLashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalLeafShot;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 16;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //快甩慢收
            if (Life > 7f) {
                Projectile.velocity *= 0.84f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
        }
    }
}
