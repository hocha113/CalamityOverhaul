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
    /// 【禁戒套·沙魂仆影】禁忌风沙缚成的术甲：①命中积攒沙魂，满六层自风沙中唤出一具沙灵仆影（至多两具）
    /// ②仆影随行八秒，锁定最近敌人周期吐出沙旋弹 ③仆影散时化为一阵回卷沙暴。
    /// 原版套装奖励（禁忌风暴）保留，神赋叠加
    /// </summary>
    internal class GsForbiddenArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.AncientBattleArmorHat];

        public override int BodyID => ItemID.AncientBattleArmorShirt;

        public override int LegsID => ItemID.AncientBattleArmorPants;

        protected override string EndowLineFallback =>
            "Sandbound Shades: strikes build sand-soul; at 6 stacks a sand shade rises (up to 2) to haunt your side and spit whirling sand bolts";

        //禁戒沙金 + 咒能青色板
        internal static readonly Color SandBright = new(255, 228, 156);
        internal static readonly Color SandMain = new(224, 180, 98);
        internal static readonly Color SandDeep = new(132, 96, 46);
        internal static readonly Color CurseCyan = new(96, 240, 228);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => SandMain;

        protected override Color ThemeBright => SandBright;

        /// <summary>仆影上限</summary>
        private const int MaxShades = 2;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsForbiddenWraithProj>()
            || proj.type == ModContent.ProjectileType<GsForbiddenWraithBoltProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            int shades = 0;
            int type = ModContent.ProjectileType<GsForbiddenWraithProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    shades++;
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.4f }, player.Center);
                //唤影：沙涡卷起
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_Smoke>(player.Center + ang.ToRotationVector2() * 20f,
                        (ang + 1.2f).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f),
                        SandMain, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(20, 0.4f, 0.05f);
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (shades >= MaxShades) {
                //已满编：续住既有仆影
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner == player.whoAmI && proj.type == type) {
                        proj.timeLeft = Math.Max(proj.timeLeft, 480);
                    }
                }
                return;
            }
            int boltDamage = Math.Clamp((int)(damageDone * 0.35f), 8, 130);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithForbiddenEndow"),
                player.Center - new Vector2(0f, 40f), Vector2.Zero,
                ModContent.ProjectileType<GsForbiddenWraithProj>(),
                boltDamage, 0f, player.whoAmI, 0f, 0f, shades);
        }
    }

    /// <summary>
    /// 沙灵仆影：一具由风沙与咒能缚成的浮空灵体，随行佩戴者侧翼，
    /// 锁定最近敌人后周期吐出沙旋弹；身躯为沙羽双叠 + 底部沙涡 + 一对咒青灵瞳，
    /// 散时化回一阵沙暴
    /// </summary>
    internal class GsForbiddenWraithProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Fog";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>侧翼槽位（0=左 1=右）</summary>
        private ref float Slot => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.7177f % 3.43f;

        /// <summary>吐弹周期</summary>
        private const int SpitInterval = 50;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 14f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>仆影本体不撞人，沙旋弹才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //方案切走仆影散形
            if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsForbiddenArmor) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            //侧翼随行 + 沙浮呼吸
            float side = Slot == 0f ? -1f : 1f;
            Vector2 anchor = owner.Center + new Vector2(side * 74f, -44f + MathF.Sin(Life * 0.05f + Seed * 2f) * 8f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.08f);
            Projectile.velocity = Vector2.Zero;

            //锁定最近敌并周期吐沙旋弹（佩戴者端裁定）
            NPC target = FindTarget();
            if (target != null) {
                Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? 1 : -1;
                if (Projectile.owner == Main.myPlayer && Life % SpitInterval == 0) {
                    Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center, vel,
                        ModContent.ProjectileType<GsForbiddenWraithBoltProj>(),
                        Projectile.damage, 1f, Projectile.owner);
                    Projectile.netUpdate = true;
                }
            }

            //沙屑剥落（客户端装饰）
            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 20f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.4f, 1.2f)));
                d.noGravity = false;
                d.scale = 0.9f;
            }
            Lighting.AddLight(Projectile.Center, GsForbiddenArmor.CurseCyan.ToVector3() * (0.14f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 700f;
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
            //散形：回卷沙暴
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 9; i++) {
                float ang = MathHelper.TwoPi * i / 9f;
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center,
                    (ang + 1.1f).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.5f),
                    GsForbiddenArmor.SandMain, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(22, 0.45f, 0.06f);
            }
        }

        //==================== 绘制：沙羽双叠躯体 + 底部沙涡 + 咒青灵瞳 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D swirl = CWRAsset.Cyclone?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fog == null || swirl == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float sway = MathF.Sin(Life * 0.06f + Seed * 3f) * 0.08f;

            //底部沙涡（旋转承托）
            Main.EntitySpriteDraw(swirl, pos + new Vector2(0f, 22f), null,
                (GsForbiddenArmor.SandMain with { A = 0 }) * (0.5f * fade), Life * 0.09f + Seed, swirl.Size() * 0.5f,
                0.42f, SpriteEffects.None, 0);
            //沙躯下叠（宽，真 alpha 可占体积）
            Main.EntitySpriteDraw(fog, pos + new Vector2(0f, 8f), null,
                GsForbiddenArmor.SandDeep * (0.85f * fade), sway, fog.Size() * 0.5f,
                new Vector2(0.30f, 0.34f), SpriteEffects.None, 0);
            //沙躯上叠（窄，成头肩）
            Main.EntitySpriteDraw(fog, pos + new Vector2(0f, -12f), null,
                GsForbiddenArmor.SandMain * (0.9f * fade), -sway * 1.3f, fog.Size() * 0.5f,
                new Vector2(0.22f, 0.26f), SpriteEffects.None, 0);
            //一对咒青灵瞳（identity 相位眨眼）
            float blink = MathF.Sin(Life * 0.11f + Seed * 7f) > -0.92f ? 1f : 0.15f;
            int dir = Projectile.spriteDirection;
            Vector2 eyeBase = pos + new Vector2(dir * 5f, -16f);
            for (int i = 0; i < 2; i++) {
                Vector2 eye = eyeBase + new Vector2((i == 0 ? -4f : 4f) + dir * 1.5f, 0f);
                Main.EntitySpriteDraw(glow, eye, null,
                    (GsForbiddenArmor.CurseCyan with { A = 0 }) * (0.9f * blink * fade), 0f, glow.Size() * 0.5f,
                    0.09f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 沙旋弹：仆影吐出的一口旋压风沙，旋涡核 + 沙尾拖迹，命中扬起沙暴小口袋
    /// </summary>
    internal class GsForbiddenWraithBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Cyclone";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9311f % 4.07f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //出口猛、途中缓（不匀速）
            if (Life > 12f) {
                Projectile.velocity *= 0.985f;
            }
            Projectile.rotation += 0.42f;
            if (!Main.dedServ && Life % 3 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.5f,
                    DustID.Sand, -Projectile.velocity * 0.1f);
                d.noGravity = true;
                d.scale = 1f;
            }
            Lighting.AddLight(Projectile.Center, GsForbiddenArmor.SandBright.ToVector3() * (0.12f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 4 }, Projectile.Center);
            //沙暴小口袋
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f),
                    GsForbiddenArmor.SandMain, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(18, 0.4f, 0.05f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    GsForbiddenArmor.CurseCyan, Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制：旋涡核 + 沙尾 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D swirl = CWRAsset.Cyclone?.Value;
            Texture2D fog = CWRAsset.Fog?.Value;
            if (swirl == null || fog == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.3f);

            //沙尾（沿速度反向两枚渐淡）
            Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 1; i <= 2; i++) {
                Main.EntitySpriteDraw(fog, pos - back * (i * 10f), null,
                    GsForbiddenArmor.SandDeep * ((0.45f - i * 0.15f) * fade), Projectile.rotation * 0.4f, fog.Size() * 0.5f,
                    new Vector2(0.10f, 0.08f) * (1f - i * 0.2f), SpriteEffects.None, 0);
            }
            //旋涡核双层
            Main.EntitySpriteDraw(swirl, pos, null,
                (GsForbiddenArmor.SandMain with { A = 0 }) * fade, Projectile.rotation, swirl.Size() * 0.5f,
                0.22f + stretch, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(swirl, pos, null,
                (GsForbiddenArmor.SandBright with { A = 0 }) * (0.7f * fade), -Projectile.rotation * 0.7f + Seed, swirl.Size() * 0.5f,
                0.14f + stretch * 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }
}
