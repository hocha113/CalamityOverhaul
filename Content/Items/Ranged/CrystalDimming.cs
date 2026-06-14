using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// 冰河时代：左键冰晶炮+晶柱，右键冰河推进波
    internal class CrystalDimming : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimming";

        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 88;
            Item.height = 34;
            Item.damage = 125;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 4f;
            Item.value = Terraria.Item.buyPrice(0, 16, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CrystalDimmingHeld>();
            Item.shootSpeed = 15f;
            Item.useAmmo = AmmoID.Snowball;
            Item.crit = 6;
        }

        public override bool AltFunctionUse(Player player) => true;

        //物品不耗雪球，手持弹幕按节奏拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[Item.shoot] > 0) {
                return false;
            }
            SnowCannonPlayer state = player.GetModPlayer<SnowCannonPlayer>();
            return player.altFunctionUse == 2
                ? Main.GameUpdateCount >= state.CrystalWaveReadyTime
                : Main.GameUpdateCount >= state.CrystalShellReadyTime;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管开火逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_PridefulHuntersPlanarRipper > 0 && CWRID.Item_RuinousSoul > 0) {
                _ = CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient(CWRID.Item_PridefulHuntersPlanarRipper, 1).
                AddIngredient(CWRID.Item_RuinousSoul, 12).
                AddTile(TileID.LunarCraftingStation).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient(ItemID.LunarBar, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
            }
        }
    }

    /// 冰河时代手持：帧0-3开火 帧4待机
    internal class CrystalDimmingHeld : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimmingHeld";
        public override int TargetItemID => ModContent.ItemType<CrystalDimming>();
        protected override int FrameCount => 5;
        protected override float BarrelLength => 44f;
        protected override float MuzzleNormalOffset => -8f;
        protected override float HoldDistance => 20f;

        /// 开火余辉计时
        private int fireAnimTime;

        //开火动画播完之前不销毁，避免炮口余辉被掐断
        protected override bool PendingWork => fireAnimTime > 0;

        protected override void UpdateGun() {
            if (fireAnimTime > 0) {
                fireAnimTime--;
                VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
            }
            else {
                Projectile.frame = 4;
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (FireKeyLeft && TimeReady(GunState.CrystalShellReadyTime)) {
                FireShell();
            }

            if (FireKeyRight && TimeReady(GunState.CrystalWaveReadyTime)) {
                FireGlacierWave();
            }
        }

        /// 发射冰晶炮弹
        private void FireShell() {
            if (!PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }

            GunState.CrystalShellReadyTime = Main.GameUpdateCount + 12;
            fireAnimTime = 12;
            recoil = 6f;

            SoundEngine.PlaySound(SoundID.Item36 with { Pitch = -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.3f, Volume = 0.5f }, Projectile.Center);

            if (!Main.dedServ) {
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.BlueCrystalShard
                        , GunForward.RotatedByRandom(0.3f) * Main.rand.NextFloat(3f, 9f), 0, default, 1.2f);
                    d.noGravity = true;
                }
            }

            Vector2 velocity = GunForward.RotatedByRandom(0.02f) * 15f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                , ModContent.ProjectileType<GlacialShell>(), damage, knockback, Owner.whoAmI);
            NetUpdate();
        }

        /// 右键冰河推进波
        private void FireGlacierWave() {
            if (!PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }
            SnowCannonPlayer state = GunState;
            state.CrystalWaveReadyTime = Main.GameUpdateCount + 80;
            //释放冰河波后主炮也要缓一口气
            if (state.CrystalShellReadyTime < Main.GameUpdateCount + 25) {
                state.CrystalShellReadyTime = Main.GameUpdateCount + 25;
            }
            fireAnimTime = 15;
            recoil = 9f;

            SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.7f, Volume = 0.9f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.6f }, Owner.Center);

            int dir = Math.Sign(ToMouse.X);
            if (dir == 0) {
                dir = Owner.direction;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Bottom + new Vector2(dir * 30, -8)
                , new Vector2(dir * 7f, 0), ModContent.ProjectileType<GlacierWave>(), (int)(damage * 1.2f), knockback, Owner.whoAmI);
            NetUpdate();
        }
    }

    /// 冰晶炮弹，命中召唤晶柱
    internal class GlacialShell : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "Crystal";

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
            //飞行一段距离后轻微下坠
            if (++Projectile.ai[0] > 35) {
                Projectile.velocity.Y += 0.12f;
            }
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , -Projectile.velocity * 0.15f, 0, default, 1.1f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 240);

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 16; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(5f, 5f), 0, default, 1.4f);
                d.noGravity = true;
            }

            if (Main.myPlayer != Projectile.owner) {
                return;
            }

            //在命中点下方寻找地面拔起晶柱，悬空则改为冰晶迸射
            if (GlacierSpikeProj.TryFindGround(Projectile.Center, 30, out Vector2 ground)) {
                //邻近晶簇共鸣：附近已有晶柱越多，新柱拔得越高
                int nearby = GlacierSpikeProj.CountNearbySpikes(ground, 220f, Projectile.owner);
                float resonance = Math.Min(nearby * 0.16f, 0.5f);
                for (int i = 0; i < 2; i++) {
                    float scaleF = Main.rand.NextFloat(0.85f, 1.15f) + resonance;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), ground + new Vector2((i * 2 - 1) * Main.rand.Next(8, 26), 0)
                        , Vector2.Zero, ModContent.ProjectileType<GlacierSpikeProj>(), Projectile.damage
                        , Projectile.knockBack, Projectile.owner, Main.rand.NextFloat(10f), scaleF);
                }
            }
            else {
                for (int i = 0; i < 5; i++) {
                    Vector2 velocity = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity
                        , ModContent.ProjectileType<SnowQuayShard>(), (int)(Projectile.damage * 0.45f), 0.5f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle frame = tex.GetRectangle(Projectile.frame, 4);
            //冰晶辉光
            Main.EntitySpriteDraw(tex, drawPos, frame, new Color(120, 200, 255, 0) * 0.6f
                , Projectile.rotation, VaultUtils.GetOrig(tex, 4), Projectile.scale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, frame, Color.White
                , Projectile.rotation, VaultUtils.GetOrig(tex, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// 冰川晶柱：GlacierSpike.fx 程序化，ai0种子 ai1缩放
    internal class GlacierSpikeProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float Seed => ref Projectile.ai[0];
        private ref float ScaleF => ref Projectile.ai[1];

        private const int GrowTime = 14;
        private const int LifeTime = 110;
        private const int FadeTime = 25;

        /// 生长进度 0~1
        private float Grow {
            get {
                float t = MathHelper.Clamp((LifeTime - Projectile.timeLeft) / (float)GrowTime, 0f, 1f);
                return 1f - MathF.Pow(1f - t, 3f);
            }
        }
        /// 消散透明度
        private float Fade => MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
        private float PillarHeight => 175f * ScaleF;
        private float PillarWidth => 72f * ScaleF;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        /// 向下探测地表
        internal static bool TryFindGround(Vector2 from, int maxTiles, out Vector2 ground) {
            Point tile = from.ToTileCoordinates();
            for (int i = 0; i < maxTiles; i++) {
                int y = tile.Y + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                if (Framing.GetTileSafely(tile.X, y).HasSolidTile()) {
                    ground = new Vector2(from.X, y * 16f);
                    return true;
                }
            }
            ground = from;
            return false;
        }

        /// 统计附近晶柱数
        internal static int CountNearbySpikes(Vector2 pos, float radius, int owner) {
            int count = 0;
            int type = ModContent.ProjectileType<GlacierSpikeProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && proj.owner == owner && proj.Center.Distance(pos) < radius) {
                    count++;
                }
            }
            return count;
        }

        public override void AI() {
            if (Projectile.timeLeft == LifeTime) {
                //破土瞬间：基座迸土与冰屑
                SoundEngine.PlaySound(SoundID.Item49 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 5 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 18; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-PillarWidth, PillarWidth) * 0.4f, 0)
                        , Main.rand.NextBool() ? DustID.BlueCrystalShard : DustID.SnowBlock
                        , new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(2f, 7f)), 60, default, Main.rand.NextFloat(1.1f, 1.7f));
                    d.noGravity = Main.rand.NextBool();
                }
            }

            //晶柱中段的寒光
            float growHeight = PillarHeight * Grow;
            Lighting.AddLight(Projectile.Center - new Vector2(0, growHeight * 0.5f)
                , 0.25f * Fade, 0.5f * Fade, 0.8f * Fade);

            //崩解期的剥落冰屑
            if (Projectile.timeLeft < FadeTime && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * PillarWidth
                    , Main.rand.NextFloat(growHeight)), DustID.BlueCrystalShard
                    , new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(1f, 3f)), 0, default, 1.2f);
                d.noGravity = false;
            }
        }

        //生长期与停留前段允许造成伤害
        public override bool? CanDamage() => Projectile.timeLeft > FadeTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float growHeight = PillarHeight * Grow;
            Rectangle pillar = new Rectangle(
                (int)(Projectile.Center.X - PillarWidth * 0.28f),
                (int)(Projectile.Center.Y - growHeight),
                (int)(PillarWidth * 0.56f),
                (int)growHeight + 8);
            return pillar.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Chilled, 120);
            }
        }

        public override void OnKill(int timeLeft) {
            //崩解：沿柱身炸开冰屑
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 20; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - new Vector2(Main.rand.NextFloat(-0.35f, 0.35f) * PillarWidth
                    , Main.rand.NextFloat(PillarHeight)), DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(3.5f, 3.5f), 0, default, Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //基座辉光，柱体由 DrawPrimitives 渲染
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color baseColor = new Color(110, 190, 255, 0) * (0.55f * Fade * Grow);
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition - new Vector2(0, 6), null
                , baseColor, 0f, glow.GetOrig(), new Vector2(PillarWidth / 38f, 0.8f), SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.GlacierSpike?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Grow <= 0.01f) {
                return;
            }

            float growHeight = PillarHeight * Grow;
            float halfW = PillarWidth * 0.5f;
            Vector2 basePos = Projectile.Center + new Vector2(0, 10);
            Vector2 topPos = basePos - new Vector2(0, growHeight + 10);

            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((topPos + new Vector2(-halfW, 0)).ToVector3(), Color.White, new Vector2(0, 0));
            quad[1] = new VertexPositionColorTexture((topPos + new Vector2(halfW, 0)).ToVector3(), Color.White, new Vector2(1, 0));
            quad[2] = new VertexPositionColorTexture((basePos + new Vector2(-halfW, 0)).ToVector3(), Color.White, new Vector2(0, 1));
            quad[3] = new VertexPositionColorTexture((basePos + new Vector2(halfW, 0)).ToVector3(), Color.White, new Vector2(1, 1));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            //生长瞬间内芯最亮，随后回落
            float glowBoost = MathHelper.Clamp(1.2f - (LifeTime - Projectile.timeLeft) / 30f, 0.3f, 1f);

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(Fade);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uGlow"]?.SetValue(glowBoost);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// 冰河推进波：沿地表拔起渐高晶柱
    internal class GlacierWave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        /// 已拔起晶柱数
        private ref float SpikeCount => ref Projectile.ai[0];
        private const int MaxSpikes = 8;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
            Projectile.extraUpdates = 1;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            //贴地行进：每帧把自己吸附到地表
            if (GlacierSpikeProj.TryFindGround(Projectile.Center - new Vector2(0, 64), 12, out Vector2 ground)) {
                Projectile.Center = new Vector2(Projectile.Center.X, ground.Y - 8);

                Projectile.timeLeft = 2;
                //按节奏拔起晶柱，越往后越高
                if (++Projectile.ai[1] >= 9) {
                    Projectile.ai[1] = 0;
                    if (Main.myPlayer == Projectile.owner) {
                        float scaleF = 0.8f + SpikeCount * 0.11f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + new Vector2(0, 8)
                            , Vector2.Zero, ModContent.ProjectileType<GlacierSpikeProj>(), Projectile.damage
                            , Projectile.knockBack, Projectile.owner, Main.rand.NextFloat(10f), scaleF);
                    }

                    SpikeCount++;
                    if (SpikeCount >= 16) {
                        Projectile.Kill();
                    }
                }
            }
            else {
                //波前冲出了悬崖：向下找不到地面就坠落寻找
                Projectile.velocity.Y += 1.6f;
                if (Projectile.velocity.Y > 12f) {
                    Projectile.velocity.Y = 12f;
                }
            }

            //撞上垂直崖壁则尝试翻越，翻不过去就消散
            Point ahead = (Projectile.Center + new Vector2(Math.Sign(Projectile.velocity.X) * 20, -8)).ToTileCoordinates();
            if (Framing.GetTileSafely(ahead.X, ahead.Y).HasSolidTile()) {
                bool climbed = false;
                for (int up = 1; up <= 4; up++) {
                    if (!Framing.GetTileSafely(ahead.X, ahead.Y - up).HasSolidTile()) {
                        Projectile.position.Y -= up * 16;
                        climbed = true;
                        break;
                    }
                }
                if (!climbed) {
                    Projectile.Kill();
                    return;
                }
            }

            //行进雪尘
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), 0)
                    , DustID.SnowBlock, new Vector2(Projectile.velocity.X * 0.3f, -Main.rand.NextFloat(1f, 4f)), 80, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
