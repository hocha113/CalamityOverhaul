using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】丛林套「孢子疫域」（A 档）：材质=活的孢子云。<br/>
    /// ①命中积攒孢子 ②满 6 层后下一击在目标处绽放一朵驻场孢子云（在场上限 2，
    /// 已满时命中不消耗层数攒着等空位）③域内敌人每半秒受创一次并中毒，
    /// 三团雾瓣慢转呼吸 + 孢子光点利萨茹漂移的驻场演出 ④受击惊散掉 2 层孢子。<br/>
    /// 原版套装奖励（减耗蓝）保留，神赋是叠加层；孢子层数是攻击方端本地量，
    /// 满孢绿辉只对佩戴者自己可见（个人读数），跨端可见的部分是孢子云实体
    /// </summary>
    internal class GsJungleArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.JungleHat, ItemID.AncientCobaltHelmet];

        public override int BodyID => ItemID.JungleShirt;

        public override int LegsID => ItemID.JunglePants;

        protected override string EndowLineFallback =>
            "Spore Blight: strikes build spores; at 6 stacks the next strike blooms a lingering cloud that poisons all foes inside";

        //孢子云色板
        internal static readonly Color LeafDark = new(30, 70, 36);
        internal static readonly Color SporeGreen = new(110, 190, 80);
        internal static readonly Color LimeBright = new(190, 240, 120);

        /// <summary>绽放所需孢子层数</summary>
        private const int FullCharge = 6;

        /// <summary>孢子云在场上限</summary>
        private const int MaxClouds = 2;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //满孢态：绿荧微光绕身（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, SporeGreen.ToVector3() * 0.18f);
            if (Main.rand.NextBool(10)) {
                Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2CircularEdge(16f, 24f),
                    DustID.JungleSpore, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = true;
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //孢子云自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsJungleSporeCloudProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //在场上限已满：这次命中不消耗层数，攒着等空位
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GsJungleSporeCloudProj>()] >= MaxClouds) {
                return;
            }

            //绽放：满孢后这一击在目标处开出一朵孢子云
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(16f, 16f),
                        DustID.JungleSpore, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                        100, default, Main.rand.NextFloat(1f, 1.4f));
                    d.noGravity = true;
                }
            }
            //proc 弹幕 owner 侧生成；每跳伤害按触发伤害 10% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int tickDamage = Math.Clamp((int)(damageDone * 0.10f), 4, 50);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithJungleEndow"),
                    target.Center, Main.rand.NextVector2Circular(0.2f, 0.2f),
                    ModContent.ProjectileType<GsJungleSporeCloudProj>(), tickDamage, 0f, player.whoAmI);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击惊散：孢子掉两层
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.JungleSpore, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.3f, 1.5f)),
                        100, default, Main.rand.NextFloat(0.8f, 1.1f));
                    d.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 丛林套「远古钴变体」：远古钴护胸 + 远古钴护腿组成的整套同样绽放疫域；
    /// 机制与本体套完全一致（薄子类只换胸腿 ID，头盔已在主方案 HeadIDs 列出）
    /// </summary>
    internal class GsJungleArmorAncientCobalt : GsJungleArmor
    {
        public override int BodyID => ItemID.AncientCobaltBreastplate;

        public override int LegsID => ItemID.AncientCobaltLeggings;
    }

    /// <summary>
    /// 孢子云：一朵活着呼吸的疫域，不是静止贴纸。缓漂减速驻场 5 秒，域内敌人每半秒受创并中毒；
    /// 三团雾瓣确定性慢转轨道 + 整体呼吸缩放 + 五粒孢子光点利萨茹漂移，
    /// 前 20 帧从零涨开、末 30 帧收场消散，域内敌人身上飘绿光
    /// </summary>
    internal class GsJungleSporeCloudProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Fog";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //生成时定域大小（一次性）
            if (Life == 1f) {
                Projectile.Resize(120, 120);
            }
            //缓漂：初速极小，逐帧再衰减，云是驻场物不是飞弹
            Projectile.velocity *= 0.98f;

            if (!Main.dedServ) {
                //偶发孢子尘上飘
                if (Main.rand.NextBool(4)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(50f, 40f),
                        DustID.JungleSpore, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.2f, 0.6f)),
                        100, default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = true;
                }
                //域内敌人轻微飘绿光，中毒的因果可见
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.friendly || !npc.Hitbox.Intersects(Projectile.Hitbox)) {
                        continue;
                    }
                    Lighting.AddLight(npc.Center, GsJungleArmor.SporeGreen.ToVector3() * 0.12f);
                    if (Main.rand.NextBool(6)) {
                        Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                            DustID.JungleSpore, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                            120, default, Main.rand.NextFloat(0.6f, 0.9f));
                        d.noGravity = true;
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsJungleArmor.SporeGreen.ToVector3() * 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 120);
            if (Main.dedServ) {
                return;
            }
            //每跳反馈：孢子在伤处扑开（域每半秒一跳，不配音效防刷耳）
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.JungleSpore, Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 1.5f),
                    100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = true;
            }
        }

        //==================== 绘制：三团雾瓣慢转 + 呼吸缩放 + 五粒孢子光点利萨茹漂移 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D dot = CWRAsset.SoftGlow?.Value;
            if (fog == null) {
                return false;
            }
            Vector2 posDraw = Projectile.Center - Main.screenPosition;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            //前 20 帧从零涨开，末 30 帧收场消散
            float growIn = MathHelper.Clamp(Life / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float fade = growIn * fadeOut;
            //整体呼吸
            float breathe = 1f + 0.08f * MathF.Sin(Life * 0.07f + Seed);

            //三团雾瓣：各自确定性慢转轨道，暗边垫底 + 绿瓣主体（Fog 真 alpha 直接染色）
            for (int i = 0; i < 3; i++) {
                float ang = Seed * 2.1f + Life * 0.012f + i * 2.09f;
                Vector2 off = ang.ToRotationVector2() * (16f + i * 3f) * growIn;
                float blobRot = ang + Seed + i;
                Main.EntitySpriteDraw(fog, posDraw + off, null, GsJungleArmor.LeafDark * (0.5f * fade),
                    blobRot, fogOrigin, 0.6f * breathe * growIn, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(fog, posDraw + off, null, GsJungleArmor.SporeGreen * (0.42f * fade),
                    blobRot, fogOrigin, 0.48f * breathe * growIn, SpriteEffects.None, 0);
            }

            //五粒孢子光点：利萨茹漂移，各自异相明灭（黑底贴图走加色观感）
            if (dot != null) {
                Vector2 dotOrigin = dot.Size() * 0.5f;
                for (int i = 0; i < 5; i++) {
                    float t = Life * 0.03f + i * 1.7f + Seed;
                    Vector2 off = new(MathF.Sin(t) * 34f, MathF.Cos(t * 1.37f + i) * 26f);
                    float pulse = 0.35f + 0.25f * MathF.Sin(Life * 0.09f + i * 2.3f + Seed);
                    Main.EntitySpriteDraw(dot, posDraw + off * growIn, null,
                        (GsJungleArmor.LimeBright with { A = 0 }) * (pulse * fade), 0f, dotOrigin,
                        0.10f + 0.02f * MathF.Sin(t * 2f), SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
