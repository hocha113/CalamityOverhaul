using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Defense
{
    /// <summary>
    /// 【贝壳链】月与海的环境应答：月光护身符=狼嚎月狩（夜战撕咬回血）、
    /// 月亮石=月相守御（夜间受击月盾）、海神贝壳=深海潮涌（入水潮甲）、
    /// 天界石=昼夜双相打击、天界贝壳=万相天体（三态合一）；外加秘药护身符=药力回环。<br/>
    /// 环境沿（入水/昼夜）在同文件私有 <see cref="TidalMoonPlayer"/> 逐帧检测
    /// </summary>
    internal class GodSmithMoonCharm : GodSmithAccEffect
    {
        /// <summary>撕咬冷却帧数</summary>
        private const int BiteCD = 30;

        public override int[] TargetItemIDs => [ItemID.MoonCharm];

        protected override string EffectDescFallback =>
            "Lunar Hunt: at night your melee bites drain 2 HP from the prey, once every 0.5s\nEach bite gleams with moon-silver sparks";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (Main.dayTime || !hit.DamageType.CountsAsClass(DamageClass.Melee)
                || !state.TryUseCooldown(item.type, BiteCD)) {
                return;
            }
            player.Heal(2);
            //月银撕咬痕（命中钩子只在攻击方端跑）
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    hit.HitDirection * new Vector2(Main.rand.NextFloat(1.5f, 4f), 0f).RotatedByRandom(0.5f),
                    Main.rand.NextBool() ? new Color(200, 210, 235) : new Color(150, 165, 210),
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(false, Main.rand.Next(12, 18));
            }
        }
    }

    /// <summary>月亮石：夜间受击垂落月盾，月相站在挨打者这边</summary>
    internal class GodSmithMoonStone : GodSmithAccEffect
    {
        /// <summary>月盾窗口帧数</summary>
        internal const int LunarGuardDuration = 240;

        /// <summary>月盾冷却帧数</summary>
        private const int LunarGuardCD = 240;

        public override int[] TargetItemIDs => [ItemID.MoonStone];

        protected override string EffectDescFallback =>
            "Lunar Aegis: at night, taking a hit veils you in moonlight for 4s: +8 defense\nTriggers once every 4s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            TidalMoonPlayer tide = player.GetModPlayer<TidalMoonPlayer>();
            if (tide.LunarGuardTimer <= 0) {
                return;
            }
            player.statDefense += 8;
            //月纱垂落（个人读数）
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -24f),
                    new Vector2(0f, Main.rand.NextFloat(0.4f, 1f)), new Color(170, 185, 230),
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(16, 0.8f);
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (Main.dayTime || !state.TryUseCooldown(item.type, LunarGuardCD)) {
                return;
            }
            player.GetModPlayer<TidalMoonPlayer>().LunarGuardTimer = LunarGuardDuration;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                new Color(170, 185, 230), 0.05f)?.Configure(0.07f, 0.4f, 16);
        }
    }

    /// <summary>海神贝壳：入水掀潮涌护体，水中命中带潮沫，海是它的主场</summary>
    internal class GodSmithNeptunesShell : GodSmithAccEffect
    {
        /// <summary>潮涌窗口帧数</summary>
        internal const int TideDuration = 300;

        /// <summary>潮涌冷却帧数</summary>
        private const int TideCD = 300;

        /// <summary>副冷却键正键高位偏移（负键域归词缀神赋，约定 2026-08-27）</summary>
        internal const int SecondaryCDKeyOffset = 10_000_000;

        public override int[] TargetItemIDs => [ItemID.NeptunesShell];

        protected override string EffectDescFallback =>
            "Tidal Surge: plunging into water raises a tide guard for 5s: +6 defense, once every 5s\nWhile wet your strikes splash with sea foam";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            TidalMoonPlayer tide = player.GetModPlayer<TidalMoonPlayer>();
            //入水沿触发潮涌
            if (tide.WetPulse && state.TryUseCooldown(item.type, TideCD)) {
                tide.RaiseTide(6, TideDuration);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = 0.2f }, player.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                        new Color(90, 190, 220), 0.05f)?.Configure(0.07f, 0.42f, 16);
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(14f, 18f),
                            DustID.BubbleBurst_Blue, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f));
                        dust.noGravity = true;
                    }
                }
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            //湿身命中带潮沫（节流，走高位副键），Default 类不触发防自喂
            if (!player.wet || hit.DamageType == DamageClass.Default
                || !state.TryUseCooldown(item.type + SecondaryCDKeyOffset, 12)) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    Main.rand.NextBool() ? new Color(90, 190, 220) : new Color(200, 240, 250),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }
    }

    /// <summary>天界石：昼灼夜霜的双相打击，环境换弹种</summary>
    internal class GodSmithCelestialStone : GodSmithAccEffect
    {
        /// <summary>双相打击冷却帧数</summary>
        private const int PhaseCD = 120;

        public override int[] TargetItemIDs => [ItemID.CelestialStone];

        protected override string EffectDescFallback =>
            "Day and Night Phases: by day your strikes ignite foes with hellfire; by night they sear with frostburn\nTriggers once every 2s, flaring sun-gold or moon-frost";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (hit.DamageType == DamageClass.Default || !state.TryUseCooldown(item.type, PhaseCD)) {
                return;
            }
            CelestialPhaseStrike(target, Main.dayTime);
        }

        /// <summary>昼夜双相打击的共用演出与挂 buff（天界石与天界贝壳共用）</summary>
        internal static void CelestialPhaseStrike(NPC target, bool day) {
            if (day) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
            else {
                target.AddBuff(BuffID.Frostburn, 240);
            }
            Color main = day ? new Color(255, 170, 40) : new Color(140, 210, 255);
            Color soft = day ? new Color(255, 230, 150) : new Color(220, 245, 255);
            SoundEngine.PlaySound((day ? SoundID.Item34 : SoundID.Item30) with { Volume = 0.35f, Pitch = 0.3f },
                target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? main : soft,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }
    }

    /// <summary>天界贝壳：万相天体，昼灼夜霜更频密，入水再掀潮涌，三态集大成</summary>
    internal class GodSmithCelestialShell : GodSmithAccEffect
    {
        /// <summary>双相打击冷却帧数（比天界石更密）</summary>
        private const int PhaseCD = 90;

        /// <summary>潮涌冷却帧数（负键，与双相分开）</summary>
        private const int TideCD = 300;

        public override int[] TargetItemIDs => [ItemID.CelestialShell];

        protected override string EffectDescFallback =>
            "Total Celestial: by day strikes ignite, by night they frostburn, once every 1.5s\nPlunging into water raises a tide guard for 5s: +8 defense, once every 5s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            TidalMoonPlayer tide = player.GetModPlayer<TidalMoonPlayer>();
            //潮涌走高位副键冷却，与双相打击互不占用
            if (tide.WetPulse && state.TryUseCooldown(item.type + GodSmithNeptunesShell.SecondaryCDKeyOffset, TideCD)) {
                tide.RaiseTide(8, GodSmithNeptunesShell.TideDuration);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = 0.3f }, player.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                        new Color(120, 210, 230), 0.05f)?.Configure(0.08f, 0.48f, 18);
                }
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (hit.DamageType == DamageClass.Default || !state.TryUseCooldown(item.type, PhaseCD)) {
                return;
            }
            GodSmithCelestialStone.CelestialPhaseStrike(target, Main.dayTime);
        }
    }

    /// <summary>秘药护身符：饮下治疗药水延展成药力回环，一口药回两段血</summary>
    internal class GodSmithCharmofMyths : GodSmithAccEffect
    {
        /// <summary>药力回环窗口帧数（8 秒）</summary>
        internal const int MythsDuration = 480;

        public override int[] TargetItemIDs => [ItemID.CharmofMyths];

        protected override string EffectDescFallback =>
            "Mythic Afterglow: drinking a healing potion leaves an afterglow for 8s: +2 HP/s regeneration\nThe glow swirls emerald while it mends";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            TidalMoonPlayer tide = player.GetModPlayer<TidalMoonPlayer>();
            //饮药沿：开启回环
            if (tide.PotionPulse) {
                tide.MythsTimer = MythsDuration;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.5f }, player.Center);
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(player.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                            Main.rand.NextBool() ? new Color(90, 220, 130) : new Color(190, 255, 200),
                            Main.rand.NextFloat(0.26f, 0.42f))?.Configure(false, Main.rand.Next(16, 26)); 
                    }
                }
            }
            if (tide.MythsTimer <= 0) {
                return;
            }
            player.lifeRegen += 4;
            //翠愈微旋（个人读数）
            if (!VaultUtils.isServer && Main.rand.NextBool(10)) {
                float angle = Main.GameUpdateCount * 0.06f;
                PRTLoader.NewParticle<PRT_Light>(player.Center + angle.ToRotationVector2() * 22f,
                    new Vector2(0f, -0.5f), new Color(90, 220, 130),
                    Main.rand.NextFloat(0.05f, 0.08f))?.Configure(12, 0.8f);
            }
        }
    }

    /// <summary>贝壳链私有状态载体：入水沿、饮药沿、月盾/潮涌/回环各窗口。本地量，无需同步</summary>
    internal class TidalMoonPlayer : ModPlayer
    {
        /// <summary>月亮石：月盾窗剩余帧数</summary>
        internal int LunarGuardTimer;

        /// <summary>潮涌窗剩余帧数（海神贝壳与天界贝壳共用，档位取大）</summary>
        internal int TideTimer { get; private set; }

        /// <summary>本次潮涌甲量</summary>
        internal int TideBonus { get; private set; }

        /// <summary>秘药护身符：药力回环窗剩余帧数</summary>
        internal int MythsTimer;

        /// <summary>本帧刚入水（湿身上升沿）</summary>
        internal bool WetPulse { get; private set; }

        /// <summary>本帧刚饮下治疗药水（potionDelay 上升沿）</summary>
        internal bool PotionPulse { get; private set; }

        private bool prevWet;

        private int prevPotionDelay;

        internal void RaiseTide(int bonus, int duration) {
            TideBonus = Math.Max(TideBonus, bonus);
            TideTimer = Math.Max(TideTimer, duration);
        }

        public override void UpdateEquips() {
            //潮涌甲在装备结算期统一发放
            if (TideTimer > 0) {
                Player.statDefense += TideBonus;
            }
        }

        public override void PostUpdateMiscEffects() {
            WetPulse = Player.wet && !prevWet;
            prevWet = Player.wet;
            PotionPulse = Player.potionDelay > 0 && prevPotionDelay == 0;
            prevPotionDelay = Player.potionDelay;

            if (LunarGuardTimer > 0) {
                LunarGuardTimer--;
            }
            if (TideTimer > 0 && --TideTimer == 0) {
                TideBonus = 0;
            }
            if (MythsTimer > 0) {
                MythsTimer--;
            }
        }

        public override void UpdateDead() {
            LunarGuardTimer = 0;
            TideTimer = 0;
            TideBonus = 0;
            MythsTimer = 0;
            WetPulse = false;
            PotionPulse = false;
        }
    }
}
