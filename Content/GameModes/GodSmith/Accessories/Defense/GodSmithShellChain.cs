using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
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
            if (player.GetModPlayer<TidalMoonPlayer>().LunarGuardTimer > 0) {
                player.statDefense += 8;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (Main.dayTime || !state.TryUseCooldown(item.type, LunarGuardCD)) {
                return;
            }
            player.GetModPlayer<TidalMoonPlayer>().LunarGuardTimer = LunarGuardDuration;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
            }
        }
    }

    /// <summary>海神贝壳：入水掀潮涌护体，海是它的主场</summary>
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
                }
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

        /// <summary>昼夜双相打击的共用音效与挂 buff（天界石与天界贝壳共用）</summary>
        internal static void CelestialPhaseStrike(NPC target, bool day) {
            if (day) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
            else {
                target.AddBuff(BuffID.Frostburn, 240);
            }
            SoundEngine.PlaySound((day ? SoundID.Item34 : SoundID.Item30) with { Volume = 0.35f, Pitch = 0.3f },
                target.Center);
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
                }
            }
            if (tide.MythsTimer > 0) {
                player.lifeRegen += 4;
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
