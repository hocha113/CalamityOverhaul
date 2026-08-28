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
    /// 【提基套·图腾祭仪】祭祀面具下的部落秘仪：①命中（含仆从）积攒祭火，满八层在脚下立起三面图腾柱八秒
    /// ②图腾自下而上逐面点睛，每多亮一面，喷向最近敌的火舌便多一道 ③祭毕图腾碎作木屑与青烟。
    /// 原版套装奖励（+1 仆从栏）保留，神赋叠加
    /// </summary>
    internal class GsTikiArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.TikiMask];

        public override int BodyID => ItemID.TikiShirt;

        public override int LegsID => ItemID.TikiPants;

        protected override string EndowLineFallback =>
            "Totem Rite: strikes build ritual fire; at 8 stacks a three-faced totem is planted for 8s, waking face by face, each lit face adding a flame tongue lashed at the nearest foe";

        //提基木 + 祭火色板
        internal static readonly Color TikiWood = new(126, 84, 46);
        internal static readonly Color TikiWoodDeep = new(72, 46, 26);
        internal static readonly Color TikiFlame = new(255, 172, 64);
        internal static readonly Color TikiGlow = new(255, 224, 124);
        internal static readonly Color TikiTeal = new(84, 202, 172);

        protected override int FullCharge => 8;

        protected override Color ThemeMain => TikiFlame;

        protected override Color ThemeBright => TikiGlow;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsTikiTotemProj>()
            || proj.type == ModContent.ProjectileType<GsTikiTotemFlameProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.6f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.1f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int type = ModContent.ProjectileType<GsTikiTotemProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    //已有图腾：续祭并重燃
                    proj.timeLeft = 480;
                    return;
                }
            }
            int flameDamage = Math.Clamp((int)(damageDone * 0.25f), 6, 100);
            //落点探地：脚下向下最多 6 格找立足
            Vector2 plant = player.Bottom;
            Point tile = plant.ToTileCoordinates();
            for (int dy = 0; dy < 6; dy++) {
                Point at = new(tile.X, tile.Y + dy);
                if (!WorldGen.InWorld(at.X, at.Y, 10)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(at.X, at.Y);
                if (t.HasTile && Main.tileSolid[t.TileType]) {
                    plant = new Vector2(at.X * 16f + 8f, at.Y * 16f);
                    break;
                }
            }
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithTikiEndow"),
                plant - new Vector2(0f, 34f), Vector2.Zero,
                ModContent.ProjectileType<GsTikiTotemProj>(),
                flameDamage, 0f, player.whoAmI);
        }
    }

    /// <summary>
    /// 提基图腾：立于大地的三面雕柱，自下而上每 60 帧点睛一面；
    /// 每 45 帧朝最近敌喷出火舌，道数等于已点亮面数；祭毕碎作木屑青烟
    /// </summary>
    internal class GsTikiTotemProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.5507f % 2.71f;

        /// <summary>已点亮面数（0~3）</summary>
        private int LitFaces => (int)MathHelper.Clamp(Life / 60f + 1f, 1f, 3f);

        /// <summary>喷焰周期</summary>
        private const int SpitInterval = 45;

        /// <summary>立柱升起帧数</summary>
        private const int RiseFrames = 12;

        private float VisualFade => MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>柱体不撞人，火舌才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //点睛拍：每亮一面燃一蓬祭火
            if (!Main.dedServ && Life % 60 == 0 && LitFaces <= 3 && Life <= 180) {
                float faceY = 22f - (LitFaces - 1) * 22f;
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), faceY),
                        -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.8f),
                        GsTikiArmor.TikiGlow, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(12, 18));
                }
            }

            //喷焰拍（佩戴者端裁定）：火舌道数 = 已亮面数
            if (Projectile.owner == Main.myPlayer && Life % SpitInterval == 0 && Life > RiseFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    for (int i = 0; i < LitFaces; i++) {
                        float faceY = 22f - i * 22f;
                        Vector2 from = Projectile.Center + new Vector2(0f, faceY);
                        Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX) * 10f;
                        //各面火舌错角
                        vel = vel.RotatedBy((i - 1) * 0.09f);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            from, vel, ModContent.ProjectileType<GsTikiTotemFlameProj>(),
                            Projectile.damage, 1f, Projectile.owner);
                    }
                    Projectile.netUpdate = true;
                }
            }

            //祭火余烬升腾（客户端装饰）
            if (!Main.dedServ && Main.rand.NextBool(7) && LitFaces >= 3) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -34f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.4f),
                    GsTikiArmor.TikiFlame, Main.rand.NextFloat(0.2f, 0.35f))?.Configure(false, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, GsTikiArmor.TikiGlow.ToVector3() * (0.16f * LitFaces * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 520f;
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
            //祭毕：木屑四溅 + 青烟一缕
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 28f),
                    DustID.WoodFurniture, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2.5f, 0.5f)));
                d.scale = Main.rand.NextFloat(0.9f, 1.3f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + new Vector2(0f, -20f + i * 12f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1f),
                    GsTikiArmor.TikiTeal, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(26, 0.35f, 0.03f);
            }
        }

        //==================== 绘制：三面雕柱，逐面点睛 ====================

        private void DrawFace(Vector2 center, float scaleMul, bool lit, float litHeat, float fade) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (core == null || glow == null || crescent == null) {
                return;
            }
            //木面基底（真 alpha 深木两层）
            Main.EntitySpriteDraw(core, center, null,
                GsTikiArmor.TikiWoodDeep * (0.95f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.20f, 0.155f) * scaleMul, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, center, null,
                GsTikiArmor.TikiWood * (0.9f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.17f, 0.13f) * scaleMul, SpriteEffects.None, 0);
            //眼与口：亮面燃祭火，暗面只余凿痕
            Color feature = lit
                ? (GsTikiArmor.TikiGlow with { A = 0 }) * (0.95f * litHeat * fade)
                : GsTikiArmor.TikiWoodDeep * (0.8f * fade);
            for (int i = -1; i <= 1; i += 2) {
                Main.EntitySpriteDraw(glow, center + new Vector2(i * 5f, -3f) * scaleMul, null,
                    feature, 0f, glow.Size() * 0.5f,
                    0.075f * scaleMul * (lit ? litHeat : 1f), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(crescent, center + new Vector2(0f, 5f) * scaleMul, null,
                feature, MathHelper.Pi, crescent.Size() * 0.5f,
                new Vector2(0.04f, 0.025f) * scaleMul, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = VisualFade;
            //升起动画：自地面拔起
            float rise = MathHelper.Clamp(Life / RiseFrames, 0f, 1f);
            rise = 1f - (1f - rise) * (1f - rise);
            Vector2 pos = Projectile.Center - Main.screenPosition + new Vector2(0f, (1f - rise) * 40f);
            //祭火呼吸（identity 相位）
            float heat = 0.8f + MathF.Sin(Life * 0.13f + Seed * 4f) * 0.2f;
            int lit = LitFaces;

            //自下而上三面（下面最大）
            for (int i = 0; i < 3; i++) {
                float y = 22f - i * 22f;
                float scaleMul = 1.05f - i * 0.12f;
                DrawFace(pos + new Vector2(MathF.Sin(Seed * 3f + i * 2f) * 1.5f, y) * rise,
                    scaleMul * rise, i < lit, heat, fade);
            }
            //顶冠祭焰（三面全亮时）
            if (lit >= 3) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Main.EntitySpriteDraw(glow, pos + new Vector2(0f, -38f) * rise, null,
                        (GsTikiArmor.TikiFlame with { A = 0 }) * (0.6f * heat * fade), 0f, glow.Size() * 0.5f,
                        new Vector2(0.32f, 0.5f) * heat, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 图腾火舌：一口卷曲的部落祭火，途中舔弧摆尾，命中挂燃并炸开火星
    /// </summary>
    internal class GsTikiTotemFlameProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9973f % 4.37f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 4f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //舔弧摆尾：轻微正弦侧摆 + 缓加速
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity += dir.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Life * 0.32f + Seed * 5f) * 0.35f;
            Projectile.velocity *= 1.012f;
            if (Projectile.velocity.Length() > 15f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 15f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f + new Vector2(0f, -0.4f),
                    Main.rand.NextBool() ? GsTikiArmor.TikiFlame : GsTikiArmor.TikiGlow,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsTikiArmor.TikiFlame.ToVector3() * (0.2f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsTikiArmor.TikiFlame : GsTikiArmor.TikiGlow,
                    Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制：卷焰体 + 舔焰残迹 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = core.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.028f, 0.05f, 0.4f);
            float lick = 1f + MathF.Sin(Life * 0.45f + Seed * 6f) * 0.14f;

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.3f * fade;
                Main.EntitySpriteDraw(core, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    (GsTikiArmor.TikiFlame with { A = 0 }) * ghost, Projectile.rotation, origin,
                    new Vector2(0.08f, 0.06f) * (1f - i * 0.14f), SpriteEffects.None, 0);
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //焰裳
            Main.EntitySpriteDraw(core, pos, null,
                (GsTikiArmor.TikiFlame with { A = 0 }) * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.12f + stretch, 0.09f) * lick, SpriteEffects.None, 0);
            //亮焰芯
            Main.EntitySpriteDraw(core, pos, null,
                (GsTikiArmor.TikiGlow with { A = 0 }) * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.065f + stretch * 0.5f, 0.045f) * lick, SpriteEffects.None, 0);
            return false;
        }
    }
}
