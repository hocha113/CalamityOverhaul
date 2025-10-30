using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Rogue;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RemakeItems.Melee;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>
    /// 正义的显现
    /// </summary>
    internal class JusticeUnveiled : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "JusticeUnveiled";
        public const int DropProbabilityDenominator = 6000;
        private static bool OnLoaden;
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 2, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.CWR().IsJusticeUnveiled = true;
            //检测换弹
            if (player.CWR().PlayerIsKreLoadTime > 0) {
                OnLoaden = true;
            }
        }

        public static bool SpwanBool(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            int type = ModContent.ProjectileType<DivineJustice>();
            int type2 = ModContent.ProjectileType<JusticeUnveiledExplode>();
            int type3 = ModContent.ProjectileType<JUZenithWorldTime>();

            if (projectile.numHits > 0) {
                return false;
            }
            if (projectile.type == type || projectile.type == type2) {
                return false;
            }

            if (!player.CWR().IsJusticeUnveiled) {
                return false;
            }
            if (player.ownedProjectileCounts[type] > 0 || player.ownedProjectileCounts[type2] > 0) {
                return false;
            }

            if (Main.zenithWorld) {
                if (player.CountProjectilesOfID(type3) == 0) {
                    Projectile.NewProjectile(player.FromObjectGetParent(), target.Center, Vector2.Zero, type3, 0, 0, player.whoAmI);
                    return true;
                }
                else {
                    return false;
                }
            }

            Item item = player.GetItem();
            if (item.type > ItemID.None && item.CWR().HasCartridgeHolder && item.CWR().AmmoCapacity <= 20) {
                if (OnLoaden) {
                    OnLoaden = false;
                    return true;
                }
            }

            if (projectile.type == ModContent.ProjectileType<StellarContemptEcho>()
                || projectile.type == ModContent.ProjectileType<GalaxySmasherEcho>()
                || projectile.type == ModContent.ProjectileType<TriactisHammerProj>()
                || projectile.type == ModContent.ProjectileType<LonginusThrow>()) {
                return true;
            }

            if (projectile.type == ModContent.ProjectileType<ExorcismProj>() && projectile.Calamity().stealthStrike) {
                return true;
            }

            if (projectile.DamageType != DamageClass.Ranged) {
                return false;
            }
            if (!hit.Crit) {
                return false;
            }
            return true;
        }

        public static void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (Main.player[projectile.owner].CWR().IsJusticeUnveiled && !Main.zenithWorld) {
                modifiers.CritDamage *= 0.5f;
            }
        }

        public static void OnHitNPCSpwanProj(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            if (!SpwanBool(player, projectile, target, hit)) {
                return;
            }

            if (Main.zenithWorld && projectile.type == ModContent.ProjectileType<LonginusThrow>()) {
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.friendly) {
                        continue;
                    }
                    Projectile.NewProjectile(player.FromObjectGetParent()
                    , npc.Center + new Vector2(0, -1120), new Vector2(0, 6)
                    , ModContent.ProjectileType<DivineJustice>(), projectile.damage, 2, player.whoAmI, npc.whoAmI);
                }
                return;
            }
            else {
                Projectile.NewProjectile(player.FromObjectGetParent()
                , target.Center + new Vector2(0, -1120), new Vector2(0, 6)
                , ModContent.ProjectileType<DivineJustice>(), projectile.damage, 2, player.whoAmI, target.whoAmI);
            }
        }
    }

    internal class JusticeUnveiledGlobalHit : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            JusticeUnveiled.OnHitNPCSpwanProj(Main.player[projectile.owner], projectile, target, hit);
        }
    }

    internal class JUZenithWorldTime : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }
        public override void AI() => Projectile.Center = Owner.GetPlayerStabilityCenter();
        public override bool PreDraw(ref Color lightColor) {
            RDefiledGreatsword.DrawRageEnergyChargeBar(Owner, 255, Projectile.timeLeft / 300f);
            return false;
        }
    }

    internal class DivineJustice : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        
        //引用高级实现案例中的优秀特效
        private readonly List<LightningBolt> lightningBolts = new();
        private float chargeIntensity = 0f;
        private int shakeTimer = 0;
        
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.timeLeft = 190;
            Projectile.extraUpdates = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanHitNPC(NPC target) {
            if (((int)Projectile.ai[0]).TryGetNPC(out var _) && target.whoAmI == Projectile.ai[0]) {
                return true;
            }
            return false;
        }

        public override void AI() {
            //蓄能阶段
            if (Projectile.timeLeft > 160) {
                float progress = (190 - Projectile.timeLeft) / 30f;
                chargeIntensity = MathHelper.Lerp(0f, 1f, CWRUtils.EaseOutCubic(progress));
                
                //生成充能粒子
                if (Main.rand.NextBool(2)) {
                    SpawnChargeParticle();
                }
                
                //生成闪电
                if (Main.rand.NextBool(5)) {
                    SpawnChargeLightning();
                }
            }
            
            //金色光芒粒子
            PRT_Spark spark = new PRT_Spark(Projectile.Center, new Vector2(0, 2), false, 22, 1.2f, 
                Color.Lerp(Color.Gold, Color.Yellow, chargeIntensity));
            PRTLoader.AddParticle(spark);
            
            //更新闪电
            for (int i = lightningBolts.Count - 1; i >= 0; i--) {
                lightningBolts[i].Update();
                if (lightningBolts[i].IsExpired()) {
                    lightningBolts.RemoveAt(i);
                }
            }
            
            //音效提示
            if (Projectile.timeLeft == 160) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { 
                    Pitch = -0.3f, 
                    Volume = 0.7f 
                }, Projectile.Center);
            }
            
            //震屏预警
            if (Projectile.timeLeft < 20 && Projectile.timeLeft > 10) {
                shakeTimer++;
                if (CWRServerConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Projectile.Center,
                        Main.rand.NextVector2Unit(),
                        2f * chargeIntensity,
                        6f,
                        5,
                        800f,
                        FullName
                    ));
                }
            }
            
            //强化照明效果
            Lighting.AddLight(Projectile.Center, 
                1.5f * chargeIntensity, 
                1.2f * chargeIntensity, 
                0.3f * chargeIntensity);
        }

        private void SpawnChargeParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(80f, 150f);
            Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
            Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * 
                               Main.rand.NextFloat(4f, 8f) * (1f + chargeIntensity);
            
            BasePRT particle = new PRT_Light(spawnPos, velocity, 
                Main.rand.NextFloat(0.6f, 1.2f), 
                Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat()), 
                25, 1, 1.5f, hueShift: 0.0f);
            PRTLoader.AddParticle(particle);
        }
        
        private void SpawnChargeLightning() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 direction = angle.ToRotationVector2();
            lightningBolts.Add(new LightningBolt(
                Projectile.Center,
                direction,
                Main.rand.Next(100, 180),
                20
            ));
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && CWRUtils.GetNPCInstance((int)Projectile.ai[0]) != null) {
                if (Main.zenithWorld) {
                    SoundEngine.PlaySound(SpearOfLonginus.AT, Projectile.Center);
                }
                else {
                    //多重音效叠加
                    SoundEngine.PlaySound(CWRSound.JustStrike with { Volume = 1.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { 
                        Pitch = -0.4f, 
                        Volume = 0.8f 
                    }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Thunder with { 
                        Pitch = 0.3f, 
                        Volume = 0.6f 
                    }, Projectile.Center);
                }

                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<JusticeUnveiledExplode>(), Projectile.damage, 2, Projectile.owner, Projectile.ai[0]);
                
                if (CWRServerConfig.Instance.ScreenVibration) {
                    PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                            Main.rand.NextVector2Unit(), 18f, 8f, 30, 1200f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }
        }
        
        public override bool PreDraw(ref Color lightColor) {
            //绘制闪电效果
            foreach (var bolt in lightningBolts) {
                bolt.Draw(Main.spriteBatch);
            }
            
            //绘制充能光环
            DrawChargeAura();
            
            return false;
        }
        
        private void DrawChargeAura() {
            if (chargeIntensity <= 0.1f) return;
            
            Texture2D glowTex = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            
            //多层光晕
            for (int i = 0; i < 4; i++) {
                float scale = (1f + i * 0.3f) * chargeIntensity * 0.8f;
                float alpha = (1f - i * 0.2f) * chargeIntensity * 0.6f;
                Color color = Color.Lerp(Color.Gold, Color.OrangeRed, i / 4f) * alpha;
                color.A = 0;
                
                Main.spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    null,
                    color,
                    Main.GlobalTimeWrappedHourly * 2f + i,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    internal class JusticeUnveiledExplode : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "JusticeUnveiledExplode";
        public const int maxFrame = 14;
        private int frameIndex = 0;
        private int time;
        private readonly List<ExplosionWave> explosionWaves = new();
        private readonly List<ImpactSpark> impactSparks = new();
        
        [VaultLoaden(CWRConstant.Masking)]
        public static Asset<Texture2D> MaskLaserLine = null;
        
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (++time < 6) {
                return;
            }
            
            //初始化爆炸特效
            if (time == 6) {
                InitializeExplosionEffects();
            }
            
            if (++Projectile.frameCounter > 3) {
                frameIndex++;
                
                //关键帧触发
                if (frameIndex == 4) {
                    TriggerMainImpact();
                }
                
                if (frameIndex == 8) {
                    TriggerSecondaryImpact();
                }
                
                if (frameIndex >= maxFrame) {
                    Projectile.Kill();
                    frameIndex = 0;
                }
                Projectile.frameCounter = 0;
            }
            
            Projectile.scale += 0.025f;

            if (Projectile.ai[1] < 4 && Projectile.ai[2] == 0) {
                Projectile.ai[1]++;
            }
            if (frameIndex > 8) {
                Projectile.ai[1] = 1;
            }
            if (Projectile.ai[2] > 0 && Projectile.ai[1] > 0) {
                Projectile.ai[1]--;
            }

            //更新特效
            UpdateExplosionEffects();
            
            //强化照明
            float lightIntensity = (float)Math.Sin(frameIndex / (float)maxFrame * MathHelper.Pi);
            Lighting.AddLight(Projectile.Center, 
                2f * lightIntensity, 
                1.5f * lightIntensity, 
                0.5f * lightIntensity);
        }
        
        private void InitializeExplosionEffects() {
            //生成初始冲击波
            for (int i = 0; i < 3; i++) {
                explosionWaves.Add(new ExplosionWave(Projectile.Center, i * 15f));
            }
            
            //生成火花效果
            for (int i = 0; i < 80; i++) {
                float angle = MathHelper.TwoPi * i / 80f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 30f);
                impactSparks.Add(new ImpactSpark(Projectile.Center, velocity));
            }
            
            //大量粒子爆发
            SpawnExplosionParticles(100);
        }
        
        private void TriggerMainImpact() {
            //主要冲击
            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                    Main.rand.NextVector2Unit(), 25f, 10f, 35, 1500f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
            
            //音效
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { 
                Volume = 1.5f, 
                Pitch = -0.3f 
            }, Projectile.Center);
            
            //额外冲击波
            for (int i = 0; i < 2; i++) {
                explosionWaves.Add(new ExplosionWave(Projectile.Center, i * 20f));
            }
            
            SpawnExplosionParticles(150);
        }
        
        private void TriggerSecondaryImpact() {
            //次级冲击
            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                    Main.rand.NextVector2Unit(), 15f, 8f, 25, 1200f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
            
            SoundEngine.PlaySound(SoundID.Item62 with { 
                Volume = 1.2f, 
                Pitch = -0.2f 
            }, Projectile.Center);
            
            SpawnExplosionParticles(80);
        }
        
        private void SpawnExplosionParticles(int count) {
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 40f);
                
                //金色火花
                BasePRT spark = new PRT_Light(
                    Projectile.Center + Main.rand.NextVector2Circular(50f, 50f),
                    velocity,
                    Main.rand.NextFloat(0.8f, 1.5f),
                    Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat()),
                    Main.rand.Next(20, 40),
                    1, 1.5f, hueShift: 0.0f
                );
                PRTLoader.AddParticle(spark);
            }
        }
        
        private void UpdateExplosionEffects() {
            //更新冲击波
            for (int i = explosionWaves.Count - 1; i >= 0; i--) {
                explosionWaves[i].Update();
                if (explosionWaves[i].ShouldRemove()) {
                    explosionWaves.RemoveAt(i);
                }
            }
            
            //更新火花
            for (int i = impactSparks.Count - 1; i >= 0; i--) {
                impactSparks[i].Update();
                if (impactSparks[i].ShouldRemove()) {
                    impactSparks.RemoveAt(i);
                }
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (!(frameIndex == 4 || frameIndex == 8)) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (time < 6) {
                return false;
            }

            //绘制冲击波
            foreach (var wave in explosionWaves) {
                wave.Draw(Main.spriteBatch);
            }
            
            //绘制火花
            foreach (var spark in impactSparks) {
                spark.Draw(Main.spriteBatch);
            }

            //绘制光柱
            DrawLightBeam();

            //绘制主体
            DrawMainExplosion(lightColor);
            
            return false;
        }
        
        private void DrawLightBeam() {
            Color drawColor = Color.Lerp(Color.Gold, Color.OrangeRed, 
                (float)Math.Sin(frameIndex / (float)maxFrame * MathHelper.Pi));
            drawColor.A = 0;
            
            //主光柱
            Main.EntitySpriteDraw(MaskLaserLine.Value, Projectile.Bottom - Main.screenPosition, null, drawColor
                , Projectile.rotation - MathHelper.PiOver2, MaskLaserLine.Value.Size() / 2
                , new Vector2(4000, Projectile.ai[1] * 0.05f * Projectile.scale), SpriteEffects.None, 0);
            
            //附加光柱增强效果
            Color accentColor = Color.Lerp(Color.Yellow, Color.White, 
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.5f + 0.5f);
            accentColor.A = 0;
            accentColor *= 0.6f;
            
            Main.EntitySpriteDraw(MaskLaserLine.Value, Projectile.Bottom - Main.screenPosition, null, accentColor
                , Projectile.rotation - MathHelper.PiOver2, MaskLaserLine.Value.Size() / 2
                , new Vector2(4000, Projectile.ai[1] * 0.03f * Projectile.scale), SpriteEffects.None, 0);
        }
        
        private void DrawMainExplosion(Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = value.GetRectangle(frameIndex, maxFrame);
            Vector2 origin = new Vector2(rectangle.Width / 2, rectangle.Height);
            
            //发光层
            Color glowColor = Color.Lerp(Color.Gold, Color.Yellow, 
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f);
            glowColor.A = 0;
            
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1f + i * 0.1f);
                float glowAlpha = (1f - i * 0.3f) * 0.5f;
                
                Main.spriteBatch.Draw(value, Projectile.Bottom - Main.screenPosition + new Vector2(0, 22 * Projectile.scale)
                    , rectangle, glowColor * glowAlpha, Projectile.rotation, origin
                    , glowScale, SpriteEffects.None, 0);
            }
            
            //主体
            Main.spriteBatch.Draw(value, Projectile.Bottom - Main.screenPosition + new Vector2(0, 22 * Projectile.scale)
                , rectangle, Color.White, Projectile.rotation, origin
                , Projectile.scale, SpriteEffects.None, 0);
        }
    }
    
    #region 特效辅助类
    
    /// <summary>
    /// 闪电效果
    /// </summary>
    internal class LightningBolt
    {
        public Vector2 StartPos;
        public Vector2 Direction;
        public float Length;
        public int MaxLife;
        public int Life;
        public List<Vector2> Points = new();

        public LightningBolt(Vector2 start, Vector2 direction, float length, int life) {
            StartPos = start;
            Direction = direction.SafeNormalize(Vector2.UnitX);
            Length = length;
            MaxLife = life;
            Life = 0;
            GeneratePoints();
        }

        private void GeneratePoints() {
            int segments = (int)(Length / 15f);
            Vector2 currentPos = StartPos;
            Points.Add(currentPos);

            for (int i = 0; i < segments; i++) {
                float segmentLength = Length / segments;
                Vector2 offset = Direction.RotatedByRandom(0.6f) * segmentLength;
                offset += Main.rand.NextVector2Circular(12f, 12f);
                currentPos += offset;
                Points.Add(currentPos);
            }
        }

        public void Update() => Life++;
        
        public bool IsExpired() => Life >= MaxLife;

        public void Draw(SpriteBatch sb) {
            float alpha = 1f - (Life / (float)MaxLife);
            if (alpha <= 0.05f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < Points.Count - 1; i++) {
                Vector2 start = Points[i];
                Vector2 end = Points[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                Color color = Color.Lerp(Color.Gold, Color.Yellow, Main.rand.NextFloat()) * alpha * 0.9f;

                sb.Draw(
                    pixel,
                    start - Main.screenPosition,
                    null,
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 4f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
    
    /// <summary>
    /// 爆炸冲击波
    /// </summary>
    internal class ExplosionWave
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius = 350f;
        public int Life;
        public int MaxLife = 40;
        public Color WaveColor;
        public float StartDelay;

        public ExplosionWave(Vector2 center, float startDelay) {
            Center = center;
            Radius = 0f;
            Life = 0;
            StartDelay = startDelay;
            WaveColor = Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat());
        }

        public void Update() {
            Life++;
            if (Life < StartDelay) return;
            
            float progress = (Life - StartDelay) / (float)MaxLife;
            Radius = MathHelper.Lerp(0f, MaxRadius, CWRUtils.EaseOutQuad(progress));
        }

        public bool ShouldRemove() => Life >= MaxLife + StartDelay;

        public void Draw(SpriteBatch sb) {
            if (Life < StartDelay) return;
            
            float progress = (Life - StartDelay) / (float)MaxLife;
            float alpha = (1f - progress) * 0.7f;
            if (alpha <= 0.05f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 72;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = Center + angle1.ToRotationVector2() * Radius;
                Vector2 p2 = Center + angle2.ToRotationVector2() * Radius;

                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                Color color = WaveColor * alpha;
                color.A = 0;

                sb.Draw(
                    pixel,
                    p1 - Main.screenPosition,
                    null,
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 5f + alpha * 4f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
    
    /// <summary>
    /// 冲击火花
    /// </summary>
    internal class ImpactSpark
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public Color SparkColor;

        public ImpactSpark(Vector2 position, Vector2 velocity) {
            Position = position;
            Velocity = velocity;
            Scale = Main.rand.NextFloat(0.8f, 1.5f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Alpha = 1f;
            Life = 0;
            MaxLife = Main.rand.Next(25, 45);
            SparkColor = Color.Lerp(Color.Gold, Color.OrangeRed, Main.rand.NextFloat());
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.96f;
            Rotation += 0.1f;
            
            float progress = Life / (float)MaxLife;
            Alpha = (float)Math.Sin((1f - progress) * MathHelper.PiOver2);
            Scale *= 0.98f;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(SpriteBatch sb) {
            Texture2D sparkTex = CWRAsset.StarTexture.Value;
            Color drawColor = SparkColor * Alpha;
            drawColor.A = 0;

            sb.Draw(
                sparkTex,
                Position - Main.screenPosition,
                null,
                drawColor,
                Rotation,
                sparkTex.Size() / 2f,
                Scale * 0.2f,
                SpriteEffects.None,
                0f
            );
        }
    }
    
    #endregion
}
