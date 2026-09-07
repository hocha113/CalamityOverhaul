using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 甲虫鳞甲（近战·进攻）：单件沿用原版。<br/>
    /// 原版旗标清点：甲虫之力（连击增伤）→ 以本套的层数机制替代（有意改写，同为「连击越久越强」）。<br/>
    /// 签名：连续命中积攒甲虫之力（每击 +1 层，最多 20 层），每层近战伤害 +1%、近战攻速 +0.5%，2 秒未命中清零；
    /// 满层时命中有 20% 概率放出一只圣甲虫俯冲目标
    /// </summary>
    internal class GsBeetleScaleArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.BeetleHelmet];
        public override int BodyID => ItemID.BeetleScaleMail;
        public override int LegsID => ItemID.BeetleLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Consecutive hits build Beetle Might, +1 stack per hit up to 20; each stack grants 1% melee damage and 0.5% melee attack speed, and 2 seconds without hitting resets it; at full stacks hits have a 20% chance to send a scarab diving at the target for 60% of the hit";

        private const int MaxStacks = 20;

        /// <summary>断层判定：多少帧没命中就清零</summary>
        private const int DecayFrames = 120;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            int stacks = Math.Clamp(Tally(player), 0, MaxStacks);
            if (stacks <= 0) {
                return;
            }
            player.GetDamage(DamageClass.Melee) += 0.01f * stacks;
            player.GetAttackSpeed(DamageClass.Melee) += 0.005f * stacks;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (TallyStale(player, DecayFrames)) {
                SetTally(player, 0);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            int stacks = Tally(player);
            if (stacks < MaxStacks) {
                SetTally(player, stacks + 1);
                if (stacks + 1 == MaxStacks && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
                }
                return;
            }
            SetTally(player, MaxStacks);
            if (target.type == NPCID.TargetDummy || target.life <= 0 || Main.rand.Next(100) >= 20) {
                return;
            }
            Vector2 from = player.Center + new Vector2(-player.direction * 30f, -50f);
            Vector2 velocity = (target.Center - from).SafeNormalize(Vector2.UnitY) * 6f;
            SpawnProc(player, "GodSmithBeetleScaleEndow", from, velocity,
                ModContent.ProjectileType<GsBeetleArmorScarabProj>(), ProcDamage(damageDone, 0.6f, 15, 90), 6f, target.whoAmI);
        }
    }

    /// <summary>
    /// 甲虫外壳（近战·防御）：单件沿用原版。<br/>
    /// 原版旗标清点：甲虫耐力（受击叠减伤）→ 以本套的层数机制替代（有意改写，同为「挨打越多越硬」）。<br/>
    /// 签名：受到伤害时获得一层甲壳（最多 3 层），每层防御 +6、受到的伤害降低 6%，10 秒未受伤全部消散；
    /// 满 3 层时再受伤会震碎甲壳，朝来袭方向扇形喷出 5 片壳，每片造成防御力 2 倍的伤害
    /// </summary>
    internal class GsBeetleShellArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.BeetleHelmet];
        public override int BodyID => ItemID.BeetleShell;
        public override int LegsID => ItemID.BeetleLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Taking damage grants a shell layer (up to 3), each layer giving 6 defense and 6% damage reduction, all fading after 10 seconds without being hit; being hit at 3 layers shatters them into 5 shell shards flung at your attacker, each dealing 2x your defense";

        private const int MaxLayers = 3;

        /// <summary>甲壳保持帧数</summary>
        private const int LayerFrames = 600;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            int layers = Math.Clamp(Tally(player), 0, MaxLayers);
            if (layers <= 0) {
                return;
            }
            player.statDefense += 6 * layers;
            player.endurance += 0.06f * layers;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (TallyStale(player, LayerFrames)) {
                SetTally(player, 0);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            int layers = Tally(player);
            if (layers < MaxLayers) {
                SetTally(player, layers + 1);
                return;
            }
            //满层再受伤：震碎甲壳，壳片朝来袭方向扇形迸出
            SetTally(player, 0);
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            NPC attacker = HurtSourceNPC(info);
            Vector2 dir = attacker != null
                ? (attacker.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction)
                : Vector2.UnitX * -player.direction;
            int damage = Math.Max(15, player.statDefense * 2);
            for (int i = -2; i <= 2; i++) {
                Vector2 velocity = dir.RotatedBy(i * 0.22f) * Main.rand.NextFloat(9f, 11f);
                SpawnProc(player, "GodSmithBeetleShellEndow", player.Center, velocity,
                    ModContent.ProjectileType<GsBeetleArmorShellProj>(), damage, 6f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.4f }, player.Center);
        }
    }

    /// <summary>圣甲虫：借圣甲虫炸弹贴图（4 帧），自肩后起飞先扬升再俯冲锁定目标（ai[0]），命中即碎</summary>
    internal class GsBeetleArmorScarabProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ScarabBomb;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.ScarabBomb]);
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            bool hasTarget = target != null && target.active && !target.friendly;
            if (Life < 12f) {
                //扬升：先向上抬升振翅
                Projectile.velocity.Y -= 0.6f;
            }
            else if (hasTarget) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 17f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (++Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit33 with { Volume = 0.6f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Stone,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f), 60, default, 1.1f);
                dust.noGravity = false;
            }
        }
    }

    /// <summary>甲壳碎片：借尖球贴图染暗，扇形迸出后受重力下坠，可穿透一次，触地即碎</summary>
    internal class GsBeetleArmorShellProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpikyBall;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(120, 90, 60, 255).MultiplyRGB(lightColor);

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.2f, 14f);
            Projectile.rotation += Projectile.velocity.X * 0.1f;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Stone,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1f));
                dust.noGravity = false;
            }
        }
    }
}
