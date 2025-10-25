using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    ///万魔殿
    /// </summary>
    internal class Pandemonium : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Pandemonium";

        public override void SetStaticDefaults() {
            Item.staff[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 25;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = ModContent.RarityType<Violet>();
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
            Item.shootSpeed = 10f;
            Item.channel = true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumChannel>()] == 0;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CosmiliteBar>(15)
                .AddIngredient(ItemID.SpellTome)
                .AddIngredient(ItemID.FragmentNebula, 20)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    ///<summary>
    ///引导法阵的核心控制器
    ///</summary>
    internal class PandemoniumChannel : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];

        private ref float ChargeTimer => ref Projectile.ai[0];
        private const int MaxCharge = 240; //4秒引导，更长的蓄力时间
        private const int ScytheReleaseTime = 60;
        private const int ScytheReleaseTime2 = 120;
        private const int FireballReleaseTime = 180;
        private const int FinalBlastTime = 230;
        private const int EndTime = 240;

        private float progress => MathHelper.Clamp(ChargeTimer / MaxCharge, 0f, 1f);
        private bool releasedScythes = false;
        private bool releasedScythes2 = false;
        private bool releasedFireballs = false;
        private bool releasedFinalBlast = false;

        // 符文动画数据
        private List<RuneData> runes = new List<RuneData>();
        private List<EnergyOrbData> orbs = new List<EnergyOrbData>();

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> RuneAsset = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        private class RuneData
        {
            public Vector2 Offset;
            public float Rotation;
            public float Scale;
            public float RotationSpeed;
            public float PulsePhase;
            public int Type; // 0-5 不同符文样式
        }

        private class EnergyOrbData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
        }

        public override void SetDefaults() {
            Projectile.width = 400;
            Projectile.height = 400;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxCharge + 60;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !Owner.channel || Owner.statMana <= 0) {
                Projectile.Kill();
                return;
            }

            //持续消耗法力
            if (ChargeTimer > 1 && ChargeTimer % 8 == 0) {
                Owner.CheckMana(Owner.inventory[Owner.selectedItem], -1, true);
            }

            Projectile.Center = Owner.Center - new Vector2(0, 120);
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.heldProj = Projectile.whoAmI;

            ChargeTimer++;

            //初始化符文
            if (ChargeTimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.5f, Pitch = -0.9f }, Projectile.Center);
                InitializeRunes();
            }

            //阶段性音效
            if (ChargeTimer == ScytheReleaseTime || ChargeTimer == ScytheReleaseTime2 || ChargeTimer == FireballReleaseTime) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 1.0f, Pitch = -0.5f }, Projectile.Center);
            }

            //攻击逻辑
            if (ChargeTimer >= ScytheReleaseTime && !releasedScythes) {
                ReleaseScytheWave(1);
                releasedScythes = true;
            }

            if (ChargeTimer >= ScytheReleaseTime2 && !releasedScythes2) {
                ReleaseScytheWave(2);
                releasedScythes2 = true;
            }

            if (ChargeTimer >= FireballReleaseTime && !releasedFireballs) {
                ReleaseFireballBarrage();
                releasedFireballs = true;
            }

            if (ChargeTimer >= FinalBlastTime && !releasedFinalBlast) {
                ReleaseFinalBlast();
                releasedFinalBlast = true;
            }

            if (ChargeTimer >= EndTime) {
                Projectile.Kill();
            }

            //更新符文动画
            UpdateRunes();
            //生成能量球粒子
            SpawnEnergyOrbs();
            //更新能量球
            UpdateEnergyOrbs();
            //生成其他粒子效果
            SpawnChargeParticles();

            //动态照明
            float light = progress * 4f;
            float flicker = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 2.0f * light * flicker, 0.3f * light * flicker, 0.6f * light * flicker);
            
            //屏幕震动
            if (progress > 0.7f) {
                Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue = progress * 2f;
            }
        }

        private void InitializeRunes() {
            runes.Clear();
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                float distance = 200f + Main.rand.NextFloat(-30f, 30f);
                runes.Add(new RuneData {
                    Offset = angle.ToRotationVector2() * distance,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Scale = Main.rand.NextFloat(0.4f, 0.7f),
                    RotationSpeed = Main.rand.NextFloat(-0.02f, 0.02f),
                    PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Type = Main.rand.Next(6)
                });
            }
        }

        private void UpdateRunes() {
            float time = Main.GlobalTimeWrappedHourly;
            foreach (var rune in runes) {
                rune.Rotation += rune.RotationSpeed * (1f + progress);
                rune.PulsePhase += 0.1f;
                
                // 符文向中心移动
                rune.Offset = Vector2.Lerp(rune.Offset, rune.Offset.SafeNormalize(Vector2.Zero) * 180f, 0.02f);
                
                // 随机扰动
                if (Main.rand.NextBool(60)) {
                    rune.Offset += Main.rand.NextVector2Circular(10f, 10f);
                }
            }
        }

        private void SpawnEnergyOrbs() {
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(300f, 450f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                
                orbs.Add(new EnergyOrbData {
                    Position = spawnPos,
                    Velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 7f),
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(60f, 90f),
                    Color = Main.rand.Next(3) switch {
                        0 => new Color(255, 50, 80),
                        1 => new Color(180, 20, 120),
                        _ => new Color(80, 10, 60)
                    }
                });
            }
        }

        private void UpdateEnergyOrbs() {
            for (int i = orbs.Count - 1; i >= 0; i--) {
                var orb = orbs[i];
                orb.Life++;
                orb.Position += orb.Velocity;
                orb.Velocity = Vector2.Lerp(orb.Velocity, (Projectile.Center - orb.Position).SafeNormalize(Vector2.Zero) * 8f, 0.05f);
                
                if (orb.Life > orb.MaxLife || Vector2.Distance(orb.Position, Projectile.Center) < 30f) {
                    // 汇聚到中心时产生小爆炸效果
                    for (int j = 0; j < 3; j++) {
                        Dust d = Dust.NewDustPerfect(orb.Position, DustID.Vortex, Main.rand.NextVector2Circular(2f, 2f), 100, orb.Color, 0.8f);
                        d.noGravity = true;
                    }
                    orbs.RemoveAt(i);
                }
            }
        }

        private void ReleaseScytheWave(int wave) {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.2f, Pitch = -0.6f }, Projectile.Center);
            if (Owner.whoAmI == Main.myPlayer) {
                int scytheCount = wave == 1 ? 24 : 36;
                float speedBase = wave == 1 ? 14f : 18f;
                
                for (int i = 0; i < scytheCount; i++) {
                    float angle = MathHelper.TwoPi * i / scytheCount;
                    // 螺旋发射模式
                    float spiralOffset = (float)Math.Sin(i * 0.5f) * 0.3f;
                    Vector2 velocity = angle.ToRotationVector2().RotatedBy(spiralOffset) * speedBase;
                    
                    int damage = wave == 1 ? Projectile.damage : (int)(Projectile.damage * 1.2f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                        ModContent.ProjectileType<PandemoniumScythe>(), damage, Projectile.knockBack, Owner.whoAmI, wave);
                }
            }
        }

        private void ReleaseFireballBarrage() {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 1.5f, Pitch = -0.4f }, Projectile.Center);
            if (Owner.whoAmI == Main.myPlayer) {
                int fireballCount = 16;
                for (int i = 0; i < fireballCount; i++) {
                    float delay = i * 3f; // 延迟发射
                    Vector2 targetPos = Main.MouseWorld + Main.rand.NextVector2Circular(120f, 120f);
                    Vector2 dir = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 0.1f,
                        ModContent.ProjectileType<PandemoniumFireball>(), (int)(Projectile.damage * 1.5f), Projectile.knockBack, Owner.whoAmI, delay);
                }
            }
        }

        private void ReleaseFinalBlast() {
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1.5f, Pitch = -0.7f }, Projectile.Center);
            if (Owner.whoAmI == Main.myPlayer) {
                // 终极爆炸波
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<PandemoniumBlastWave>(), (int)(Projectile.damage * 2f), Projectile.knockBack * 2f, Owner.whoAmI);
            }
        }

        private void SpawnChargeParticles() {
            if (Main.rand.NextBool(1)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(250f, 400f) * progress;
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f);

                Dust d = Dust.NewDustPerfect(spawnPos, DustID.Vortex, velocity, 100, Color.Red, Main.rand.NextFloat(1.2f, 2.0f));
                d.noGravity = true;
                d.color = Color.Lerp(new Color(255, 50, 80), new Color(100, 10, 50), Main.rand.NextFloat());
            }

            // 内圈快速旋转粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.GlobalTimeWrappedHourly * 5f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * (80f * progress);
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, Vector2.Zero, 100, Color.Purple, 1.0f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float p = progress;
            float time = Main.GlobalTimeWrappedHourly;

            Color coreColor = new Color(255, 200, 180);
            Color midColor = new Color(255, 80, 60);
            Color edgeColor = new Color(180, 20, 40);
            Color darkColor = new Color(100, 10, 30);
            Color voidColor = new Color(60, 5, 20);

            // 绘制外层暗影光环
            DrawVoidRing(sb, center, 450f * p, voidColor, p, time);
            DrawVoidRing(sb, center, 400f * p, darkColor, p, time * 0.8f);

            // 绘制多层复杂法阵
            DrawComplexRing(sb, center, 380f * p, 14f, edgeColor, p, time * 1.8f, 24);
            DrawComplexRing(sb, center, 340f * p, 10f, midColor, p, time * -2.2f, 18);
            DrawComplexRing(sb, center, 300f * p, 8f, edgeColor, p, time * 2.6f, 16);
            DrawComplexRing(sb, center, 260f * p, 6f, darkColor, p, time * -3.0f, 12);
            DrawComplexRing(sb, center, 220f * p, 8f, midColor, p, time * 3.5f, 14);
            DrawComplexRing(sb, center, 180f * p, 5f, coreColor, p, time * -4.0f, 10);
            DrawComplexRing(sb, center, 140f * p, 4f, edgeColor, p, time * 4.5f, 8);

            // 绘制连接线网络
            DrawConnectionWeb(sb, center, 320f * p, p, edgeColor, time);

            // 绘制多重六芒星
            DrawHexagram(sb, center, 280f * p, 7f, Color.Lerp(darkColor, midColor, p), time * 1.2f);
            DrawHexagram(sb, center, 240f * p, 5f, Color.Lerp(midColor, coreColor, p), time * -1.6f);
            DrawHexagram(sb, center, 200f * p, 4f, edgeColor * p, time * 2.0f);

            // 绘制五芒星
            DrawPentagram(sb, center, 260f * p, 6f, midColor * p, -time * 1.4f);
            DrawPentagram(sb, center, 160f * p, 3f, coreColor * p, time * 2.2f);

            // 绘制符文（使用动态数据）
            if (RuneAsset?.IsLoaded ?? false) {
                DrawAnimatedRunes(sb, RuneAsset.Value, center, p, coreColor, edgeColor, darkColor);
            }

            // 绘制能量球
            DrawEnergyOrbs(sb, center);

            // 绘制中心核心辉光
            if (GlowAsset?.IsLoaded ?? false) {
                DrawCoreGlow(sb, GlowAsset.Value, center, p, coreColor, midColor, edgeColor, time);
            }

            // 绘制闪电效果
            if (p > 0.5f) {
                DrawLightningArcs(sb, center, 280f * p, p, midColor, time);
            }

            return false;
        }

        private void DrawVoidRing(SpriteBatch sb, Vector2 center, float radius, Color color, float p, float time) {
            if (GlowAsset?.IsLoaded ?? false) {
                float pulse = (float)Math.Sin(time * 3f) * 0.3f + 0.7f;
                sb.Draw(GlowAsset.Value, center, null, color with { A = 0 } * p * 0.3f * pulse, time, 
                    GlowAsset.Value.Size() / 2, radius / GlowAsset.Value.Width * 2f, 0, 0);
            }
        }

        private void DrawComplexRing(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float p, float rotation, int segments) {
            if (p <= 0) return;
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            
            for (int i = 0; i < segments; i++) {
                float angle = rotation + MathHelper.TwoPi * i / segments;
                float nextAngle = rotation + MathHelper.TwoPi * (i + 1) / segments;
                
                // 动态脉冲
                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 0.5f) * 0.4f + 0.6f;
                float segmentLength = (MathHelper.TwoPi * radius / segments) * (0.7f + pulse * 0.2f);
                
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                Color segmentColor = Color.Lerp(color, color * 0.5f, (float)Math.Sin(i * 0.8f + rotation * 2f) * 0.5f + 0.5f);
                
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), segmentColor * p * pulse, angle + MathHelper.PiOver2, 
                    new Vector2(0.5f, 0.5f), new Vector2(thickness * pulse, segmentLength), SpriteEffects.None, 0f);
            }
        }

        private void DrawConnectionWeb(SpriteBatch sb, Vector2 center, float radius, float p, Color color, float time) {
            if (p < 0.3f) return;
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            
            int points = 12;
            for (int i = 0; i < points; i++) {
                float angle1 = time * 2f + MathHelper.TwoPi * i / points;
                Vector2 pos1 = center + angle1.ToRotationVector2() * radius;
                
                // 连接到相邻点
                for (int j = i + 1; j < Math.Min(i + 4, points); j++) {
                    float angle2 = time * 2f + MathHelper.TwoPi * j / points;
                    Vector2 pos2 = center + angle2.ToRotationVector2() * radius;
                    
                    float pulse = (float)Math.Sin(time * 10f + i + j) * 0.5f + 0.5f;
                    DrawLine(sb, pixel, pos1, pos2, 1f, color * p * 0.3f * pulse);
                }
            }
        }

        private void DrawHexagram(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            DrawPolygon(sb, pixel, center, 3, radius, thickness, color, rotation);
            DrawPolygon(sb, pixel, center, 3, radius, thickness, color, rotation + MathHelper.Pi);
        }

        private void DrawPentagram(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int points = 5;
            Vector2[] vertices = new Vector2[points];
            
            for (int i = 0; i < points; i++) {
                float angle = rotation + i * MathHelper.TwoPi / points - MathHelper.PiOver2;
                vertices[i] = center + angle.ToRotationVector2() * radius;
            }
            
            // 绘制五芒星的连接线
            for (int i = 0; i < points; i++) {
                DrawLine(sb, pixel, vertices[i], vertices[(i + 2) % points], thickness, color);
            }
        }

        private void DrawPolygon(SpriteBatch sb, Texture2D pixel, Vector2 center, int sides, float radius, float thickness, Color col, float rot) {
            if (sides < 3) return;
            Vector2 prev = center + (rot).ToRotationVector2() * radius;
            for (int i = 1; i <= sides; i++) {
                float ang = rot + i * MathHelper.TwoPi / sides;
                Vector2 curr = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, pixel, prev, curr, thickness, col);
                prev = curr;
            }
        }

        private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color col) {
            Vector2 diff = end - start;
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), col, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), thickness), SpriteEffects.None, 0f);
        }

        private void DrawAnimatedRunes(SpriteBatch sb, Texture2D runeTex, Vector2 center, float p, Color c1, Color c2, Color c3) {
            foreach (var rune in runes) {
                Vector2 pos = center + rune.Offset * p;
                float pulse = (float)Math.Sin(rune.PulsePhase) * 0.5f + 0.5f;
                
                Color runeColor = rune.Type switch {
                    0 => Color.Lerp(c1, c2, pulse),
                    1 => Color.Lerp(c2, c3, pulse),
                    2 => Color.Lerp(c3, c1, pulse),
                    3 => c1 * pulse,
                    4 => c2 * pulse,
                    _ => c3 * pulse
                };
                
                runeColor *= p * (0.6f + pulse * 0.4f);
                runeColor.A = 0;
                
                float finalScale = rune.Scale * p * (0.8f + pulse * 0.4f);
                sb.Draw(runeTex, pos, null, runeColor, rune.Rotation, runeTex.Size() / 2f, finalScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawEnergyOrbs(SpriteBatch sb, Vector2 center) {
            if (!(GlowAsset?.IsLoaded ?? false)) return;
            
            foreach (var orb in orbs) {
                Vector2 drawPos = orb.Position - Main.screenPosition;
                float lifeRatio = 1f - (orb.Life / orb.MaxLife);
                float scale = lifeRatio * 0.3f;
                
                sb.Draw(GlowAsset.Value, drawPos, null, orb.Color with { A = 0 } * lifeRatio, 0, 
                    GlowAsset.Value.Size() / 2, scale, 0, 0);
            }
        }

        private void DrawCoreGlow(SpriteBatch sb, Texture2D glow, Vector2 center, float p, Color c1, Color c2, Color c3, float time) {
            float pulse1 = (float)Math.Sin(time * 15f) * 0.5f + 0.5f;
            float pulse2 = (float)Math.Sin(time * 12f + 1f) * 0.5f + 0.5f;
            float pulse3 = (float)Math.Sin(time * 18f + 2f) * 0.5f + 0.5f;

            sb.Draw(glow, center, null, c3 with { A = 0 } * p * 0.6f, time * 2f, glow.Size() / 2, p * 2.5f, 0, 0);
            sb.Draw(glow, center, null, c2 with { A = 0 } * p * 0.8f * pulse1, -time * 1.5f, glow.Size() / 2, p * (1.8f + pulse1 * 0.4f), 0, 0);
            sb.Draw(glow, center, null, c1 with { A = 0 } * p * pulse2, time, glow.Size() / 2, p * (1.2f + pulse2 * 0.5f), 0, 0);
            sb.Draw(glow, center, null, Color.White with { A = 0 } * p * 0.4f * pulse3, 0, glow.Size() / 2, p * 0.8f * (1f + pulse3 * 0.3f), 0, 0);
        }

        private void DrawLightningArcs(SpriteBatch sb, Vector2 center, float radius, float p, Color color, float time) {
            if (Main.rand.NextBool(3)) {
                Texture2D pixel = CWRAsset.Placeholder_White.Value;
                int arcCount = 8;
                for (int i = 0; i < arcCount; i++) {
                    if (Main.rand.NextBool(2)) {
                        float angle = time * 3f + MathHelper.TwoPi * i / arcCount;
                        Vector2 start = center;
                        Vector2 end = center + angle.ToRotationVector2() * radius;
                        
                        DrawLine(sb, pixel, start, end, 2f, color * p * 0.6f);
                    }
                }
            }
        }
    }

    ///<summary>
    ///深渊血色镰刀 - 增强版
    ///</summary>
    internal class PandemoniumScythe : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BalefulSickle";

        private NPC target;
        private float searchCooldown = 0;
        private ref float Wave => ref Projectile.ai[0];
        private ref float TrailTimer => ref Projectile.ai[1];

        // 拖尾效果
        private Vector2[] oldPositions = new Vector2[20];
        private float[] oldRotations = new float[20];

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 360;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            // 初始化拖尾数组
            if (TrailTimer == 0) {
                for (int i = 0; i < oldPositions.Length; i++) {
                    oldPositions[i] = Projectile.Center;
                }
            }
            TrailTimer++;

            // 更新拖尾位置
            for (int i = oldPositions.Length - 1; i > 0; i--) {
                oldPositions[i] = oldPositions[i - 1];
                oldRotations[i] = oldRotations[i - 1];
            }
            oldPositions[0] = Projectile.Center;
            oldRotations[0] = Projectile.rotation;

            // 螺旋运动
            float spiralAmount = (float)Math.Sin(TrailTimer * 0.1f) * 2f;
            Projectile.velocity = Projectile.velocity.RotatedBy(spiralAmount * 0.02f);

            Projectile.rotation += 0.5f * Math.Sign(Projectile.velocity.X != 0 ? Projectile.velocity.X : 1);

            //寻找目标
            if (searchCooldown <= 0) {
                target = FindTarget();
                searchCooldown = 12;
            }
            else {
                searchCooldown--;
            }

            //追踪目标（第二波更强）
            if (target != null && target.active && !target.dontTakeDamage) {
                Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float homingStrength = Wave == 1 ? 0.08f : 0.12f;
                float targetSpeed = Wave == 1 ? 18f : 22f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * targetSpeed, homingStrength);
            }
            else {
                Projectile.velocity *= 0.99f;
            }

            //增强粒子效果
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * -0.15f, 100, default, 1.5f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(2f, 2f), 100, Color.DarkRed, 0.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1.0f, 0.2f, 0.3f);
        }

        private NPC FindTarget() {
            NPC closest = null;
            float maxDist = 900f;
            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy(this)) {
                    float dist = Projectile.Distance(npc.Center);
                    if (dist < maxDist) {
                        maxDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 240);
            target.AddBuff(BuffID.ShadowFlame, 180);
            
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.position);

            // 命中爆发效果
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, vel, 100, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            // 绘制拖尾
            for (int i = 1; i < oldPositions.Length; i++) {
                float progress = 1f - (i / (float)oldPositions.Length);
                Color trailColor = Color.Lerp(new Color(180, 20, 40, 0), new Color(255, 80, 60, 0), progress) * progress * 0.8f;
                float trailScale = Projectile.scale * progress * 1.2f;
                
                sb.Draw(texture, oldPositions[i] - Main.screenPosition, null, trailColor, oldRotations[i], 
                    texture.Size() / 2, trailScale, SpriteEffects.None, 0f);
            }

            // 绘制主体
            Color mainColor = Projectile.GetAlpha(lightColor);
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, 
                texture.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);

            // 绘制辉光
            Color glowColor = new Color(255, 100, 80, 0) * 0.8f;
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, 
                texture.Size() / 2, Projectile.scale * 1.1f, SpriteEffects.None, 0f);

            return false;
        }
    }

    ///<summary>
    ///混沌魔能火球 - 增强版
    ///</summary>
    internal class PandemoniumFireball : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool exploded = false;
        private ref float DelayTimer => ref Projectile.ai[0];
        private bool initialized = false;
        private Vector2 targetVelocity;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (!initialized && DelayTimer > 0) {
                Projectile.velocity = Vector2.Zero;
                DelayTimer--;
                
                // 延迟期间的充能效果
                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f), 
                        DustID.Torch, Vector2.Zero, 100, Color.OrangeRed, 1.2f);
                    d.noGravity = true;
                }
                
                return;
            }

            if (!initialized) {
                initialized = true;
                targetVelocity = Projectile.velocity;
            }

            // 加速发射
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity.SafeNormalize(Vector2.Zero) * 15f, 0.1f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 增强拖尾效果
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Projectile.velocity.SafeNormalize(Vector2.Zero) * -i * 8f;
                Color dustColor = Color.Lerp(Color.Red, Color.Orange, i / 3f);
                var d = Dust.NewDustPerfect(Projectile.Center + offset, DustID.AncientLight, Vector2.Zero, 100, dustColor, 2.0f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            // 火焰粒子
            if (Main.rand.NextBool(1)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, 
                    Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1.5f, 0.8f, 0.4f);
        }

        public override void OnKill(int timeLeft) {
            if (exploded) return;
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (exploded) return;
            Explode();
        }

        private void Explode() {
            if (exploded) return;
            exploded = true;

            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = 300;
            Projectile.Center = Projectile.position;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

            //强化爆炸粒子
            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, 
                    new Color(255, Main.rand.Next(80, 150), 40), Main.rand.NextFloat(2.0f, 3.5f));
                d.noGravity = true;
            }
            
            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 150, 
                    Color.DarkGray, Main.rand.NextFloat(2.0f, 3.0f));
                d.noGravity = true;
            }

            // 火环效果
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 12f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, Color.OrangeRed, 2.0f);
                d.noGravity = true;
            }

            Projectile.active = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!initialized && DelayTimer > 0) return false;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = (float)Math.Sin(time * 25f) * 0.5f + 0.5f;

            Color c1 = new Color(255, 240, 200, 0);
            Color c2 = new Color(255, 150, 60, 0);
            Color c3 = new Color(255, 80, 30, 0);
            Color c4 = new Color(180, 30, 20, 0);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.Draw(glow, drawPos, null, c4 * 0.6f, 0, glow.Size() / 2, Projectile.scale * 2.5f, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c3 * 0.8f, time * 2f, glow.Size() / 2, Projectile.scale * 2.0f, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c2, -time * 3f, glow.Size() / 2, Projectile.scale * (1.5f + pulse * 0.3f), 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c1, time * 4f, glow.Size() / 2, Projectile.scale * (1.0f + pulse * 0.5f), 0, 0);

            return false;
        }
    }

    ///<summary>
    ///终极爆炸波
    ///</summary>
    internal class PandemoniumBlastWave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float ExpandTimer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            ExpandTimer++;
            float progress = ExpandTimer / 60f;
            
            // 快速扩张
            Projectile.scale = progress * 15f;
            Projectile.width = Projectile.height = (int)(50 + progress * 600f);

            // 粒子效果
            if (Main.rand.NextBool(1)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = progress * 400f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * distance;
                
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, angle.ToRotationVector2() * 5f, 
                    100, Color.OrangeRed, 2.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 3.0f * progress, 1.0f * progress, 0.5f * progress);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            target.AddBuff(BuffID.Ichor, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!(CWRAsset.SoftGlow?.IsLoaded ?? false)) return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float progress = ExpandTimer / 60f;
            float alpha = 1f - progress;

            Color c1 = new Color(255, 200, 100, 0) * alpha;
            Color c2 = new Color(255, 100, 50, 0) * alpha * 0.8f;
            Color c3 = new Color(200, 50, 30, 0) * alpha * 0.6f;

            sb.Draw(glow, center, null, c3, 0, glow.Size() / 2, Projectile.scale * 1.2f, 0, 0);
            sb.Draw(glow, center, null, c2, 0, glow.Size() / 2, Projectile.scale, 0, 0);
            sb.Draw(glow, center, null, c1, 0, glow.Size() / 2, Projectile.scale * 0.7f, 0, 0);

            return false;
        }
    }
}
