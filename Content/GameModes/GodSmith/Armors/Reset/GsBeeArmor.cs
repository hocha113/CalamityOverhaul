using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 蜜蜂套 · 蜂后近卫（召唤）。单件沿用原版（头 +1 栏 +4%、胸 +1 栏 +4%、腿 +5% 召唤伤害）。<br/>
    /// 原版旗标清点：套装 +10% 召唤伤害 → 原样补回；无删除项。<br/>
    /// 签名：三只护卫蜂环绕（召唤伤害、吃召唤加成、不占栏位），敌人靠近即扑咬；
    /// 穿戴者受击时全体护卫蜂立刻扑向攻击者
    /// </summary>
    internal class GsBeeArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.BeeHeadgear];
        public override int BodyID => ItemID.BeeBreastplate;
        public override int LegsID => ItemID.BeeGreaves;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "10% increased summon damage; three giant bees circle you and sting any enemy that comes close, and they all swarm whoever hurts you";

        /// <summary>护卫蜂数量</summary>
        internal const int GuardCount = 3;

        /// <summary>护卫蜂基础伤害（随召唤加成实时缩放）</summary>
        private const int GuardDamage = 8;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Summon) += 0.10f;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //护卫蜂只由穿戴者本端补员，各蜂按 ai[0] 序号分相位
            if (player.whoAmI != Main.myPlayer || player.dead) {
                return;
            }
            int type = ModContent.ProjectileType<GsBeeGuardProj>();
            if (player.ownedProjectileCounts[type] >= GuardCount) {
                return;
            }
            bool[] taken = new bool[GuardCount];
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    int slot = (int)proj.ai[0];
                    if (slot >= 0 && slot < GuardCount) {
                        taken[slot] = true;
                    }
                }
            }
            for (int i = 0; i < GuardCount; i++) {
                if (taken[i]) {
                    continue;
                }
                SpawnProc(player, "GodSmithBeeEndow", player.Center, Vector2.Zero, type, GuardDamage, 1f, i);
                break;
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //蜂群反击：受击者本端裁定，攻击者写进每只蜂的追击位随同步过线
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            NPC attacker = HurtSourceNPC(info);
            if (attacker == null || attacker.friendly) {
                return;
            }
            int type = ModContent.ProjectileType<GsBeeGuardProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    proj.ai[1] = attacker.whoAmI + 1;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }
    }

    /// <summary>
    /// 护卫大蜜蜂：借原版大蜜蜂贴图，绕主人环飞（ai[0] = 相位序号），
    /// 敌人进入警戒圈即扑咬（ai[1] = 追击目标 + 1，0 = 环飞），咬到或追久了就飞回；
    /// 套装失效后不再续命，一秒内自行消散
    /// </summary>
    internal class GsBeeGuardProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GiantBee;

        private ref float Slot => ref Projectile.ai[0];

        private ref float ChasePlusOne => ref Projectile.ai[1];

        /// <summary>追击已持续帧数</summary>
        private ref float ChaseFrames => ref Projectile.localAI[0];

        private const float OrbitRadius = 56f;
        private const float AlertRange = 260f;
        private const int MaxChaseFrames = 75;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.GiantBee]);
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.ContinuouslyUpdateDamageStats = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //套装仍生效才续命；失效后放任 timeLeft 走完（远端装备同步略滞后也不会闪灭）
            if (owner.GetModPlayer<GodSmithArmorPlayer>().HasBonus<GsBeeArmor>()) {
                Projectile.timeLeft = 60;
            }

            NPC chase = ChasePlusOne > 0 ? Main.npc[(int)ChasePlusOne - 1] : null;
            if (chase != null && (!chase.active || chase.friendly || ChaseFrames > MaxChaseFrames
                || chase.Center.Distance(owner.Center) > AlertRange * 1.6f)) {
                chase = null;
                ChasePlusOne = 0;
                ChaseFrames = 0;
            }

            if (chase == null) {
                //环飞：各蜂按序号错开相位，轨迹随时间缓缓摆动
                float angle = Main.GameUpdateCount * 0.045f + Slot * MathHelper.TwoPi / GsBeeArmor.GuardCount;
                Vector2 want = owner.Center + angle.ToRotationVector2() * OrbitRadius
                    + new Vector2(0f, MathF.Sin(Main.GameUpdateCount * 0.11f + Slot) * 6f);
                Projectile.velocity = (want - Projectile.Center) * 0.18f;
                if (Projectile.owner == Main.myPlayer && Main.GameUpdateCount % 10 == Slot * 3) {
                    NPC target = FindTarget(owner.Center);
                    if (target != null) {
                        ChasePlusOne = target.whoAmI + 1;
                        ChaseFrames = 0;
                        Projectile.netUpdate = true;
                    }
                }
            }
            else {
                //扑咬：直取目标，速度带惯性
                ChaseFrames++;
                Vector2 want = (chase.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.2f);
            }

            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
            Projectile.rotation = Projectile.velocity.X * 0.05f;
            if (++Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        private NPC FindTarget(Vector2 from) {
            NPC best = null;
            float bestDist = AlertRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = from.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //咬中即收翅飞回
            ChasePlusOne = 0;
            ChaseFrames = 0;
            Projectile.netUpdate = true;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Honey2,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f));
                dust.noGravity = true;
            }
        }
    }
}
