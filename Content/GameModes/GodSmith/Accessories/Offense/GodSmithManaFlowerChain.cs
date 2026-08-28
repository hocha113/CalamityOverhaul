using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【法花链】五朵法师之花五种供养：自然馈赠=施法回命回蓝、魔力花=低蓝甘露应急、
    /// 秘术花=虹吸叠层、磁石花=磁引闪络（弧光跳目标）、魔力披风=织法星屑（蓄星护主反击）。<br/>
    /// 全部按魔法类过滤（支援弹为 DamageClass.Default，防自喂）；
    /// 每玩家状态在同文件私有 <see cref="ManaBloomPlayer"/>
    /// </summary>
    internal class GodSmithNaturesGift : GodSmithAccEffect
    {
        /// <summary>馈赠冷却帧数</summary>
        private const int GiftCD = 90;

        public override int[] TargetItemIDs => [ItemID.NaturesGift];

        protected override string EffectDescFallback =>
            "Nature's Boon: magic hits bloom with life, healing 5 HP and restoring 5 mana\nTriggers once every 1.5s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Magic) || !state.TryUseCooldown(item.type, GiftCD)) {
                return;
            }
            player.Heal(5);
            player.statMana = Math.Min(player.statMana + 5, player.statManaMax2);
            player.ManaEffect(5);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = 0.4f }, player.Center);
            //翠叶花瓣自身周舒开（命中钩子只在攻击方端跑）
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(12f, 16f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.6f, 1.8f)),
                    Main.rand.NextBool() ? new Color(120, 220, 90) : new Color(200, 255, 150),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(16, 26));
            }
        }
    }

    /// <summary>魔力花：低蓝时甘露涌泉，一口大蓝的应急电池</summary>
    internal class GodSmithManaFlower : GodSmithAccEffect
    {
        /// <summary>触发阈值（法力占比）</summary>
        private const float NectarThreshold = 0.2f;

        /// <summary>甘露冷却帧数（10 秒）</summary>
        private const int NectarCD = 600;

        /// <summary>甘露回蓝量</summary>
        private const int NectarAmount = 50;

        public override int[] TargetItemIDs => [ItemID.ManaFlower];

        protected override string EffectDescFallback =>
            "Nectar Surge: dropping below 20% mana instantly restores 50 mana in a burst of petals\nTriggers once every 10s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            //甘露应急：法力见底自动涌泉（个人量，owner 端权威）
            if (player.whoAmI != Main.myPlayer || player.statMana >= player.statManaMax2 * NectarThreshold
                || !state.TryUseCooldown(item.type, NectarCD)) {
                return;
            }
            player.statMana = Math.Min(player.statMana + NectarAmount, player.statManaMax2);
            player.ManaEffect(NectarAmount);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f }, player.Center);
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                    new Color(90, 140, 255), 0.05f)?.Configure(0.06f, 0.4f, 16);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                        Main.rand.NextBool() ? new Color(90, 140, 255) : new Color(200, 225, 255),
                        Main.rand.NextFloat(0.28f, 0.46f))?.Configure(false, Main.rand.Next(16, 26));
                }
            }
        }
    }

    /// <summary>秘术花：魔法连击虹吸叠层，满层再省蓝，输出续航双收的滚层器</summary>
    internal class GodSmithArcaneFlower : GodSmithAccEffect
    {
        /// <summary>虹吸叠层上限</summary>
        internal const int MaxStacks = 6;

        /// <summary>虹吸持续帧数（命中刷新）</summary>
        internal const int SiphonDuration = 300;

        /// <summary>叠层内置冷却</summary>
        private const int StackICD = 10;

        public override int[] TargetItemIDs => [ItemID.ArcaneFlower];

        protected override string EffectDescFallback =>
            "Arcane Siphon: magic hits siphon power, +1% magic damage per stack, up to 6 stacks (5s)\nAt full siphon your spells also cost 5% less mana";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            ManaBloomPlayer bloom = player.GetModPlayer<ManaBloomPlayer>();
            if (bloom.SiphonStacks <= 0) {
                return;
            }
            player.GetDamage(DamageClass.Magic) += 0.01f * bloom.SiphonStacks;
            if (bloom.SiphonStacks >= MaxStacks) {
                player.manaCost -= 0.05f;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Magic)) {
                return;
            }
            ManaBloomPlayer bloom = player.GetModPlayer<ManaBloomPlayer>();
            if (state.TryUseCooldown(item.type, StackICD)) {
                bloom.AddSiphonStack();
            }
            //满层虹吸：紫金符文火花
            if (bloom.SiphonStacks >= MaxStacks) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                        Main.rand.NextBool() ? new Color(170, 100, 255) : new Color(255, 210, 120),
                        Main.rand.NextFloat(0.26f, 0.42f))?.Configure(false, Main.rand.Next(12, 18));
                }
            }
        }
    }

    /// <summary>磁石花：命中把法力磁引成弧光，跳向近旁第二个敌人，群战导体</summary>
    internal class GodSmithMagnetFlower : GodSmithAccEffect
    {
        /// <summary>闪络冷却帧数</summary>
        private const int ArcCD = 45;

        /// <summary>闪络搜索半径（像素）</summary>
        private const float ArcRange = 300f;

        public override int[] TargetItemIDs => [ItemID.MagnetFlower];

        protected override string EffectDescFallback =>
            "Magnetic Arc: magic hits fling an arc of charge to another foe within 19 tiles\nThe arc deals 35% of that hit, once every 0.75s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Magic) || state.IsOnCooldown(item.type)) {
                return;
            }
            //先找第二目标，找不到不消耗冷却
            NPC next = FindNextTarget(target);
            if (next == null) {
                return;
            }
            state.SetCooldown(item.type, ArcCD);
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.4f }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(2f, 2f),
                    new Color(120, 200, 255), Main.rand.NextFloat(0.26f, 0.4f))?.Configure(false, 12);
            }
            if (player.whoAmI == Main.myPlayer) {
                int arcDamage = Math.Clamp((int)(damageDone * 0.35f), 8, 200);
                Vector2 vel = (next.Center - target.Center).SafeNormalize(Vector2.UnitX) * 14f;
                Projectile.NewProjectile(player.GetSource_Accessory(item), target.Center, vel,
                    ModContent.ProjectileType<GodSmithMagnetFlowerArcProj>(), arcDamage, 1.5f, player.whoAmI,
                    next.whoAmI);
            }
        }

        private static NPC FindNextTarget(NPC exclude) {
            NPC best = null;
            float bestDist = ArcRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == exclude.whoAmI || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = exclude.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }

    /// <summary>魔力披风：施法织星屑蓄于周身，受击时全数掷向敌人，蓄势反击的软甲</summary>
    internal class GodSmithManaCloak : GodSmithAccEffect
    {
        /// <summary>星屑储备上限</summary>
        internal const int MaxCharges = 3;

        /// <summary>织星内置冷却</summary>
        private const int WeaveICD = 20;

        public override int[] TargetItemIDs => [ItemID.ManaCloak];

        protected override string EffectDescFallback =>
            "Woven Stars: magic hits weave mana stardust around you, up to 3 charges\nWhen struck, all stored stardust flies out as homing star bolts dealing 60 damage each";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            ManaBloomPlayer bloom = player.GetModPlayer<ManaBloomPlayer>();
            if (bloom.StarCharges <= 0 || VaultUtils.isServer) {
                return;
            }
            //蓄星读数：星屑沿轨环绕微闪（攻击方端本地量，仅佩戴者可见）
            for (int i = 0; i < bloom.StarCharges; i++) {
                if (!Main.rand.NextBool(8)) {
                    continue;
                }
                float angle = Main.GameUpdateCount * 0.045f + MathHelper.TwoPi * i / MaxCharges;
                Vector2 at = player.Center + angle.ToRotationVector2() * 26f;
                PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, new Color(140, 170, 255),
                    Main.rand.NextFloat(0.05f, 0.08f))?.Configure(10, 0.8f);
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Magic) || !state.TryUseCooldown(item.type, WeaveICD)) {
                return;
            }
            ManaBloomPlayer bloom = player.GetModPlayer<ManaBloomPlayer>();
            if (bloom.StarCharges >= MaxCharges) {
                return;
            }
            bloom.StarCharges++;
            PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(20f, 24f),
                new Vector2(0f, -0.8f), new Color(140, 170, 255), 0.09f)?.Configure(14, 0.8f);
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            ManaBloomPlayer bloom = player.GetModPlayer<ManaBloomPlayer>();
            if (bloom.StarCharges <= 0) {
                return;
            }
            int charges = bloom.StarCharges;
            bloom.StarCharges = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.1f }, player.Center);
            }
            //星屑护主反击：owner 侧生成（受击方本地端权威）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            for (int i = 0; i < charges; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedBy((i - (charges - 1) * 0.5f) * 0.6f) * 9f
                    + Main.rand.NextVector2Circular(1f, 1f);
                Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                    ModContent.ProjectileType<GodSmithManaCloakStarProj>(), 60, 2f, player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 磁光弧：一段被磁石拽出的高压电荷，直扑第二目标；
    /// 双层电蓝曳光 + 锯齿抖动，命中炸出静电火花
    /// </summary>
    internal class GodSmithMagnetFlowerArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float TargetIndex => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.8311f % 2.77f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs) {
                NPC target = Main.npc[(int)TargetIndex];
                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 14f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.15f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Projectile.timeLeft % 2 == 0) {
                //电荷失稳：沿途甩静电屑
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    new Color(120, 200, 255), Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, 8);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.15f, 0.3f, 0.5f));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? new Color(120, 200, 255) : new Color(220, 245, 255),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.LightShot?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.3f, 0.8f);
            //电弧抖动：宽度以高频确定性相位痉挛
            float jitter = 1f + MathF.Sin(Projectile.timeLeft * 1.6f + Seed * 7f) * 0.25f;
            Main.EntitySpriteDraw(tex, pos, null, new Color(90, 180, 255) with { A = 0 } * 0.85f,
                Projectile.rotation, origin, new Vector2(stretch, 0.08f * jitter), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, new Color(230, 250, 255) with { A = 0 } * 0.7f,
                Projectile.rotation, origin, new Vector2(stretch * 0.5f, 0.04f * jitter), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 织法星屑：一粒受惊出鞘的法力星，先散后咬向最近敌人；
    /// 星芒旋转自绘 + 淡蓝光晕，亡处散星尘
    /// </summary>
    internal class GodSmithManaCloakStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.6733f % 3.31f;

        /// <summary>散开段帧数，之后开始追踪</summary>
        private const int ScatterFrames = 10;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.05f, 0.18f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }
            if (!Main.dedServ && Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.06f,
                    new Color(140, 170, 255), Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, 10);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.25f, 0.5f));
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

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f, Pitch = 0.7f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? new Color(140, 170, 255) : new Color(230, 240, 255),
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(false, Main.rand.Next(12, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float spin = Life * 0.12f + Seed * 3f;
            float pulse = 1f + MathF.Sin(Life * 0.4f + Seed * 5f) * 0.12f;
            //光晕垫底
            Main.EntitySpriteDraw(glow, pos, null, new Color(90, 130, 255) with { A = 0 } * 0.5f,
                0f, glow.Size() * 0.5f, 0.5f * pulse, SpriteEffects.None, 0);
            //星芒本体旋转
            Main.EntitySpriteDraw(star, pos, null, new Color(180, 210, 255) with { A = 0 } * 0.9f,
                spin, star.Size() * 0.5f, 0.32f * pulse, SpriteEffects.None, 0);
            //白炽星芯
            Main.EntitySpriteDraw(star, pos, null, new Color(255, 255, 255) with { A = 0 } * 0.6f,
                -spin * 0.7f, star.Size() * 0.5f, 0.18f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>法花链私有状态载体：虹吸叠层与织法星屑。攻击方端本地量，无需同步</summary>
    internal class ManaBloomPlayer : ModPlayer
    {
        /// <summary>秘术花：虹吸层数</summary>
        internal int SiphonStacks { get; private set; }

        private int siphonTimer;

        /// <summary>魔力披风：蓄存星屑数</summary>
        internal int StarCharges;

        internal void AddSiphonStack() {
            SiphonStacks = Math.Min(SiphonStacks + 1, GodSmithArcaneFlower.MaxStacks);
            siphonTimer = GodSmithArcaneFlower.SiphonDuration;
        }

        public override void PostUpdateMiscEffects() {
            if (siphonTimer > 0 && --siphonTimer == 0) {
                SiphonStacks = 0;
            }
        }

        public override void UpdateDead() {
            SiphonStacks = 0;
            siphonTimer = 0;
            StarCharges = 0;
        }
    }
}
