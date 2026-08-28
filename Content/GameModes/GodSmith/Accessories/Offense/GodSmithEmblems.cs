using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【徽章族】七枚战意烙印，每枚一个职业化触发：战士=连击战意、游侠=远距专注、
    /// 法师=秘能回涌、召唤=军团号令、复仇者=受击反烙、毁灭者=处决脉冲、巨像之眼=会心凝视。<br/>
    /// 全部触发按 hit.DamageType 过滤（支援弹为 DamageClass.Default，天然防自喂）；
    /// 每玩家状态集中在同文件私有 <see cref="EmblemWarPlayer"/>
    /// </summary>
    internal class GodSmithWarriorEmblem : GodSmithAccEffect
    {
        /// <summary>战意叠层上限</summary>
        internal const int MaxStacks = 6;

        /// <summary>战意持续帧数（命中刷新）</summary>
        internal const int WarDuration = 240;

        /// <summary>叠层内置冷却</summary>
        private const int StackICD = 8;

        public override int[] TargetItemIDs => [ItemID.WarriorEmblem];

        protected override string EffectDescFallback =>
            "War Momentum: melee hits build fervor, +1.5% melee damage per stack, up to 6 stacks (4s)\nAt full stacks strikes flare gold; taking a hit sheds 3 stacks";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            EmblemWarPlayer war = player.GetModPlayer<EmblemWarPlayer>();
            if (war.WarStacks > 0) {
                player.GetDamage(DamageClass.Melee) += 0.015f * war.WarStacks;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Melee)) {
                return;
            }
            EmblemWarPlayer war = player.GetModPlayer<EmblemWarPlayer>();
            if (state.TryUseCooldown(item.type, StackICD)) {
                war.AddWarStack();
            }
            //满层战意：金红怒焰迸溅（命中钩子只在攻击方端跑）
            if (war.WarStacks >= MaxStacks) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                        Main.rand.NextBool() ? new Color(255, 190, 60) : new Color(230, 80, 40),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info)
            => player.GetModPlayer<EmblemWarPlayer>().ShedWarStacks(3);
    }

    /// <summary>游侠徽章：远距离（25 格外）命中触发专注窗口，奖励拉开身位的射手</summary>
    internal class GodSmithRangerEmblem : GodSmithAccEffect
    {
        /// <summary>触发距离（像素，25 格）</summary>
        private const float FocusRange = 400f;

        /// <summary>专注窗口帧数</summary>
        internal const int FocusDuration = 240;

        public override int[] TargetItemIDs => [ItemID.RangerEmblem];

        protected override string EffectDescFallback =>
            "Long Hunt: ranged hits from over 25 tiles away grant Focus for 4s: +8% ranged damage\nFocused hits gleam with silver tracer sparks";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<EmblemWarPlayer>().FocusTimer > 0) {
                player.GetDamage(DamageClass.Ranged) += 0.08f;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged)) {
                return;
            }
            EmblemWarPlayer war = player.GetModPlayer<EmblemWarPlayer>();
            if (player.Distance(target.Center) >= FocusRange) {
                war.FocusTimer = FocusDuration;
            }
            //专注态命中带银白曳光（节流防糊屏）
            if (war.FocusTimer > 0 && state.TryUseCooldown(item.type, 12)) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                        new Color(220, 230, 240), Main.rand.NextFloat(0.26f, 0.42f))
                        ?.Configure(false, Main.rand.Next(10, 16));
                }
            }
        }
    }

    /// <summary>法师徽章：施法回涌，低蓝时回涌翻倍，续航型触发</summary>
    internal class GodSmithSorcererEmblem : GodSmithAccEffect
    {
        /// <summary>回涌冷却帧数</summary>
        private const int SurgeCD = 45;

        public override int[] TargetItemIDs => [ItemID.SorcererEmblem];

        protected override string EffectDescFallback =>
            "Arcane Resurge: magic hits restore 6 mana, or 12 when below 30% mana\nTriggers once every 0.75s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Magic) || !state.TryUseCooldown(item.type, SurgeCD)) {
                return;
            }
            int amount = player.statMana < player.statManaMax2 * 0.3f ? 12 : 6;
            player.statMana = Math.Min(player.statMana + amount, player.statManaMax2);
            player.ManaEffect(amount);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.5f }, target.Center);
            //紫金符文火花自目标涌回
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    (player.Center - target.Center).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2f, 4f)
                        + Main.rand.NextVector2Circular(1f, 1f),
                    Main.rand.NextBool() ? new Color(150, 90, 240) : new Color(90, 160, 255),
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(false, Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>召唤师徽章：仆从与鞭命中集结军团号令，层数换全军增伤</summary>
    internal class GodSmithSummonerEmblem : GodSmithAccEffect
    {
        /// <summary>号令叠层上限</summary>
        internal const int MaxStacks = 4;

        /// <summary>号令持续帧数</summary>
        internal const int LegionDuration = 360;

        /// <summary>叠层内置冷却</summary>
        private const int StackICD = 20;

        public override int[] TargetItemIDs => [ItemID.SummonerEmblem];

        protected override string EffectDescFallback =>
            "Legion Call: minion and whip hits rally the legion, +1.5% summon damage per stack, up to 4 stacks (6s)\nReaching full rally pulses a golden command ring";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            EmblemWarPlayer war = player.GetModPlayer<EmblemWarPlayer>();
            if (war.LegionStacks > 0) {
                player.GetDamage(DamageClass.Summon) += 0.015f * war.LegionStacks;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Summon) || !state.TryUseCooldown(item.type, StackICD)) {
                return;
            }
            EmblemWarPlayer war = player.GetModPlayer<EmblemWarPlayer>();
            bool wasFull = war.LegionStacks >= MaxStacks;
            war.AddLegionStack();
            //首次集结满编：金色号令环自佩戴者荡开
            if (!wasFull && war.LegionStacks >= MaxStacks) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                    new Color(255, 210, 110), 0.05f)?.Configure(0.08f, 0.5f, 18);
            }
        }
    }

    /// <summary>复仇者徽章：受击反烙复仇印，短窗全伤增幅，越挨打越凶</summary>
    internal class GodSmithAvengerEmblem : GodSmithAccEffect
    {
        /// <summary>复仇窗口帧数</summary>
        internal const int VengeanceDuration = 300;

        public override int[] TargetItemIDs => [ItemID.AvengerEmblem];

        protected override string EffectDescFallback =>
            "Avenger's Brand: taking a hit brands you with vengeance for 5s: +8% damage of all types\nThe brand flares blood-gold when struck";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<EmblemWarPlayer>().VengeanceTimer > 0) {
                player.GetDamage(DamageClass.Generic) += 0.08f;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            player.GetModPlayer<EmblemWarPlayer>().VengeanceTimer = VengeanceDuration;
            if (VaultUtils.isServer) {
                return;
            }
            //血金复仇焰自胸口炸开（受击方本地端权威）
            PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                new Color(220, 90, 50), 0.05f)?.Configure(0.06f, 0.4f, 14);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? new Color(220, 90, 50) : new Color(255, 200, 100),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>毁灭者徽章：对残血目标的任意武器命中追射歼灭脉冲，机械处决协议</summary>
    internal class GodSmithDestroyerEmblem : GodSmithAccEffect
    {
        /// <summary>处决生命阈值</summary>
        private const float ExecuteThreshold = 0.20f;

        /// <summary>脉冲冷却帧数</summary>
        private const int PulseCD = 40;

        public override int[] TargetItemIDs => [ItemID.DestroyerEmblem];

        protected override string EffectDescFallback =>
            "Annihilation Protocol: striking a foe below 20% life fires a crimson probe pulse at it\nThe pulse deals 50% of that strike, once every 0.67s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            //Default 类（本包支援弹）不触发，防自喂
            if (hit.DamageType == DamageClass.Default || target.life <= 0
                || target.life > target.lifeMax * ExecuteThreshold || !state.TryUseCooldown(item.type, PulseCD)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = -0.3f }, player.Center);
            //锁定红光在目标身上一闪
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f),
                    new Color(255, 60, 60), Main.rand.NextFloat(0.3f, 0.45f))?.Configure(false, 10);
            }
            if (player.whoAmI == Main.myPlayer) {
                int pulseDamage = Math.Clamp((int)(damageDone * 0.5f), 10, 250);
                Vector2 vel = (target.Center - player.Center).SafeNormalize(Vector2.UnitX) * 15f;
                Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                    ModContent.ProjectileType<GodSmithDestroyerEmblemPulseProj>(), pulseDamage, 2f, player.whoAmI,
                    target.whoAmI);
            }
        }
    }

    /// <summary>巨像之眼：会心命中锁定凝视窗口，短时会心率再抬，暴击滚雪球</summary>
    internal class GodSmithEyeoftheGolem : GodSmithAccEffect
    {
        /// <summary>凝视窗口帧数</summary>
        internal const int GazeDuration = 240;

        /// <summary>凝视触发冷却</summary>
        private const int GazeCD = 90;

        public override int[] TargetItemIDs => [ItemID.EyeoftheGolem];

        protected override string EffectDescFallback =>
            "Golem's Gaze: landing a critical hit locks the gaze for 4s: +6% critical strike chance\nTriggers once every 1.5s; gazing crits burn with sun sparks";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<EmblemWarPlayer>().GazeTimer > 0) {
                player.GetCritChance(DamageClass.Generic) += 6f;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.Crit || hit.DamageType == DamageClass.Default) {
                return;
            }
            EmblemWarPlayer war = player.GetModPlayer<EmblemWarPlayer>();
            //凝视中的会心带日耀迸溅
            if (war.GazeTimer > 0) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                        Main.rand.NextBool() ? new Color(255, 170, 40) : new Color(255, 230, 140),
                        Main.rand.NextFloat(0.3f, 0.52f))?.Configure(true, Main.rand.Next(14, 22));
                }
            }
            if (!state.TryUseCooldown(item.type, GazeCD)) {
                return;
            }
            war.GazeTimer = GazeDuration;
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.6f }, player.Center);
            PRTLoader.NewParticle<PRT_Light>(player.Center, Vector2.Zero,
                new Color(255, 170, 40), 0.16f)?.Configure(16, 0.8f);
        }
    }

    /// <summary>
    /// 歼灭脉冲：一道有锁定意志的机械红光，直取残血目标；
    /// 双层曳光自绘 + 速度拉伸，命中迸机械火花
    /// </summary>
    internal class GodSmithDestroyerEmblemPulseProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float TargetIndex => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.5417f % 3.14f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            //轻追踪：向锁定目标缓修航向，索敌规则各端确定
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs) {
                NPC target = Main.npc[(int)TargetIndex];
                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 15f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Projectile.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.08f,
                    new Color(255, 70, 60), Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, 8);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.08f, 0.06f));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                    Main.rand.NextBool() ? new Color(255, 70, 60) : new Color(255, 160, 120),
                    Main.rand.NextFloat(0.26f, 0.45f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.LightShot?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.045f, 0.3f, 0.9f);
            float wob = 1f + MathF.Sin(Projectile.timeLeft * 0.7f + Seed * 4f) * 0.08f;
            //外层猩红脉冲体
            Main.EntitySpriteDraw(tex, pos, null, new Color(255, 60, 50) with { A = 0 } * 0.85f,
                Projectile.rotation, origin, new Vector2(stretch, 0.10f * wob), SpriteEffects.None, 0);
            //内层白热芯
            Main.EntitySpriteDraw(tex, pos, null, new Color(255, 220, 200) with { A = 0 } * 0.7f,
                Projectile.rotation, origin, new Vector2(stretch * 0.55f, 0.05f * wob), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>徽章族私有状态载体：战意/专注/号令/复仇/凝视各计时器与层数。攻击方或受击方本地量，无需同步</summary>
    internal class EmblemWarPlayer : ModPlayer
    {
        /// <summary>战士徽章：战意层数</summary>
        internal int WarStacks { get; private set; }

        private int warTimer;

        /// <summary>游侠徽章：专注剩余帧数</summary>
        internal int FocusTimer;

        /// <summary>召唤徽章：号令层数</summary>
        internal int LegionStacks { get; private set; }

        private int legionTimer;

        /// <summary>复仇者徽章：复仇窗口剩余帧数</summary>
        internal int VengeanceTimer;

        /// <summary>巨像之眼：凝视窗口剩余帧数</summary>
        internal int GazeTimer;

        internal void AddWarStack() {
            WarStacks = Math.Min(WarStacks + 1, GodSmithWarriorEmblem.MaxStacks);
            warTimer = GodSmithWarriorEmblem.WarDuration;
        }

        internal void ShedWarStacks(int amount) => WarStacks = Math.Max(0, WarStacks - amount);

        internal void AddLegionStack() {
            LegionStacks = Math.Min(LegionStacks + 1, GodSmithSummonerEmblem.MaxStacks);
            legionTimer = GodSmithSummonerEmblem.LegionDuration;
        }

        public override void PostUpdateMiscEffects() {
            if (warTimer > 0 && --warTimer == 0) {
                WarStacks = 0;
            }
            if (legionTimer > 0 && --legionTimer == 0) {
                LegionStacks = 0;
            }
            if (FocusTimer > 0) {
                FocusTimer--;
            }
            if (VengeanceTimer > 0) {
                VengeanceTimer--;
            }
            if (GazeTimer > 0) {
                GazeTimer--;
            }
        }

        public override void UpdateDead() {
            WarStacks = 0;
            warTimer = 0;
            LegionStacks = 0;
            legionTimer = 0;
            FocusTimer = 0;
            VengeanceTimer = 0;
            GazeTimer = 0;
        }
    }
}
