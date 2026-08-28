using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 幽灵套双盔共用的子族骨架：只吃魔法弹幕命中（材质=幽界灵质的冷蓝磷光）；
    /// 兜帽=灵愈圣所（治疗向）、面具=幽界裂隙（输出向），两条分立神赋按头盔分流
    /// </summary>
    internal abstract class GsSpectreArmorScheme : GsArmorsBChargeScheme
    {
        public override int BodyID => ItemID.SpectreRobe;

        public override int LegsID => ItemID.SpectrePants;

        //幽界磷光色板
        internal static readonly Color SpectreWhite = new(232, 255, 250);
        internal static readonly Color SpectreCyan = new(122, 236, 220);
        internal static readonly Color SpectreBlue = new(66, 158, 196);
        internal static readonly Color SpectreDeep = new(24, 72, 96);
        internal static readonly Color HealGreen = new(150, 255, 178);

        protected override Color ThemeMain => SpectreCyan;

        protected override Color ThemeBright => SpectreWhite;

        protected sealed override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsSpectreSanctumProj>()
            || proj.type == ModContent.ProjectileType<GsSpectreSoulOrbProj>()
            || proj.type == ModContent.ProjectileType<GsSpectreRiftProj>();

        public sealed override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //灵质只应魔法：魔法弹幕命中才积攒/触发
            if (sourceProj == null || !sourceProj.CountsAsClass(DamageClass.Magic)) {
                return;
            }
            base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
        }
    }

    /// <summary>
    /// 【幽灵套·兜帽（治疗）★A】灵愈圣所：①魔法命中积攒灵液，满十层在脚下展开圣所五秒
    /// ②圣所周期灼噬域内敌人，每灼一人便抽出一枚灵愈光球飞回佩戴者回血
    /// ③圣所中央灵烛长明，谢幕时烛光散作萤群。原版套装奖励（伤害转治疗球）保留，神赋叠加
    /// </summary>
    internal class GsSpectreHoodArmor : GsSpectreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SpectreHood];

        protected override string EndowLineFallback =>
            "Soulmend Sanctum: magic hits build ectoplasm; at 10 stacks a sanctum unfolds underfoot for 5s, searing foes inside and drawing soul orbs back to mend you";

        protected override int FullCharge => 10;

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.35f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int pulseDamage = Math.Clamp((int)(damageDone * 0.25f), 6, 90);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithSpectreEndow"),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSpectreSanctumProj>(),
                pulseDamage, 0f, player.whoAmI);
        }
    }

    /// <summary>
    /// 【幽灵套·面具（输出）★A】幽界裂隙：①魔法命中积攒怨魂，满八层后下一击在目标处撕开裂隙
    /// ②裂隙驻场三秒，持续把周围敌人拖向隙心并周期喷吐魂焰脉冲 ③闭合时向内坍缩、散出逃逸的魂影。
    /// 原版套装奖励（幽灵弹追击）保留，神赋叠加
    /// </summary>
    internal class GsSpectreMaskArmor : GsSpectreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SpectreMask];

        protected override string EndowLineFallback =>
            "Nether Rift: magic hits build wraiths; at 8 stacks the next hit tears a rift that drags nearby foes inward and pulses soulflame for 3s";

        protected override int FullCharge => 8;

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.6f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.2f }, target.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int pulseDamage = Math.Clamp((int)(damageDone * 0.30f), 8, 120);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithSpectreEndow"),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSpectreRiftProj>(),
                pulseDamage, 2f, player.whoAmI);
        }
    }

    /// <summary>
    /// 灵愈圣所：脚下展开的幽界灵阵，界环双层 + 三盏绕行灵烛 + 中央灵焰；
    /// 每 40 帧灼噬域内敌人，每灼一人抽一枚灵愈光球飞回佩戴者（佩戴者端回血 2 点）
    /// </summary>
    internal class GsSpectreSanctumProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.5807f % 2.81f;

        /// <summary>圣所半径</summary>
        private const float Radius = 200f;

        /// <summary>灼噬周期</summary>
        private const int PulseInterval = 40;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 15f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = PulseInterval;
        }

        /// <summary>只在灼噬拍的短窗判定</summary>
        public override bool? CanDamage() => Life % PulseInterval < 4 && Life > 15;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2())
                < Radius + targetHitbox.Width * 0.25f;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //灼噬拍前奏：界环增亮由绘制层处理；音效只在拍点
            if (!Main.dedServ && Life % PulseInterval == 0 && Life > 15) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            }

            //灵萤上升（客户端装饰）
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Vector2 at = Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.85f, 24f) + new Vector2(0f, 10f);
                PRTLoader.NewParticle<PRT_Spark>(at,
                    new Vector2(MathF.Sin(Life * 0.04f + at.X * 0.02f) * 0.3f, -Main.rand.NextFloat(0.6f, 1.6f)),
                    Main.rand.NextBool(4) ? GsSpectreArmorScheme.HealGreen : GsSpectreArmorScheme.SpectreCyan,
                    Main.rand.NextFloat(0.16f, 0.3f))?.Configure(false, Main.rand.Next(20, 34));
            }
            Lighting.AddLight(Projectile.Center, GsSpectreArmorScheme.SpectreCyan.ToVector3() * (0.4f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //每灼一人抽一枚灵愈光球（命中钩子只在佩戴者端跑）
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    target.Center, -Vector2.UnitY * 3f,
                    ModContent.ProjectileType<GsSpectreSoulOrbProj>(),
                    0, 0f, Projectile.owner);
            }
            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        -Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f) + Main.rand.NextVector2Circular(1f, 0.5f),
                        GsSpectreArmorScheme.SpectreCyan, Main.rand.NextFloat(0.25f, 0.4f))
                        ?.Configure(false, Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //谢幕：烛光散作萤群
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(40f, 16f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2.2f) + Main.rand.NextVector2Circular(0.8f, 0.3f),
                    Main.rand.NextBool(3) ? GsSpectreArmorScheme.HealGreen : GsSpectreArmorScheme.SpectreCyan,
                    Main.rand.NextFloat(0.2f, 0.36f))?.Configure(false, Main.rand.Next(24, 40));
            }
        }

        //==================== 绘制：界环双层 + 绕行灵烛 + 中央灵焰 + 灼噬拍闪 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ring == null || core == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //灼噬拍闪：拍前 6 帧界环收拢增亮
            float beat = Life % PulseInterval > PulseInterval - 6
                ? (Life % PulseInterval - (PulseInterval - 6)) / 6f : 0f;
            float breathe = 1f + MathF.Sin(Life * 0.05f + Seed * 2f) * 0.02f;

            //压地灵阵底（扁椭圆暗层，真 alpha）
            Main.EntitySpriteDraw(core, pos + new Vector2(0f, 14f), null,
                GsSpectreArmorScheme.SpectreDeep * (0.55f * fade), 0f, core.Size() * 0.5f,
                new Vector2(Radius * 2.4f / core.Width, 0.30f), SpriteEffects.None, 0);
            //界环双层（外定内旋）
            Main.EntitySpriteDraw(ring, pos + new Vector2(0f, 10f), null,
                (GsSpectreArmorScheme.SpectreBlue with { A = 0 }) * ((0.5f + beat * 0.35f) * fade), 0f, ring.Size() * 0.5f,
                new Vector2(Radius * 2f * breathe / ring.Width, Radius * 0.5f / ring.Width), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(ring, pos + new Vector2(0f, 10f), null,
                (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * ((0.35f + beat * 0.3f) * fade), 0f, ring.Size() * 0.5f,
                new Vector2(Radius * (1.7f - beat * 0.25f) / ring.Width, Radius * 0.42f / ring.Width), SpriteEffects.None, 0);
            //三盏绕行灵烛
            for (int i = 0; i < 3; i++) {
                float ang = Life * 0.02f + MathHelper.TwoPi * i / 3f + Seed;
                Vector2 candle = pos + new Vector2(MathF.Cos(ang) * Radius * 0.72f, MathF.Sin(ang) * 18f - 6f);
                float depth = MathF.Sin(ang) * 0.5f + 0.5f;
                Main.EntitySpriteDraw(core, candle, null,
                    (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * ((0.4f + depth * 0.4f) * fade), 0f, core.Size() * 0.5f,
                    new Vector2(0.045f, 0.09f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, candle - new Vector2(0f, 8f), null,
                    (GsSpectreArmorScheme.SpectreWhite with { A = 0 }) * ((0.35f + depth * 0.3f) * fade), 0f, glow.Size() * 0.5f,
                    0.16f, SpriteEffects.None, 0);
            }
            //中央灵焰（双层摇曳）
            float lick = 1f + MathF.Sin(Life * 0.17f + Seed * 5f) * 0.12f;
            Main.EntitySpriteDraw(core, pos - new Vector2(0f, 10f * lick), null,
                (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * (0.75f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.10f, 0.19f * lick), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos - new Vector2(0f, 6f * lick), null,
                (GsSpectreArmorScheme.SpectreWhite with { A = 0 }) * (0.65f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.05f, 0.10f * lick), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 灵愈光球：自被灼者体内抽出的灵质，先上飘再折返归主，
    /// 归主即愈（佩戴者端回血 2 点）；球体灵绿双层 + 磷光尾迹
    /// </summary>
    internal class GsSpectreSoulOrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.8747f % 3.89f;

        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>纯治疗体，不伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Life > 12f) {
                //折返归主：越近越快
                Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                    * MathHelper.Clamp(6f + (Life - 12f) * 0.35f, 6f, 17f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
                //归主即愈（佩戴者端结算）
                if (Projectile.Center.Distance(owner.Center) < 24f) {
                    if (Projectile.owner == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                        owner.Heal(2);
                    }
                    Projectile.Kill();
                    return;
                }
            }
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.06f, GsSpectreArmorScheme.HealGreen,
                    Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsSpectreArmorScheme.HealGreen.ToVector3() * (0.2f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsSpectreArmorScheme.HealGreen, 0.1f)?.Configure(8, 0.7f);
        }

        //==================== 绘制：灵绿双层球 + 呼吸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float breathe = 1f + MathF.Sin(Life * 0.25f + Seed * 4f) * 0.1f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.025f, 0f, 0.3f);

            Main.EntitySpriteDraw(core, pos, null,
                (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * (0.7f * fade), Projectile.velocity.ToRotation(), core.Size() * 0.5f,
                new Vector2(0.10f + stretch, 0.085f) * breathe, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                (GsSpectreArmorScheme.HealGreen with { A = 0 }) * (0.9f * fade), Projectile.velocity.ToRotation(), core.Size() * 0.5f,
                new Vector2(0.06f + stretch * 0.5f, 0.05f) * breathe, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 幽界裂隙：撕开在目标处的一道灵界豁口，横缝撕开、暗核吞光、灵涡逆旋；
    /// 持续把周围敌人拖向隙心（无视 Boss），每 30 帧喷吐一轮魂焰脉冲，闭合时向内坍缩散出魂影
    /// </summary>
    internal class GsSpectreRiftProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.7591f % 3.49f;

        /// <summary>牵引半径</summary>
        private const float PullRadius = 260f;

        /// <summary>脉冲判定半径</summary>
        private const float PulseRadius = 120f;

        /// <summary>脉冲周期</summary>
        private const int PulseInterval = 30;

        /// <summary>撕开帧数</summary>
        private const int TearFrames = 10;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / TearFrames, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = PulseInterval;
        }

        /// <summary>只在脉冲拍短窗判定</summary>
        public override bool? CanDamage() => Life % PulseInterval < 4 && Life > TearFrames;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2())
                < PulseRadius + targetHitbox.Width * 0.25f;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //牵引：各端确定性执行（速度推移，Boss 与免击退者不受制）
            if (Life > TearFrames) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.knockBackResist <= 0f || !npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    float dist = npc.Center.Distance(Projectile.Center);
                    if (dist > PullRadius || dist < 24f) {
                        continue;
                    }
                    Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.UnitX)
                        * 0.55f * npc.knockBackResist * (1f - dist / PullRadius + 0.4f);
                    npc.velocity += pull;
                    if (npc.velocity.Length() > 7f) {
                        npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitX) * 7f;
                    }
                }
            }

            if (!Main.dedServ) {
                if (Life % PulseInterval == 0 && Life > TearFrames) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                }
                //吸入的灵尘（沿牵引方向飞向隙心）
                if (Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 at = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(70f, PullRadius * 0.8f);
                    PRTLoader.NewParticle<PRT_Spark>(at,
                        (Projectile.Center - at).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(2f, 4.5f),
                        Main.rand.NextBool(3) ? GsSpectreArmorScheme.SpectreWhite : GsSpectreArmorScheme.SpectreCyan,
                        Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(14, 24));
                }
            }
            Lighting.AddLight(Projectile.Center, GsSpectreArmorScheme.SpectreCyan.ToVector3() * (0.45f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //闭合坍缩：魂影四散
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 9; i++) {
                float ang = MathHelper.TwoPi * i / 9f;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f),
                    Main.rand.NextBool() ? GsSpectreArmorScheme.SpectreCyan : GsSpectreArmorScheme.SpectreBlue,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(false, Main.rand.Next(18, 30));
            }
            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Vector2.Zero,
                GsSpectreArmorScheme.SpectreDeep, 0.5f)?.Configure(22, 0.45f, 0.05f);
        }

        //==================== 绘制：横缝撕开 + 暗核 + 逆旋灵涡 + 脉冲环 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D swirl = CWRAsset.Cyclone?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (core == null || swirl == null || ring == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = VisualFade;
            //撕开动画：先横缝后撑圆；闭合时反向收拢
            float tear = MathHelper.Clamp(Life / TearFrames, 0f, 1f);
            float close = MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f);
            float openY = (0.16f + 0.84f * tear * tear) * close;
            float beat = Life % PulseInterval > PulseInterval - 6
                ? (Life % PulseInterval - (PulseInterval - 6)) / 6f : 0f;

            //外晕灵涡（逆旋双层）
            Main.EntitySpriteDraw(swirl, pos, null,
                (GsSpectreArmorScheme.SpectreBlue with { A = 0 }) * (0.5f * fade), -Life * 0.05f + Seed, swirl.Size() * 0.5f,
                new Vector2(1.15f, 1.15f * openY), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(swirl, pos, null,
                (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * (0.4f * fade), Life * 0.035f - Seed, swirl.Size() * 0.5f,
                new Vector2(0.8f, 0.8f * openY), SpriteEffects.None, 0);
            //隙缘冷光
            Main.EntitySpriteDraw(core, pos, null,
                (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * ((0.8f + beat * 0.2f) * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.30f, 0.30f * openY), SpriteEffects.None, 0);
            //暗核吞光（真 alpha 压暗）
            Main.EntitySpriteDraw(core, pos, null,
                GsSpectreArmorScheme.SpectreDeep * (0.95f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.22f, 0.22f * openY), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                new Color(8, 14, 20) * (0.9f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.14f, 0.15f * openY), SpriteEffects.None, 0);
            //脉冲环外扩
            if (beat > 0f) {
                Main.EntitySpriteDraw(ring, pos, null,
                    (GsSpectreArmorScheme.SpectreCyan with { A = 0 }) * ((1f - beat) * 0.7f * fade), 0f, ring.Size() * 0.5f,
                    PulseRadius * 2f * (0.4f + beat * 0.7f) / ring.Width, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
