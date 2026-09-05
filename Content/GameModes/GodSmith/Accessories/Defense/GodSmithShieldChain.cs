using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Defense
{
    /// <summary>
    /// 【盾链】铁镣与五面盾的受击哲学：铁镣=役魂定身（受击后免击退）、
    /// 钴/黑曜石/十字章盾多认领「坚壁回振」（受击回振甲窗，档位递进+档位专属附带）、
    /// 圣骑士盾=圣裁回掷（受击掷审判之锤）、英雄盾=血契（受击开吸取窗）、寒冰盾=寒晶护死（致死免死）。<br/>
    /// 受击钩子受击方本地权威；proc 弹 owner 侧生成；
    /// 每玩家状态在同文件私有 <see cref="BulwarkPlayer"/>
    /// </summary>
    internal class GodSmithShackle : GodSmithAccEffect
    {
        /// <summary>定身窗口帧数</summary>
        internal const int ResolveDuration = 240;

        /// <summary>触发冷却帧数</summary>
        private const int ResolveCD = 300;

        public override int[] TargetItemIDs => [ItemID.Shackle];

        protected override string EffectDescFallback =>
            "Soulbound Irons: taking a hit anchors you for 4s: immune to knockback and +4 defense\nTriggers once every 5s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<BulwarkPlayer>().ShackleTimer <= 0) {
                return;
            }
            player.noKnockback = true;
            player.statDefense += 4;
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, ResolveCD)) {
                return;
            }
            player.GetModPlayer<BulwarkPlayer>().ShackleTimer = ResolveDuration;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.5f, Pitch = -0.4f }, player.Center);
            }
        }
    }

    /// <summary>钴/黑曜石/十字章盾多认领：同一「坚壁回振」按链递进（+6/+8/+10 防），高档带专属附带</summary>
    internal class GodSmithCobaltShield : GodSmithAccEffect
    {
        /// <summary>回振甲窗帧数</summary>
        internal const int GuardDuration = 180;

        /// <summary>链共用触发冷却帧数</summary>
        private const int GuardCD = 240;

        public override int[] TargetItemIDs => [ItemID.CobaltShield, ItemID.ObsidianShield, ItemID.AnkhShield];

        protected override string EffectDescFallback =>
            "Bulwark Echo: taking a hit rings the shield, granting +6 / +8 / +10 defense (Cobalt / Obsidian / Ankh) for 3s\nObsidian's echo ignites nearby foes; Ankh's echo also cleanses up to 2 debuffs\nTriggers once every 4s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            BulwarkPlayer bulwark = player.GetModPlayer<BulwarkPlayer>();
            if (bulwark.GuardTimer > 0) {
                player.statDefense += bulwark.GuardBonus;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            //链共用冷却键：同佩多面盾只回振一次，档位取触发那件
            if (!state.TryUseCooldown(TargetItemIDs[0], GuardCD)) {
                return;
            }
            BulwarkPlayer bulwark = player.GetModPlayer<BulwarkPlayer>();
            bulwark.GuardTimer = GuardDuration;
            bulwark.GuardBonus = item.type == ItemID.CobaltShield ? 6 : item.type == ItemID.ObsidianShield ? 8 : 10;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
            }

            //黑曜石回振：震焰点燃近身敌人（受击方本地端可安全请求上 buff）
            if (item.type == ItemID.ObsidianShield) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.friendly && npc.Distance(player.Center) < 140f && npc.CanBeChasedBy()) {
                        npc.AddBuff(BuffID.OnFire, 180);
                    }
                }
            }
            //十字章回振：净化至多两个减益
            else if (item.type == ItemID.AnkhShield) {
                int cleansed = 0;
                for (int i = 0; i < Player.MaxBuffs && cleansed < 2; i++) {
                    int type = player.buffType[i];
                    if (type > 0 && Main.debuff[type] && player.buffTime[i] > 0
                        && !BuffID.Sets.NurseCannotRemoveDebuff[type]) {
                        player.DelBuff(i);
                        i--;
                        cleansed++;
                    }
                }
            }
        }
    }

    /// <summary>圣骑士盾：受击即审判，掷出回旋圣锤直取最近之敌，替队友挨的打也算数</summary>
    internal class GodSmithPaladinsShield : GodSmithAccEffect
    {
        /// <summary>圣裁冷却帧数</summary>
        private const int JudgementCD = 300;

        public override int[] TargetItemIDs => [ItemID.PaladinsShield];

        protected override string EffectDescFallback =>
            "Paladin's Retribution: taking a hit hurls a judging hammer at the nearest foe\nThe hammer deals 150% of the damage you took and pierces 3, once every 5s\nDamage absorbed for teammates counts too";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, JudgementCD)) {
                return;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = -0.1f }, player.Center);
            }
            //审判之锤 owner 侧生成（受击方本地端权威）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int hammerDamage = Math.Clamp((int)(info.Damage * 1.5f), 20, 350);
            NPC target = FindNearest(player);
            Vector2 vel = target != null
                ? (target.Center - player.Center).SafeNormalize(Vector2.UnitX) * 11f
                : new Vector2(player.direction * 11f, -2f);
            Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                ModContent.ProjectileType<GodSmithPaladinsShieldHammerProj>(), hammerDamage, 7f, player.whoAmI);
        }

        private static NPC FindNearest(Player player) {
            NPC best = null;
            float bestDist = 620f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = player.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }

    /// <summary>英雄盾：受击立血契，窗口内近战命中吸取生命，替人扛伤替己续命</summary>
    internal class GodSmithHeroShield : GodSmithAccEffect
    {
        /// <summary>血契窗口帧数</summary>
        internal const int PactDuration = 300;

        /// <summary>血契触发冷却</summary>
        private const int PactCD = 240;

        /// <summary>窗内吸取内置冷却</summary>
        private const int LeechICD = 15;

        /// <summary>副冷却键正键高位偏移（负键域归词缀神赋，约定 2026-08-27）</summary>
        private const int SecondaryCDKeyOffset = 10_000_000;

        public override int[] TargetItemIDs => [ItemID.HeroShield];

        protected override string EffectDescFallback =>
            "Hero's Pact: taking a hit seals a pact for 5s: +8 defense, and your melee hits drain 1 HP\nTriggers once every 4s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<BulwarkPlayer>().PactTimer > 0) {
                player.statDefense += 8;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, PactCD)) {
                return;
            }
            player.GetModPlayer<BulwarkPlayer>().PactTimer = PactDuration;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            //血契窗内近战吸取（命中钩子只在攻击方端跑）
            if (player.GetModPlayer<BulwarkPlayer>().PactTimer <= 0
                || !hit.DamageType.CountsAsClass(DamageClass.Melee)
                || !state.TryUseCooldown(item.type + SecondaryCDKeyOffset, LeechICD)) {
                return;
            }
            player.Heal(1);
        }
    }

    /// <summary>寒冰盾：致死一击冻结在寒晶里，免死回身，冰爆挫敌，长冷却压底线</summary>
    internal class GodSmithFrozenShield : GodSmithAccEffect
    {
        /// <summary>护死冷却帧数（90 秒）</summary>
        private const int WardCD = 5400;

        public override int[] TargetItemIDs => [ItemID.FrozenShield];

        protected override string EffectDescFallback =>
            "Cryo Ward: a killing blow instead freezes into the crystal: you survive at 25% max life,\nnearby foes are blasted with frostburn, and you gain brief immunity\nOnce every 90s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override bool PreKill(Item item, Player player, GodSmithPlayer state, double damage,
            int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (state.IsOnCooldown(item.type)) {
                return true;
            }
            state.SetCooldown(item.type, WardCD);
            //免死回身（护死在受击方本地端结算）
            player.statLife = Math.Max(1, player.statLifeMax2 / 4);
            player.HealEffect(player.statLife);
            player.immune = true;
            player.SetImmuneTimeForAllTypes(90);
            playSound = false;
            genGore = false;

            //寒晶爆发：碎裂声 + 近身挫伤
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = -0.4f }, player.Center);
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.friendly && npc.Distance(player.Center) < 200f && npc.CanBeChasedBy()) {
                    npc.AddBuff(BuffID.Frostburn, 300);
                }
            }
            return false;
        }
    }

    /// <summary>审判之锤：一柄自旋飞出的圣金战锤，追着最近的罪人去；命中敲出神圣钟音</summary>
    internal class GodSmithPaladinsShieldHammerProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PaladinsHammerFriendly;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            //轻追踪最近敌人，锤有意志但不魔法制导
            NPC target = FindTarget();
            if (target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.05f);
            }
            //锤体自旋
            Projectile.rotation += 0.35f;
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 500f;
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
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
        }
    }

    /// <summary>盾链私有状态载体：铁镣定身、坚壁回振、血契各窗口。受击方本地量，无需同步</summary>
    internal class BulwarkPlayer : ModPlayer
    {
        /// <summary>铁镣：定身窗口剩余帧数</summary>
        internal int ShackleTimer;

        /// <summary>坚壁回振：甲窗剩余帧数</summary>
        internal int GuardTimer;

        /// <summary>坚壁回振：本次甲量（触发那件的档位）</summary>
        internal int GuardBonus;

        /// <summary>英雄盾：血契窗口剩余帧数</summary>
        internal int PactTimer;

        public override void PostUpdateMiscEffects() {
            if (ShackleTimer > 0) {
                ShackleTimer--;
            }
            if (GuardTimer > 0 && --GuardTimer == 0) {
                GuardBonus = 0;
            }
            if (PactTimer > 0) {
                PactTimer--;
            }
        }

        public override void UpdateDead() {
            ShackleTimer = 0;
            GuardTimer = 0;
            GuardBonus = 0;
            PactTimer = 0;
        }
    }
}
