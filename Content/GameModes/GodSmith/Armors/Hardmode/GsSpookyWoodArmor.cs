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
    /// 【阴森木套·万圣巡灯】阴森木雕成的引魂甲：①命中（含仆从）积攒魂火，满六层点起一盏南瓜巡灯（至多两盏）
    /// ②巡灯绕主巡游八秒，锁定最近敌喷出三连鬼火 ③灯面刻脸随焰明灭，熄灯时炸作一蓬鬼绿焰。
    /// 原版套装奖励（+1 仆从栏等）保留，神赋叠加
    /// </summary>
    internal class GsSpookyWoodArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.SpookyHelmet];

        public override int BodyID => ItemID.SpookyBreastplate;

        public override int LegsID => ItemID.SpookyLeggings;

        protected override string EndowLineFallback =>
            "Hallow's Patrol: strikes build soulfire; at 6 stacks a jack-o'-lantern rises (up to 2) to patrol around you and spit triple ghostflame at the nearest foe";

        //阴森橙 + 鬼绿色板
        internal static readonly Color SpookyOrange = new(255, 152, 64);
        internal static readonly Color SpookyDeep = new(142, 62, 22);
        internal static readonly Color SpookyGreen = new(150, 255, 132);
        internal static readonly Color SpookyGlow = new(255, 224, 130);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => SpookyOrange;

        protected override Color ThemeBright => SpookyGreen;

        /// <summary>巡灯上限</summary>
        private const int MaxLanterns = 2;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsSpookyLanternProj>()
            || proj.type == ModContent.ProjectileType<GsSpookyLanternWispProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            int lanterns = 0;
            int type = ModContent.ProjectileType<GsSpookyLanternProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    lanterns++;
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.55f }, player.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(16f, 20f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f),
                        i % 2 == 0 ? SpookyGreen : SpookyOrange, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(false, Main.rand.Next(14, 22));
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (lanterns >= MaxLanterns) {
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner == player.whoAmI && proj.type == type) {
                        proj.timeLeft = Math.Max(proj.timeLeft, 480);
                    }
                }
                return;
            }
            int wispDamage = Math.Clamp((int)(damageDone * 0.30f), 8, 120);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithSpookyEndow"),
                player.Center - new Vector2(0f, 50f), Vector2.Zero,
                ModContent.ProjectileType<GsSpookyLanternProj>(),
                wispDamage, 0f, player.whoAmI, 0f, 0f, lanterns);
        }
    }

    /// <summary>
    /// 南瓜巡灯：一盏浮空的杰克南瓜灯，绕佩戴者宽轨巡游，
    /// 灯面三角眼与锯齿嘴随焰明灭；锁定最近敌后每 70 帧喷出三连鬼火，
    /// 熄灯时炸作一蓬鬼绿焰
    /// </summary>
    internal class GsSpookyLanternProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>巡游槽位（0/1 反相）</summary>
        private ref float Slot => ref Projectile.ai[2];

        /// <summary>连喷记数（>0 时每 6 帧喷一发）</summary>
        private ref float VolleyLeft => ref Projectile.localAI[0];

        private ref float VolleyTargetIndex => ref Projectile.localAI[1];

        private float Seed => Projectile.identity * 0.7717f % 3.59f;

        /// <summary>喷火周期</summary>
        private const int SpitInterval = 70;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 12f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>灯体不撞人，鬼火才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsSpookyWoodArmor) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            //宽轨巡游：慢椭圆 + 浮沉
            float ang = Life * 0.02f + Slot * MathHelper.Pi + Seed;
            Vector2 orbit = new(MathF.Cos(ang) * 92f, MathF.Sin(ang) * 54f - 40f + MathF.Sin(Life * 0.045f + Seed * 3f) * 7f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center + orbit, 0.07f);
            Projectile.velocity = Vector2.Zero;

            //锁定与三连喷（佩戴者端裁定起喷，逐发在后续帧吐出）
            if (Projectile.owner == Main.myPlayer) {
                if (Life % SpitInterval == 0) {
                    NPC target = FindTarget();
                    if (target != null) {
                        VolleyLeft = 3f;
                        VolleyTargetIndex = target.whoAmI;
                    }
                }
                if (VolleyLeft > 0f && Life % 6 == 0) {
                    VolleyLeft--;
                    NPC target = VolleyTargetIndex >= 0 && VolleyTargetIndex < Main.maxNPCs
                        ? Main.npc[(int)VolleyTargetIndex] : null;
                    if (target != null && target.active) {
                        Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10.5f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center, vel.RotatedBy(Main.rand.NextFloat(-0.08f, 0.08f)),
                            ModContent.ProjectileType<GsSpookyLanternWispProj>(),
                            Projectile.damage, 1f, Projectile.owner);
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
                        }
                    }
                }
            }

            //灯焰余滴（客户端装饰）
            if (!Main.dedServ && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), 10f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                    GsSpookyWoodArmor.SpookyOrange, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
            Lighting.AddLight(Projectile.Center, GsSpookyWoodArmor.SpookyGlow.ToVector3() * (0.3f * VisualFade));
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
            //熄灯：鬼绿焰炸开
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsSpookyWoodArmor.SpookyGreen : GsSpookyWoodArmor.SpookyOrange,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, -Vector2.UnitY * 0.5f,
                GsSpookyWoodArmor.SpookyDeep, 0.4f)?.Configure(20, 0.4f, 0.04f);
        }

        //==================== 绘制：南瓜灯体 + 棱线 + 刻脸明灭 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (core == null || glow == null || crescent == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //灯焰明灭（identity 相位）
            float flicker = 0.75f + MathF.Sin(Life * 0.19f + Seed * 5f) * 0.15f + MathF.Sin(Life * 0.53f + Seed) * 0.1f;
            float sway = MathF.Sin(Life * 0.045f + Seed * 3f) * 0.06f;

            //南瓜身（真 alpha 橙body）
            Main.EntitySpriteDraw(core, pos, null,
                GsSpookyWoodArmor.SpookyDeep * (0.95f * fade), sway, core.Size() * 0.5f,
                new Vector2(0.26f, 0.22f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                GsSpookyWoodArmor.SpookyOrange * (0.85f * fade), sway, core.Size() * 0.5f,
                new Vector2(0.22f, 0.185f), SpriteEffects.None, 0);
            //棱线两道（深色窄条竖分瓜面）
            for (int i = -1; i <= 1; i += 2) {
                Main.EntitySpriteDraw(core, pos + new Vector2(i * 7f, 0f), null,
                    GsSpookyWoodArmor.SpookyDeep * (0.5f * fade), sway, core.Size() * 0.5f,
                    new Vector2(0.03f, 0.17f), SpriteEffects.None, 0);
            }
            //内焰透光
            Main.EntitySpriteDraw(glow, pos, null,
                (GsSpookyWoodArmor.SpookyGlow with { A = 0 }) * (0.55f * flicker * fade), 0f, glow.Size() * 0.5f,
                0.5f * flicker, SpriteEffects.None, 0);
            //三角眼一对 + 锯齿嘴（加色刻脸，随焰明灭）
            Color face = (GsSpookyWoodArmor.SpookyGlow with { A = 0 }) * (0.95f * flicker * fade);
            for (int i = -1; i <= 1; i += 2) {
                Main.EntitySpriteDraw(crescent, pos + new Vector2(i * 5.5f, -4f), null,
                    face, MathHelper.PiOver2 + sway, crescent.Size() * 0.5f,
                    new Vector2(0.022f, 0.03f), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(crescent, pos + new Vector2(0f, 6f), null,
                face, MathHelper.Pi + sway, crescent.Size() * 0.5f,
                new Vector2(0.05f, 0.032f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 鬼火：巡灯喷出的一口橙芯绿焰，蛇形游进，命中炸作小蓬鬼焰
    /// </summary>
    internal class GsSpookyLanternWispProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.8629f % 3.97f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 4f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //蛇形游进：垂直于速度的正弦摆
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity += dir.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Life * 0.4f + Seed * 4f) * 0.5f;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * MathHelper.Clamp(Projectile.velocity.Length(), 9f, 12f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.06f,
                    Main.rand.NextBool() ? GsSpookyWoodArmor.SpookyGreen : GsSpookyWoodArmor.SpookyOrange,
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 13));
            }
            Lighting.AddLight(Projectile.Center, GsSpookyWoodArmor.SpookyGreen.ToVector3() * (0.22f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    Main.rand.NextBool() ? GsSpookyWoodArmor.SpookyGreen : GsSpookyWoodArmor.SpookyGlow,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制：橙芯绿焰 + 游焰残迹 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = core.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.05f, 0.4f);
            float lick = 1f + MathF.Sin(Life * 0.5f + Seed * 6f) * 0.12f;

            //游焰残迹
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.3f * fade;
                Main.EntitySpriteDraw(core, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    (GsSpookyWoodArmor.SpookyGreen with { A = 0 }) * ghost, Projectile.rotation, origin,
                    new Vector2(0.09f, 0.07f) * (1f - i * 0.12f), SpriteEffects.None, 0);
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //绿焰裳
            Main.EntitySpriteDraw(core, pos, null,
                (GsSpookyWoodArmor.SpookyGreen with { A = 0 }) * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.13f + stretch, 0.10f) * lick, SpriteEffects.None, 0);
            //橙焰芯
            Main.EntitySpriteDraw(core, pos, null,
                (GsSpookyWoodArmor.SpookyGlow with { A = 0 }) * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.07f + stretch * 0.5f, 0.05f) * lick, SpriteEffects.None, 0);
            return false;
        }
    }
}
