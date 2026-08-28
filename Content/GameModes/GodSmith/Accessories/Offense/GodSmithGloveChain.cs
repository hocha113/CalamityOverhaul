using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【手套链】野爪与巨力手套家族：①野爪=野性节奏（连击叠攻速，受击中断）
    /// ②泰坦/能量/机械手套多认领共用「泰坦震击」（每第 5 次近战命中轰出冲击波，档位随链递进）
    /// ③熔火护手=震击点燃 ④狂战护手=震击附带狂暴窗口。<br/>
    /// 震击伤害走 <see cref="GodSmithTitanGloveQuakeProj"/>（DamageClass.Default，
    /// 类过滤天然防自喂）；链内共用震击冷却键防多件同帧连爆
    /// </summary>
    internal class GodSmithFeralClaws : GodSmithAccEffect
    {
        /// <summary>野性叠层上限</summary>
        internal const int MaxStacks = 8;

        /// <summary>连击维持帧数（命中刷新）</summary>
        internal const int ChainWindow = 150;

        /// <summary>叠层内置冷却，防高速连击瞬间叠满</summary>
        private const int StackICD = 6;

        public override int[] TargetItemIDs => [ItemID.FeralClaws];

        protected override string EffectDescFallback =>
            "Wild Tempo: chaining melee hits within 2.5s stacks +1% melee speed each, up to 8 stacks\nAt 6+ stacks strikes shed feral claw sparks; taking a hit breaks the tempo";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            GloveChainPlayer claws = player.GetModPlayer<GloveChainPlayer>();
            if (claws.FeralStacks <= 0) {
                return;
            }
            player.GetAttackSpeed(DamageClass.Melee) += 0.01f * claws.FeralStacks;
            //满层态：腕间掠过兽性红痕（攻击方端本地量，仅佩戴者可见）
            if (claws.FeralStacks >= MaxStacks && !VaultUtils.isServer && Main.rand.NextBool(12)) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(14f, 18f),
                    new Vector2(player.direction * 1.2f, -0.6f), new Color(230, 60, 50),
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, 12);
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Melee)) {
                return;
            }
            GloveChainPlayer claws = player.GetModPlayer<GloveChainPlayer>();
            if (state.TryUseCooldown(item.type, StackICD)) {
                claws.AddFeralStack();
            }
            //高层数时挥击带兽爪血火花（命中钩子只在攻击方端跑）
            if (claws.FeralStacks >= 6) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        hit.HitDirection * new Vector2(Main.rand.NextFloat(2f, 5f), 0f).RotatedByRandom(0.6f),
                        Main.rand.NextBool() ? new Color(230, 60, 50) : new Color(255, 150, 90),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            //受击节奏中断
            GloveChainPlayer claws = player.GetModPlayer<GloveChainPlayer>();
            if (claws.FeralStacks <= 0) {
                return;
            }
            claws.ClearFeral();
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust dust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 14f),
                        DustID.Blood, Main.rand.NextVector2Circular(2f, 2f), 120);
                    dust.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 巨力手套家族公共层：每第 N 次近战命中在目标处轰出泰坦震击。
    /// 计数按类分键存 <see cref="GloveChainPlayer"/>；
    /// 链内共用震击冷却键（<see cref="QuakeSharedCDKey"/>）防多件同帧连爆
    /// </summary>
    internal abstract class GloveImpactBase : GodSmithAccEffect
    {
        /// <summary>链内共用震击冷却键：正键高位偏移，避开物品 type 键域（负键域归词缀神赋，约定 2026-08-27）</summary>
        internal const int QuakeSharedCDKey = ItemID.TitanGlove + 10_000_000;

        /// <summary>触发震击所需近战命中数</summary>
        protected virtual int HitsPerQuake => 5;

        /// <summary>震击伤害 = 触发那一击 × 此系数（按触发那件的 type 定档；效果是共享单例，禁实例字段存档位）</summary>
        protected abstract float QuakeRatio(int itemType);

        /// <summary>震击半径（像素，按触发那件的 type 定档）</summary>
        protected abstract int QuakeRadius(int itemType);

        /// <summary>震击火花主色</summary>
        protected abstract Color SparkColor { get; }

        /// <summary>震击是否点燃（狱火）</summary>
        protected virtual bool QuakeIgnites => false;

        /// <summary>震击触发后的档位专属附带效果</summary>
        protected virtual void OnQuake(Item item, Player player, GodSmithPlayer state, NPC target) { }

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            //震击弹为 DamageClass.Default，近战过滤天然防自喂
            if (!hit.DamageType.CountsAsClass(DamageClass.Melee)) {
                return;
            }
            GloveChainPlayer chain = player.GetModPlayer<GloveChainPlayer>();
            //计数键按类分（TargetItemIDs[0]），多件同链各自蓄力
            if (chain.AddImpact(TargetItemIDs[0]) < HitsPerQuake) {
                return;
            }
            //链内共用冷却：未就绪时计数保持满值，下一击再试
            if (!state.TryUseCooldown(QuakeSharedCDKey, 30)) {
                return;
            }
            chain.ResetImpact(TargetItemIDs[0]);
            float ratio = QuakeRatio(item.type);
            int radius = QuakeRadius(item.type);

            //震击演出：冲击环 + 迸溅钢屑（命中钩子只在攻击方端跑）
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.55f, Pitch = 0.35f }, target.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, SparkColor, 0.05f)
                ?.Configure(0.06f, radius / 380f, 16);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f),
                    Main.rand.NextBool() ? SparkColor : Color.Lerp(SparkColor, Color.White, 0.5f),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(14, 24));
            }

            //范围伤害弹 owner 侧生成，伤害按触发伤害折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int quakeDamage = Math.Clamp((int)(damageDone * ratio), 8, 300);
                Projectile.NewProjectile(player.GetSource_Accessory(item), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GodSmithTitanGloveQuakeProj>(), quakeDamage, 9f, player.whoAmI,
                    radius, QuakeIgnites ? 1f : 0f);
            }
            OnQuake(item, player, state, target);
        }
    }

    /// <summary>泰坦/能量/机械手套多认领：同一「泰坦震击」按链递进档位（35%/45%/55%，半径同步扩）</summary>
    internal class GodSmithTitanGlove : GloveImpactBase
    {
        public override int[] TargetItemIDs => [ItemID.TitanGlove, ItemID.PowerGlove, ItemID.MechanicalGlove];

        protected override string EffectDescFallback =>
            "Titan Quake: every 5th melee strike slams a shockwave around the target\nDeals 35% / 45% / 55% of that strike (Titan / Power / Mechanical) in an area with heavy knockback";

        //档位随链递进：由触发那件决定
        protected override float QuakeRatio(int itemType)
            => itemType == ItemID.TitanGlove ? 0.35f : itemType == ItemID.PowerGlove ? 0.45f : 0.55f;

        protected override int QuakeRadius(int itemType)
            => itemType == ItemID.TitanGlove ? 140 : itemType == ItemID.PowerGlove ? 160 : 180;

        protected override Color SparkColor => new(200, 210, 230);
    }

    /// <summary>熔火护手：震击升格为灼热爆轰，点燃敌人（狱火）</summary>
    internal class GodSmithFireGauntlet : GloveImpactBase
    {
        public override int[] TargetItemIDs => [ItemID.FireGauntlet];

        protected override string EffectDescFallback =>
            "Molten Quake: every 5th melee strike erupts a burning shockwave dealing 60% of that strike\nThe blast ignites foes with hellfire";

        protected override float QuakeRatio(int itemType) => 0.60f;

        protected override int QuakeRadius(int itemType) => 190;

        protected override Color SparkColor => new(255, 140, 40);

        protected override bool QuakeIgnites => true;

        protected override void OnQuake(Item item, Player player, GodSmithPlayer state, NPC target) {
            //熔滴自爆心涌出（攻击方端）
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6f), 0, default, Main.rand.NextFloat(1.4f, 2f));
                dust.noGravity = true;
            }
        }
    }

    /// <summary>狂战护手：震击最重，且每次震击驱入狂暴（近战速度+10%，3 秒）</summary>
    internal class GodSmithBerserkerGlove : GloveImpactBase
    {
        /// <summary>狂暴窗口帧数</summary>
        internal const int BerserkDuration = 180;

        public override int[] TargetItemIDs => [ItemID.BerserkerGlove];

        protected override string EffectDescFallback =>
            "Berserk Quake: every 5th melee strike detonates a shockwave dealing 70% of that strike\nEach quake drives you berserk: +10% melee speed for 3s";

        protected override float QuakeRatio(int itemType) => 0.70f;

        protected override int QuakeRadius(int itemType) => 210;

        protected override Color SparkColor => new(255, 230, 180);

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            GloveChainPlayer chain = player.GetModPlayer<GloveChainPlayer>();
            if (chain.BerserkTimer <= 0) {
                return;
            }
            player.GetAttackSpeed(DamageClass.Melee) += 0.10f;
            //狂暴态白热余焰
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(12f, 18f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)), new Color(255, 230, 180),
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.7f);
            }
        }

        protected override void OnQuake(Item item, Player player, GodSmithPlayer state, NPC target)
            => player.GetModPlayer<GloveChainPlayer>().BerserkTimer = BerserkDuration;
    }

    /// <summary>
    /// 泰坦震击波：一记砸进地面的巨力，不是光圈贴纸。
    /// 短命范围判定 + 三层自绘（扩散环收口、星芒重击闪、暗压边），确定性抖动不掷 Main.rand
    /// </summary>
    internal class GodSmithTitanGloveQuakeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Radius => ref Projectile.ai[0];

        private ref float IgniteFlag => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.6173f % 2.83f;

        private const int LifeMax = 14;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
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
            if (Life == 0f) {
                //按生成参数展开判定域
                int size = (int)MathHelper.Clamp(Radius <= 0f ? 160f : Radius, 60f, 320f) * 2;
                Projectile.Resize(size, size);
            }
            Life++;
            //判定只留前段，后段纯余波
            if (Life > 6f) {
                Projectile.friendly = false;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.5f, 0.45f) * (1f - Life / LifeMax));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (IgniteFlag == 1f) {
                target.AddBuff(BuffID.OnFire3, 300);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (ring == null || star == null) {
                return false;
            }
            float progress = MathHelper.Clamp(Life / LifeMax, 0f, 1f);
            float fade = 1f - progress;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float radius = Projectile.width * 0.5f;

            //扩散环：由内向外推，宽度随生命收口
            float ringScale = radius * 2f / ring.Width * (0.35f + 0.75f * MathF.Sqrt(progress));
            Color ringColor = new Color(220, 225, 235) with { A = 0 };
            Main.EntitySpriteDraw(ring, pos, null, ringColor * (0.85f * fade), Seed,
                ring.Size() * 0.5f, ringScale, SpriteEffects.None, 0);
            //暗压边：真 alpha 焦灰外缘，给冲击一个重量下缘
            Main.EntitySpriteDraw(ring, pos, null, new Color(60, 55, 50) * (0.35f * fade), -Seed,
                ring.Size() * 0.5f, ringScale * 1.08f, SpriteEffects.None, 0);
            //星芒重击闪：只活前 6 帧
            if (Life <= 6f) {
                float flash = 1f - Life / 6f;
                Color flashColor = new Color(255, 250, 235) with { A = 0 };
                Main.EntitySpriteDraw(star, pos, null, flashColor * (0.8f * flash),
                    Seed * 2f + Life * 0.05f, star.Size() * 0.5f,
                    radius / star.Width * (1.1f + 0.5f * flash), SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>手套链私有状态载体：野性叠层、震击计数（按类分键）、狂暴窗口。攻击方端本地量，无需同步</summary>
    internal class GloveChainPlayer : ModPlayer
    {
        /// <summary>当前野性层数</summary>
        internal int FeralStacks { get; private set; }

        private int feralTimer;

        /// <summary>狂暴剩余帧数（狂战护手震击触发）</summary>
        internal int BerserkTimer;

        //震击计数，键 = 各手套类的 TargetItemIDs[0]
        private readonly Dictionary<int, int> impactCounts = [];

        internal void AddFeralStack() {
            FeralStacks = Math.Min(FeralStacks + 1, GodSmithFeralClaws.MaxStacks);
            feralTimer = GodSmithFeralClaws.ChainWindow;
        }

        internal void ClearFeral() {
            FeralStacks = 0;
            feralTimer = 0;
        }

        /// <summary>命中计数 +1 并返回当前值</summary>
        internal int AddImpact(int key) {
            impactCounts.TryGetValue(key, out int count);
            impactCounts[key] = ++count;
            return count;
        }

        internal void ResetImpact(int key) => impactCounts[key] = 0;

        public override void PostUpdateMiscEffects() {
            if (feralTimer > 0 && --feralTimer == 0) {
                FeralStacks = 0;
            }
            if (BerserkTimer > 0) {
                BerserkTimer--;
            }
        }

        public override void UpdateDead() {
            ClearFeral();
            BerserkTimer = 0;
            impactCounts.Clear();
        }
    }
}
