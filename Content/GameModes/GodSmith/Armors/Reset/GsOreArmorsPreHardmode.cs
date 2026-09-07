using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 肉前八套矿石甲的公共层 · 金属物性阶梯（R3 路 B）：头盔与护腿无单件属性，胸甲按矿石档位给挖掘速度；
    /// 套装奖励 = 原版量级的防御（只在穿着时给）+ 一条职业中立的金属物性（被镶嵌继承时也给）。
    /// 头盔可镶嵌低一档头盔并在整套穿好后继承其物性（镶嵌链数据见 GodSmithHelmetNestItem，配方见 GsHelmetNest）。<br/>
    /// 原版旗标清点：套装 +2/+3/+4 防 → 按档补回（穿着时）；无删除项
    /// </summary>
    internal abstract class GsOreArmorScheme : GsResetArmorScheme
    {
        /// <summary>胸甲提供的挖掘速度提升比例（0.05 = 快 5%）</summary>
        protected abstract float MiningSpeed { get; }

        /// <summary>穿着整套时的防御奖励（原版量级）</summary>
        protected abstract int SetDefense { get; }

        public override void UpdateBody(Player player, Item item) => player.pickSpeed -= MiningSpeed;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            if (IsWorn(state)) {
                player.statDefense += SetDefense;
            }
            UpdateProperty(player, state);
        }

        /// <summary>金属物性的数值层（穿着与被继承都生效）</summary>
        protected virtual void UpdateProperty(Player player, GodSmithArmorPlayer state) { }
    }

    /// <summary>铜套 · 静电：每 6 次命中向最近的另一名敌人跳一道电弧（伤害 = 本次命中 40%，类型跟随命中）</summary>
    internal class GsCopperArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CopperHelmet];
        public override int BodyID => ItemID.CopperChainmail;
        public override int LegsID => ItemID.CopperGreaves;
        protected override float MiningSpeed => 0.05f;
        protected override int SetDefense => 2;
        protected override string BodyLineFallback => "5% faster mining speed";
        protected override string SetBonusLineFallback =>
            "2 more defense; every 6th hit arcs static electricity to the nearest other enemy for 40% of the hit";

        private const int HitsPerArc = 6;
        private const float ArcRange = 240f;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (target.type == NPCID.TargetDummy || !TallyUp(player, HitsPerArc)) {
                return;
            }
            NPC second = NearestEnemy(target.Center, ArcRange, target.whoAmI);
            if (second == null) {
                return;
            }
            Projectile arc = SpawnProc(player, "GodSmithCopperEndow", second.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCopperArmorArcProj>(), ProcDamage(damageDone, 0.4f, 3, 12), 2f,
                target.Center.X, target.Center.Y);
            if (arc != null) {
                arc.DamageType = hit.DamageType;
            }
        }
    }

    /// <summary>锡套 · 锡鸣：受击时发出锡鸣，把周围六格内的敌人震退（冷却 5 秒）</summary>
    internal class GsTinArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TinHelmet];
        public override int BodyID => ItemID.TinChainmail;
        public override int LegsID => ItemID.TinGreaves;
        protected override float MiningSpeed => 0.06f;
        protected override int SetDefense => 2;
        protected override string BodyLineFallback => "6% faster mining speed";
        protected override string SetBonusLineFallback =>
            "2 more defense; taking damage rings out a tin chime that knocks nearby enemies away, once every 5 seconds";

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 300)) {
                return;
            }
            SpawnProc(player, "GodSmithTinEndow", player.Center, Vector2.Zero,
                ModContent.ProjectileType<GsTinArmorChimeProj>(), 2, 9f);
        }
    }

    /// <summary>铁套 · 铸铁站姿：站立不动 1 秒后进入站姿（防御 +5、穿甲 +10、免疫击退），移动即解除</summary>
    internal class GsIronArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.IronHelmet, ItemID.AncientIronHelmet];
        public override int BodyID => ItemID.IronChainmail;
        public override int LegsID => ItemID.IronGreaves;
        protected override float MiningSpeed => 0.08f;
        protected override int SetDefense => 3;
        protected override string BodyLineFallback => "8% faster mining speed";
        protected override string SetBonusLineFallback =>
            "3 more defense; standing still for 1 second plants you in a cast-iron stance: 5 more defense, 10 armor penetration and immunity to knockback until you move";

        /// <summary>进入站姿所需静止帧数</summary>
        private const int StanceFrames = 60;

        protected override void UpdateProperty(Player player, GodSmithArmorPlayer state) {
            if (Tally(player) < StanceFrames) {
                return;
            }
            player.statDefense += 5;
            player.GetArmorPenetration(DamageClass.Generic) += 10f;
            player.noKnockback = true;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            bool still = player.velocity == Vector2.Zero && !player.mount.Active && !player.dead;
            if (!still) {
                if (Tally(player) != 0) {
                    SetTally(player, 0);
                }
                return;
            }
            int frames = Tally(player);
            if (frames >= StanceFrames) {
                return;
            }
            SetTally(player, frames + 1);
            if (frames + 1 == StanceFrames && !Main.dedServ) {
                //进入站姿：脚下迸一撮铁火星
                SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.5f, Pitch = -0.4f }, player.Bottom);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(player.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f), DustID.Iron,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)), 0, default, 1.2f);
                    dust.noGravity = false;
                }
            }
        }
    }

    /// <summary>铅套 · 铅毒：命中 25% 概率挂铅毒（每秒 2 点，持续 4 秒，压掉再生）</summary>
    internal class GsLeadArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.LeadHelmet];
        public override int BodyID => ItemID.LeadChainmail;
        public override int LegsID => ItemID.LeadGreaves;
        protected override float MiningSpeed => 0.09f;
        protected override int SetDefense => 3;
        protected override string BodyLineFallback => "9% faster mining speed";
        protected override string SetBonusLineFallback =>
            "3 more defense; hits have a 25% chance to inflict Lead Poisoning (2 damage per second for 4 seconds)";

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (Main.rand.Next(100) >= 25) {
                return;
            }
            target.AddBuff(ModContent.BuffType<GsLeadPoisonBuff>(), 240);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Lead, 0f, 1f, 80, default, 1f);
                dust.noGravity = false;
            }
        }
    }

    /// <summary>银套 · 止血：受击后 4 秒内生命再生大幅提高</summary>
    internal class GsSilverArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SilverHelmet];
        public override int BodyID => ItemID.SilverChainmail;
        public override int LegsID => ItemID.SilverGreaves;
        protected override float MiningSpeed => 0.11f;
        protected override int SetDefense => 3;
        protected override string BodyLineFallback => "11% faster mining speed";
        protected override string SetBonusLineFallback => "3 more defense; for 4 seconds after taking damage, life regeneration is greatly increased";

        private const int StaunchFrames = 240;

        protected override void UpdateProperty(Player player, GodSmithArmorPlayer state) {
            if (TallyAge(player) >= (uint)StaunchFrames) {
                return;
            }
            player.lifeRegen += 6;
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.SilverCoin, 0f, -0.6f, 120, default, 0.8f);
                dust.noGravity = true;
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) => SetTally(player, 1);
    }

    /// <summary>钨套 · 钨芯：攻击击退 +30%、穿甲 +4</summary>
    internal class GsTungstenArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TungstenHelmet];
        public override int BodyID => ItemID.TungstenChainmail;
        public override int LegsID => ItemID.TungstenGreaves;
        protected override float MiningSpeed => 0.12f;
        protected override int SetDefense => 3;
        protected override string BodyLineFallback => "12% faster mining speed";
        protected override string SetBonusLineFallback => "3 more defense; 30% increased knockback and 4 armor penetration";

        protected override void UpdateProperty(Player player, GodSmithArmorPlayer state) {
            player.GetKnockback(DamageClass.Generic) += 0.30f;
            player.GetArmorPenetration(DamageClass.Generic) += 4f;
        }
    }

    /// <summary>金套 · 金币迸射：每 8 次命中向目标掷出 3 枚金币（各 = 本次命中 35%，类型跟随命中）</summary>
    internal class GsGoldArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.GoldHelmet, ItemID.AncientGoldHelmet];
        public override int BodyID => ItemID.GoldChainmail;
        public override int LegsID => ItemID.GoldGreaves;
        protected override float MiningSpeed => 0.14f;
        protected override int SetDefense => 4;
        protected override string BodyLineFallback => "14% faster mining speed";
        protected override string SetBonusLineFallback =>
            "4 more defense; every 8th hit flings 3 gold coins at the target, each dealing 35% of the hit";

        private const int HitsPerBurst = 8;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (target.type == NPCID.TargetDummy || !TallyUp(player, HitsPerBurst) || player.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = ProcDamage(damageDone, 0.35f, 3, 15);
            Vector2 toTarget = target.Center - player.Center;
            float dist = MathHelper.Clamp(toTarget.Length(), 60f, 600f);
            Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX * player.direction);
            for (int i = 0; i < 3; i++) {
                //三枚错角抛出，重力自然落向目标
                Vector2 velocity = dir * (9f + dist * 0.006f) - Vector2.UnitY * (2f + i * 1.2f);
                Projectile coin = SpawnProc(player, "GodSmithGoldEndow", player.Center, velocity,
                    ModContent.ProjectileType<GsGoldArmorCoinProj>(), damage, 2f);
                if (coin != null) {
                    coin.DamageType = hit.DamageType;
                }
            }
            SoundEngine.PlaySound(SoundID.Coins with { Volume = 0.6f }, player.Center);
        }
    }

    /// <summary>铂金套 · 惰性：免疫中毒、流血、虚弱与黑暗（贵金属不受腐蚀）</summary>
    internal class GsPlatinumArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PlatinumHelmet];
        public override int BodyID => ItemID.PlatinumChainmail;
        public override int LegsID => ItemID.PlatinumGreaves;
        protected override float MiningSpeed => 0.15f;
        protected override int SetDefense => 4;
        protected override string BodyLineFallback => "15% faster mining speed";
        protected override string SetBonusLineFallback => "4 more defense; immune to Poisoned, Bleeding, Weak and Darkness";

        protected override void UpdateProperty(Player player, GodSmithArmorPlayer state) {
            player.buffImmune[BuffID.Poisoned] = true;
            player.buffImmune[BuffID.Bleeding] = true;
            player.buffImmune[BuffID.Weak] = true;
            player.buffImmune[BuffID.Darkness] = true;
        }
    }

    /// <summary>
    /// 铜静电弧：出生在第二名敌人身上的一帧判定体，同时用电火花粒子在原目标（ai[0], ai[1]）与自身之间铺一道电弧
    /// </summary>
    internal class GsCopperArmorArcProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 4;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] != 0f || Main.dedServ) {
                return;
            }
            Projectile.localAI[0] = 1f;
            Vector2 from = new(Projectile.ai[0], Projectile.ai[1]);
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            //电弧：主线一串电火花，再抖两段侧枝
            DustLineJagged(from, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 0, default, 1f);
                spark.noGravity = true;
            }
        }

        private static void DustLineJagged(Vector2 from, Vector2 to) {
            int segments = Math.Max(3, (int)(from.Distance(to) / 24f));
            Vector2 prev = from;
            Vector2 normal = (to - from).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int i = 1; i <= segments; i++) {
                Vector2 next = Vector2.Lerp(from, to, i / (float)segments);
                if (i < segments) {
                    next += normal * Main.rand.NextFloat(-10f, 10f);
                }
                float length = prev.Distance(next);
                int count = Math.Max(2, (int)(length / 6f));
                for (int k = 0; k <= count; k++) {
                    Dust dust = Dust.NewDustPerfect(Vector2.Lerp(prev, next, k / (float)count), DustID.Electric, Vector2.Zero, 0, default, 0.8f);
                    dust.noGravity = true;
                }
                prev = next;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>锡鸣：以穿戴者为中心的一帧大判定区，伤害极低、击退很高；只用锡屑粒子与铃音</summary>
    internal class GsTinArmorChimeProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        public override void SetDefaults() {
            Projectile.width = 192;
            Projectile.height = 192;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] != 0f || Main.dedServ) {
                return;
            }
            Projectile.localAI[0] = 1f;
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 24; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 24f).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * 20f, DustID.Tin, dir * 5f, 0, default, 1.3f);
                dust.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //Boss 不吃震退，只受象征性的 2 点
            if (target.boss) {
                modifiers.Knockback *= 0f;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>金币弹：借原版金币贴图，抛物飞向目标、随速自转，命中一次即碎成金屑</summary>
    internal class GsGoldArmorCoinProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GoldCoin;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.28f, 14f);
            Projectile.rotation += 0.25f * Projectile.direction;
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 100, default, 0.9f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.5f, MaxInstances = 4 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f), 80, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
