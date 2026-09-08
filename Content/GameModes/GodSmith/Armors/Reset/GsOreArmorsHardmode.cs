using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 肉后六套矿石甲的公共层：三顶头盔的原版单件属性与**按头盔分职业的原版套装奖励全部放行**
    /// （KeepsVanillaSetBonus），本族只在其上叠一条金属物性；proc 伤害类型跟随触发它的那次命中。
    /// 头盔同样可镶嵌低一档头盔（钴/钯金→秘银/山铜→精金/钛金），整套穿好后继承其物性。<br/>
    /// 原版旗标清点：三职业套装奖励 → 原版 UpdateArmorSets 照常执行；无删除项
    /// </summary>
    internal abstract class GsHardmodeOreArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;

        public sealed override bool KeepsVanillaSetBonus => true;
    }

    /// <summary>钴套 · 疾影：5 秒未受击进入疾影态（移速 +8%），疾影态下每 5 次命中从身侧甩出一道残像剑气</summary>
    internal class GsCobaltArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CobaltHelmet, ItemID.CobaltHat, ItemID.CobaltMask];
        public override int BodyID => ItemID.CobaltBreastplate;
        public override int LegsID => ItemID.CobaltLeggings;
        protected override string SetBonusLineFallback =>
            "Your helmet's set bonus works as before; 5 seconds without taking damage puts you in Swift Shadow: 8% increased movement speed, and every 5th hit lets an afterimage lash a cobalt blade at the target for 50% of the hit";

        private const int ShadowDelay = 300;
        private const int HitsPerBlade = 5;

        private bool InShadow(Player player) => MarkAge(player) >= (uint)ShadowDelay;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            if (InShadow(player)) {
                player.moveSpeed += 0.08f;
            }
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (Main.dedServ || !InShadow(player) || player.velocity.Length() < 2f || !Main.rand.NextBool(3)) {
                return;
            }
            Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Cobalt,
                -player.velocity.X * 0.3f, -player.velocity.Y * 0.3f, 120, default, 1f);
            dust.noGravity = true;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) => MarkNow(player);

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (!InShadow(player) || target.type == NPCID.TargetDummy || !TallyUp(player, HitsPerBlade)) {
                return;
            }
            Vector2 from = player.Center + new Vector2(-player.direction * 26f, -6f);
            Vector2 velocity = (target.Center - from).SafeNormalize(Vector2.UnitX * player.direction) * 16f;
            Projectile blade = SpawnProc(player, "GodSmithCobaltEndow", from, velocity,
                ModContent.ProjectileType<GsCobaltArmorBeamProj>(), ProcDamage(damageDone, 0.5f, 8, 40), 3f, target.whoAmI);
            if (blade != null) {
                blade.DamageType = hit.DamageType;
            }
        }
    }

    /// <summary>钯金套 · 虹吸：原版命中回血照旧；同一敌人连续命中 6 次放出寄生光珠，咬中后返回治疗 8</summary>
    internal class GsPalladiumArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PalladiumHelmet, ItemID.PalladiumHeadgear, ItemID.PalladiumMask];
        public override int BodyID => ItemID.PalladiumBreastplate;
        public override int LegsID => ItemID.PalladiumLeggings;
        protected override string SetBonusLineFallback =>
            "Hitting enemies still grants rapid life regeneration; hitting the same enemy 6 times in a row releases a siphon orb that bites it and flies back to heal you for 8";

        internal const int OrbHeal = 8;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || target.type == NPCID.TargetDummy || target.life <= 0
                || !target.GetGlobalNPC<GsArmorMarkNPC>().SiphonUp(6, 120)) {
                return;
            }
            Vector2 velocity = (target.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction).RotatedByRandom(0.5f) * 8f;
            Projectile orb = SpawnProc(player, "GodSmithPalladiumEndow", player.Center, velocity,
                ModContent.ProjectileType<GsPalladiumArmorOrbProj>(), ProcDamage(damageDone, 0.3f, 6, 30), 1f, target.whoAmI);
            if (orb != null) {
                orb.DamageType = hit.DamageType;
            }
        }
    }

    /// <summary>秘银套 · 剑庐：每 8 次命中在肩后铸出一柄秘银灵剑，悬停片刻后刺向目标</summary>
    internal class GsMythrilArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.MythrilHelmet, ItemID.MythrilHood, ItemID.MythrilHat];
        public override int BodyID => ItemID.MythrilChainmail;
        public override int LegsID => ItemID.MythrilGreaves;
        protected override string SetBonusLineFallback =>
            "Your helmet's set bonus works as before; every 8th hit forges a mythril spirit blade over your shoulder that lunges at the target for 60% of the hit";

        private const int HitsPerBlade = 8;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (target.type == NPCID.TargetDummy || !TallyUp(player, HitsPerBlade)) {
                return;
            }
            Vector2 shoulder = player.Center + new Vector2(-player.direction * 20f, -26f);
            Projectile blade = SpawnProc(player, "GodSmithMythrilEndow", shoulder, Vector2.Zero,
                ModContent.ProjectileType<GsMythrilArmorSwordProj>(), ProcDamage(damageDone, 0.6f, 10, 60), 4f, target.whoAmI);
            if (blade != null) {
                blade.DamageType = hit.DamageType;
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = 0.3f, MaxInstances = 3 }, shoulder);
            }
        }
    }

    /// <summary>山铜套 · 乱舞：原版花瓣照旧；同一敌人每被花瓣命中 5 次，第 5 片分裂出两片追踪花瓣</summary>
    internal class GsOrichalcumArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.OrichalcumHelmet, ItemID.OrichalcumHeadgear, ItemID.OrichalcumMask];
        public override int BodyID => ItemID.OrichalcumBreastplate;
        public override int LegsID => ItemID.OrichalcumLeggings;
        protected override string SetBonusLineFallback =>
            "Flower petals still fall on hit; every 5th petal to strike the same enemy splits into two extra homing petals";

        private const int PetalsPerSplit = 5;

        /// <summary>真实命中取样的有效期，过期就退回花瓣自身伤害</summary>
        private const int SampleFrames = 180;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            if (sourceProj == null || sourceProj.type != ProjectileID.FlowerPetal) {
                //原版花瓣的伤害是写死的 36，不随武器成长，拿它当分裂瓣基准会前期超模、后期归零，
                //所以非花瓣的命中只用来记下最近一次真实命中伤害（族内 proc 已被 IGsArmorProc 挡在钩子外）
                SetTally(player, damageDone);
                return;
            }
            GsArmorMarkNPC mark = target.GetGlobalNPC<GsArmorMarkNPC>();
            if (++mark.PetalHits < PetalsPerSplit) {
                return;
            }
            mark.PetalHits = 0;
            int reference = TallyStale(player, SampleFrames) ? damageDone : Math.Max(damageDone, Tally(player));
            //预算账：约 9 次武器命中出一次分裂（原版花瓣 20 帧一片，还要落中 5 片），
            //两瓣各穿三次实取约 5 段，故期望增量 0.11 × 5 × 0.2 ≈ +11%，落在法四的 +12% 以内
            int petalDamage = ProcDamage(reference, 0.2f, 5, 120);
            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 9f);
                Projectile petal = SpawnProc(player, "GodSmithOrichalcumEndow", target.Center + velocity * 3f, velocity,
                    ModContent.ProjectileType<GsOrichalcumPetalProj>(), petalDamage, 2f);
                if (petal != null) {
                    petal.DamageType = hit.DamageType;
                }
            }
        }
    }

    /// <summary>精金套 · 炉温：命中积热（12 层，2 秒不打开始衰退），满层下一击从目标身上炸出六颗精金余烬</summary>
    internal class GsAdamantiteArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.AdamantiteHelmet, ItemID.AdamantiteHeadgear, ItemID.AdamantiteMask];
        public override int BodyID => ItemID.AdamantiteBreastplate;
        public override int LegsID => ItemID.AdamantiteLeggings;
        protected override string SetBonusLineFallback =>
            "Your helmet's set bonus works as before; hits build forge heat (12 stacks, fading after 2 seconds idle), and at full heat the next hit bursts six adamantite embers out of the target for 35% of the hit";

        private const int FullHeat = 12;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (TallyStale(player, 120)) {
                SetTally(player, 0);
                return;
            }
            int heat = Tally(player);
            if (Main.dedServ || heat <= 0 || Main.rand.Next(FullHeat + 2) >= heat) {
                return;
            }
            //炉温读数：层数越高身周红热尘越密
            Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Adamantite, 0f, -0.8f, 120, default, 0.9f);
            dust.noGravity = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (target.type == NPCID.TargetDummy || !TallyUp(player, FullHeat)) {
                return;
            }
            int damage = ProcDamage(damageDone, 0.35f, 8, 40);
            for (int i = 0; i < 6; i++) {
                Vector2 velocity = (MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.2f, 0.2f)).ToRotationVector2() * Main.rand.NextFloat(6f, 8f);
                Projectile ember = SpawnProc(player, "GodSmithAdamantiteEndow", target.Center, velocity,
                    ModContent.ProjectileType<GsAdamantiteArmorEmberProj>(), damage, 3f);
                if (ember != null) {
                    ember.DamageType = hit.DamageType;
                }
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 钛金套：击中敌人获得钛金屏障（原版机制），钛金碎片环绕守护；碎片越多手持武器职业的暴击最多 +15%、伤害最多 +10%，
    /// 受伤后屏障与碎片立即消散
    /// </summary>
    internal class GsTitaniumArmor : GsHardmodeOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TitaniumHelmet, ItemID.TitaniumHeadgear, ItemID.TitaniumMask];
        public override int BodyID => ItemID.TitaniumBreastplate;
        public override int LegsID => ItemID.TitaniumLeggings;
        protected override string SetBonusLineFallback =>
            "Hitting an enemy still raises the Titanium Barrier; the more shards orbit you, the more your held weapon gains, up to 15% critical strike chance and 10% damage, but the barrier shatters the moment you take damage";

        /// <summary>原版屏障的碎片上限</summary>
        private const int MaxShards = 7;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            int shards = Math.Min(MaxShards, player.ownedProjectileCounts[ProjectileID.TitaniumStormShard]);
            if (shards <= 0) {
                return;
            }
            Item held = player.HeldItem;
            DamageClass heldClass = held != null && !held.IsAir && held.damage > 0 ? held.DamageType : DamageClass.Generic;
            float t = shards / (float)MaxShards;
            player.GetCritChance(heldClass) += 15f * t;
            player.GetDamage(heldClass) += 0.10f * t;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            player.ClearBuff(BuffID.TitaniumStorm);
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == ProjectileID.TitaniumStormShard) {
                    proj.Kill();
                }
            }
        }
    }

    /// <summary>钴残像剑气：借附魔剑气贴图，自身侧甩向锁定目标（ai[0]）并轻微追踪；拖钴蓝尘</summary>
    internal class GsCobaltArmorBeamProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EnchantedBeam;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            int index = (int)TargetIndex;
            if (index >= 0 && index < Main.maxNPCs) {
                NPC target = Main.npc[index];
                if (target.active && !target.friendly) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 16f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.1f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.7f);
            if (Main.dedServ) {
                return;
            }
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Cobalt,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.1f);
            dust.noGravity = true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Cobalt,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 钯金寄生光珠：借吸血刀回血光点贴图染钯金橙，去程追锁定目标（ai[0]），咬中后回航（ai[1] = 1）治疗主人
    /// </summary>
    internal class GsPalladiumArmorOrbProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireHeal;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Returning => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 150, 70, 200);

        public override bool? CanDamage() => Returning == 0f;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (Returning == 0f) {
                NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
                if (target == null || !target.active || target.friendly) {
                    Returning = 1f;
                    Projectile.netUpdate = true;
                }
                else {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
                }
            }
            if (Returning == 1f) {
                if (!owner.active || owner.dead) {
                    Projectile.Kill();
                    return;
                }
                Vector2 toOwner = owner.Center - Projectile.Center;
                if (toOwner.Length() < 24f) {
                    if (Projectile.owner == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                        owner.Heal(GsPalladiumArmor.OrbHeal);
                    }
                    Projectile.Kill();
                    return;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.UnitX) * 15f, 0.15f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.6f, 0.35f, 0.1f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Palladium, 0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Returning = 1f;
            Projectile.netUpdate = true;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
        }
    }

    /// <summary>
    /// 秘银灵剑：借剑气贴图，先在主人肩后悬停 14 帧（剑尖指向目标），再刺向锁定目标（ai[0]），可穿透两次
    /// </summary>
    internal class GsMythrilArmorSwordProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SwordBeam;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        private const int HoverFrames = 14;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = HoverFrames + 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => Life >= HoverFrames;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            bool hasTarget = target != null && target.active && !target.friendly;
            if (Life <= HoverFrames) {
                //悬停在肩后随主人移动，剑尖对准目标
                Projectile.Center = owner.Center + new Vector2(-owner.direction * 20f, -26f - Life * 0.4f);
                Vector2 aim = hasTarget ? target.Center - Projectile.Center : Vector2.UnitX * owner.direction;
                Projectile.rotation = aim.ToRotation() + MathHelper.PiOver4;
                Projectile.velocity = Vector2.Zero;
                if ((int)Life == HoverFrames) {
                    Projectile.velocity = aim.SafeNormalize(Vector2.UnitX) * 18f;
                    Projectile.netUpdate = true;
                }
            }
            else if (hasTarget) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 18f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.15f);
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.5f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Mythril, 0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Mythril,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 山铜分裂花瓣：借原版花瓣贴图（3 帧），出手短暂散开后咬向最近敌人，可穿透两次；消散只用原版粉色花瓣粒子
    /// </summary>
    internal class GsOrichalcumPetalProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>散开段帧数，之后开始追踪</summary>
        private const int ScatterFrames = 8;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.FlowerPetal]);
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.06f, 0.18f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }
            Projectile.rotation += 0.25f;
            if (++Projectile.frameCounter >= 6) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy,
                    0f, 0f, 100, default, 0.9f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 600f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }

    /// <summary>精金余烬：借火球贴图染精金红，自目标向外抛出后受重力落回，命中点燃；出生 6 帧内免地形碰撞</summary>
    internal class GsAdamantiteArmorEmberProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Fireball;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 110, 90, 180);

        public override void AI() {
            Life++;
            if (Life > 6f) {
                Projectile.tileCollide = true;
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.2f, 12f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.7f, 0.25f, 0.2f);
            if (Main.dedServ) {
                return;
            }
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Adamantite,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.1f);
            dust.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Adamantite,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
