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
    /// 【精金套·熔炉过载】炉心通红的精金重甲：①命中攒一格炉温、受击攒两格（战斗越凶炉越烫）
    /// ②满十格自动过载：以佩戴者为心喷发熔浪冲击环 ③熔浪扫过地面时落下三处精金余烬驻场灼烧。
    /// 原版套装奖励保留，神赋叠加
    /// </summary>
    internal class GsAdamantiteArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.AdamantiteHeadgear, ItemID.AdamantiteHelmet, ItemID.AdamantiteMask];

        public override int BodyID => ItemID.AdamantiteBreastplate;

        public override int LegsID => ItemID.AdamantiteLeggings;

        protected override string EndowLineFallback =>
            "Furnace Overload: strikes stoke the furnace by 1, taking hits by 2; at 10 heat it erupts into a molten shockwave that seeds burning adamantite embers on the ground";

        //精金熔红色板
        internal static readonly Color AdamantiteBright = new(255, 172, 120);
        internal static readonly Color AdamantiteMain = new(250, 84, 58);
        internal static readonly Color AdamantiteDeep = new(140, 32, 26);

        protected override int FullCharge => 10;

        protected override Color ThemeMain => AdamantiteMain;

        protected override Color ThemeBright => AdamantiteBright;

        protected override bool ChargeOnHurt => true;

        protected override int ChargePerHurt => 2;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsAdamantiteFurnaceNovaProj>()
            || proj.type == ModContent.ProjectileType<GsAdamantiteFurnaceEmberProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj == null || !IsOwnProc(sourceProj)) {
                //炉温记录近期打击力道（EndowTimer 借作伤害寄存）
                state.EndowTimer = (uint)Math.Clamp(damageDone, 1, 100000);
            }
            base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (state.EndowCharge < FullCharge) {
                return;
            }
            //满温自动过载（层数攻击方本地，只在佩戴者端触发；喷发实体跨端可见）
            state.EndowCharge = 0;
            Erupt(player, (int)state.EndowTimer);
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            //命中恰逢满温也走同一过载（时序兜底）
            Erupt(player, damageDone);
        }

        private static void Erupt(Player player, int recentDamage) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.75f, Pitch = -0.35f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
                //炉门崩开：焦渣飞溅
                for (int i = 0; i < 14; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f),
                        Main.rand.NextBool() ? AdamantiteBright : AdamantiteMain,
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(18, 30));
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int novaDamage = Math.Clamp((int)(recentDamage * 0.9f), 30, 260);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithAdamantiteEndow"),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<GsAdamantiteFurnaceNovaProj>(),
                novaDamage, 6f, player.whoAmI);
        }
    }

    /// <summary>
    /// 精金熔浪环：自佩戴者炸开的环状熔红冲击波，只在环带上判定；
    /// 扩张中途在环缘落点播种三处精金余烬（佩戴者端裁定），环体双层旋涡 + 亮缘 + 焦渣飞屑
    /// </summary>
    internal class GsAdamantiteFurnaceNovaProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>余烬是否已播（佩戴者端标记）</summary>
        private ref float EmberSeeded => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.6659f % 3.11f;

        /// <summary>扩张总帧数</summary>
        private const int ExpandFrames = 26;

        /// <summary>环最大半径</summary>
        private const float MaxRadius = 330f;

        private float RingRadius => MathF.Pow(MathHelper.Clamp(Life / ExpandFrames, 0f, 1f), 0.62f) * MaxRadius;

        private float VisualFade => MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ExpandFrames + 8;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>只在环带上判定命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            float band = 46f + targetHitbox.Width * 0.25f;
            return Math.Abs(dist - RingRadius) < band;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //环缘焦渣（客户端装饰）
            if (!Main.dedServ && Life <= ExpandFrames && Life % 2 == 0) {
                for (int i = 0; i < 3; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + ang.ToRotationVector2() * RingRadius,
                        ang.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                        Main.rand.NextBool() ? GsAdamantiteArmor.AdamantiteBright : GsAdamantiteArmor.AdamantiteMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }

            //扩张过半：环缘落点播种余烬（佩戴者端裁定生成）
            if (EmberSeeded == 0f && Life >= ExpandFrames * 0.55f) {
                EmberSeeded = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    for (int i = 0; i < 3; i++) {
                        float ang = MathHelper.Pi * (0.15f + 0.35f * i) + Seed;
                        //左右交替落点
                        float dirSign = i % 2 == 0 ? 1f : -1f;
                        Vector2 probe = Projectile.Center + new Vector2(MathF.Cos(ang) * dirSign, 0f) * RingRadius;
                        //向下探地至多 10 格
                        Point tile = probe.ToTileCoordinates();
                        bool grounded = false;
                        for (int dy = 0; dy < 10; dy++) {
                            Point at = new(tile.X, tile.Y + dy);
                            if (!WorldGen.InWorld(at.X, at.Y, 10)) {
                                break;
                            }
                            Tile t = Framing.GetTileSafely(at.X, at.Y);
                            if (t.HasTile && Main.tileSolid[t.TileType]) {
                                probe = new Vector2(at.X * 16f + 8f, at.Y * 16f - 10f);
                                grounded = true;
                                break;
                            }
                        }
                        if (!grounded) {
                            continue;
                        }
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            probe, Vector2.Zero,
                            ModContent.ProjectileType<GsAdamantiteFurnaceEmberProj>(),
                            Math.Max(8, Projectile.damage / 3), 0f, Projectile.owner);
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsAdamantiteArmor.AdamantiteMain.ToVector3() * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    GsAdamantiteArmor.AdamantiteBright, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        //==================== 绘制：双层熔环 + 内旋涡 + 亮缘 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D swirl = CWRAsset.Cyclone?.Value;
            if (ring == null || swirl == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float scale = RingRadius * 2f / ring.Width;

            //焦红外环
            Main.EntitySpriteDraw(ring, pos, null,
                (GsAdamantiteArmor.AdamantiteDeep with { A = 0 }) * (0.9f * fade), 0f, ring.Size() * 0.5f,
                scale * 1.06f, SpriteEffects.None, 0);
            //熔红主环
            Main.EntitySpriteDraw(ring, pos, null,
                (GsAdamantiteArmor.AdamantiteMain with { A = 0 }) * fade, 0f, ring.Size() * 0.5f,
                scale, SpriteEffects.None, 0);
            //亮缘
            Main.EntitySpriteDraw(ring, pos, null,
                (GsAdamantiteArmor.AdamantiteBright with { A = 0 }) * (0.65f * fade), 0f, ring.Size() * 0.5f,
                scale * 0.94f, SpriteEffects.None, 0);
            //内腔热涡（随扩张变淡）
            float swirlFade = (1f - MathHelper.Clamp(Life / ExpandFrames, 0f, 1f)) * 0.5f * fade;
            Main.EntitySpriteDraw(swirl, pos, null,
                (GsAdamantiteArmor.AdamantiteMain with { A = 0 }) * swirlFade, Life * 0.15f + Seed, swirl.Size() * 0.5f,
                RingRadius * 1.3f / swirl.Width, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 精金余烬：熔浪落点的驻场炉渣，三秒内周期灼烫踩上来的敌人；
    /// 焰体三层叠色随炉息明灭，顶部余火星升腾，燃尽时一缕黑烟收场
    /// </summary>
    internal class GsAdamantiteFurnaceEmberProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9127f % 4.73f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 10f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            if (!Main.dedServ) {
                //余火星升腾
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 6f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.8f, 1.8f)),
                        Main.rand.NextBool() ? GsAdamantiteArmor.AdamantiteBright : GsAdamantiteArmor.AdamantiteMain,
                        Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(14, 24));
                }
                //将熄时黑烟
                if (Projectile.timeLeft < 30 && Main.rand.NextBool(6)) {
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f),
                        new Vector2(0f, -0.6f), new Color(60, 45, 40), Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(24, 0.35f, 0.02f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsAdamantiteArmor.AdamantiteMain.ToVector3() * (0.35f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        //==================== 绘制：伏地炉渣火堆，炉息明灭 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (core == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //炉息明灭（identity 种子）
            float flicker = 0.8f + MathF.Sin(Life * 0.23f + Seed * 6f) * 0.12f + MathF.Sin(Life * 0.57f + Seed * 2f) * 0.08f;

            //焦渣底堆（真 alpha 压暗）
            Main.EntitySpriteDraw(core, pos + new Vector2(0f, 6f), null,
                GsAdamantiteArmor.AdamantiteDeep * (0.85f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.5f, 0.22f), SpriteEffects.None, 0);
            //熔红焰體
            Main.EntitySpriteDraw(core, pos, null,
                (GsAdamantiteArmor.AdamantiteMain with { A = 0 }) * (fade * flicker), 0f, core.Size() * 0.5f,
                new Vector2(0.34f, 0.30f + flicker * 0.05f), SpriteEffects.None, 0);
            //白热芯
            Main.EntitySpriteDraw(glow, pos + new Vector2(0f, 2f), null,
                (GsAdamantiteArmor.AdamantiteBright with { A = 0 }) * (0.7f * fade * flicker), 0f, glow.Size() * 0.5f,
                new Vector2(0.5f, 0.4f) * flicker, SpriteEffects.None, 0);
            return false;
        }
    }
}
