using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Defense
{
    /// <summary>
    /// 【专家品】三件专家掉落的怪东西：蠕虫围巾=共生甲壳（受压越狠甲越厚）、
    /// 混乱之脑=神经错乱波（受击扩散致乱+过载）、克苏鲁之盾=瞳溃冲撞（冲刺撞击引爆瞳震）。<br/>
    /// 冲刺检测走 owner 端 <see cref="Player.eocDash"/>/<see cref="Player.eocHit"/>（本地模拟量）；
    /// 每玩家状态在同文件私有 <see cref="ExpertRelicPlayer"/>
    /// </summary>
    internal class GodSmithWormScarf : GodSmithAccEffect
    {
        /// <summary>甲壳叠层上限</summary>
        internal const int MaxPlates = 3;

        /// <summary>甲壳持续帧数（受击刷新）</summary>
        internal const int PlateDuration = 480;

        public override int[] TargetItemIDs => [ItemID.WormScarf];

        protected override string EffectDescFallback =>
            "Symbiotic Carapace: each hit taken grows a carapace plate: -4% damage taken per plate, up to 3 plates (8s)\nThe scarf coils tighter the harder you are pressed";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void ModifyHurt(Item item, Player player, GodSmithPlayer state, ref Player.HurtModifiers modifiers) {
            ExpertRelicPlayer relic = player.GetModPlayer<ExpertRelicPlayer>();
            if (relic.CarapacePlates > 0) {
                modifiers.FinalDamage *= 1f - 0.04f * relic.CarapacePlates;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info)
            => player.GetModPlayer<ExpertRelicPlayer>().GrowCarapace();
    }

    /// <summary>混乱之脑：受击炸开神经错乱波，乱敌心智，反把痛觉拧成过载输出</summary>
    internal class GodSmithBrainOfConfusion : GodSmithAccEffect
    {
        /// <summary>错乱波冷却帧数</summary>
        private const int WaveCD = 240;

        /// <summary>过载窗口帧数</summary>
        internal const int OverloadDuration = 180;

        public override int[] TargetItemIDs => [ItemID.BrainOfConfusion];

        protected override string EffectDescFallback =>
            "Neural Shockwave: taking a hit bursts a psychic wave: nearby foes are confused for 3s\nand your own mind overclocks: +8% damage for 3s. Triggers once every 4s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<ExpertRelicPlayer>().OverloadTimer > 0) {
                player.GetDamage(DamageClass.Generic) += 0.08f;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, WaveCD)) {
                return;
            }
            player.GetModPlayer<ExpertRelicPlayer>().OverloadTimer = OverloadDuration;
            //乱心波及近敌（受击方本地端可安全请求上 buff）
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.friendly && npc.Distance(player.Center) < 180f && npc.CanBeChasedBy()) {
                    npc.AddBuff(BuffID.Confused, 180);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
            }
        }
    }

    /// <summary>克苏鲁之盾：冲刺撞上敌人时引爆瞳震，冲撞不再只是位移，是宣言</summary>
    internal class GodSmithShieldofCthulhu : GodSmithAccEffect
    {
        /// <summary>瞳震冷却帧数</summary>
        private const int RuptureCD = 90;

        public override int[] TargetItemIDs => [ItemID.EoCShield];

        protected override string EffectDescFallback =>
            "Pupil Rupture: bashing a foe with your dash ruptures a crimson shockwave around it\ndealing 25 damage with heavy knockback, once every 1.5s\nDashing trails a crimson gaze";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void PostUpdateEquips(Item item, Player player, GodSmithPlayer state) {
            //eocDash/eocHit 是 owner 端本地模拟量，远端副本不可靠，整段只在 owner 端跑
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            ExpertRelicPlayer relic = player.GetModPlayer<ExpertRelicPlayer>();

            //冲撞沿：eocHit 从无到有的那一帧结算瞳震
            int bashTarget = player.eocHit;
            bool bashed = bashTarget >= 0 && relic.PrevEocHit < 0;
            relic.PrevEocHit = bashTarget;
            if (!bashed || bashTarget >= Main.maxNPCs || !state.TryUseCooldown(item.type, RuptureCD)) {
                return;
            }
            NPC target = Main.npc[bashTarget];
            if (!target.active) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.55f, Pitch = -0.3f }, target.Center);
            Projectile.NewProjectile(player.GetSource_Accessory(item), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GodSmithShieldofCthulhuBurstProj>(), 25, 8f, player.whoAmI);
        }
    }

    /// <summary>
    /// 瞳溃冲击：一记自撞点炸开的猩红瞳震；
    /// 绘制只有一笔按半径缩放的原版气泡贴图给出范围提示
    /// </summary>
    internal class GodSmithShieldofCthulhuBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private ref float Life => ref Projectile.ai[0];

        private const int LifeMax = 14;

        private const int Radius = 120;

        public override void SetDefaults() {
            Projectile.width = Radius * 2;
            Projectile.height = Radius * 2;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeMax;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            if (Life > 6f) {
                Projectile.friendly = false;
            }
        }

        /// <summary>区域弹一笔：原版气泡贴图按判定直径缩放画在中心，随生命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float fade = 1f - MathHelper.Clamp(Life / LifeMax, 0f, 1f);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, Radius * 2f / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>专家品私有状态载体：共生甲壳、过载窗、冲撞沿记忆。本地量，无需同步</summary>
    internal class ExpertRelicPlayer : ModPlayer
    {
        /// <summary>蠕虫围巾：甲壳层数</summary>
        internal int CarapacePlates { get; private set; }

        private int carapaceTimer;

        /// <summary>混乱之脑：过载窗剩余帧数</summary>
        internal int OverloadTimer;

        /// <summary>克苏鲁之盾：上一帧的 eocHit（冲撞沿检测）</summary>
        internal int PrevEocHit = -1;

        internal void GrowCarapace() {
            CarapacePlates = Math.Min(CarapacePlates + 1, GodSmithWormScarf.MaxPlates);
            carapaceTimer = GodSmithWormScarf.PlateDuration;
        }

        public override void PostUpdateMiscEffects() {
            if (carapaceTimer > 0 && --carapaceTimer == 0) {
                CarapacePlates = 0;
            }
            if (OverloadTimer > 0) {
                OverloadTimer--;
            }
        }

        public override void UpdateDead() {
            CarapacePlates = 0;
            carapaceTimer = 0;
            OverloadTimer = 0;
            PrevEocHit = -1;
        }
    }
}
