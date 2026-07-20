using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Granites
{
    /// <summary>
    /// 花岗之花：抛出水晶种子，落点绽放成驻场能量花，
    /// 朝最近的敌人定向脉冲喷射花瓣碎片，凋谢时花瓣碎裂成一圈晶屑
    /// </summary>
    internal class GraniteFlower : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 38;
            Item.damage = 16;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 12;
            Item.useTime = Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 3f;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GraniteFlowerHeld>();
            Item.shootSpeed = 11f;
            Item.value = Item.sellPrice(0, 0, 75, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<GraniteFlowerHeld>()] <= 0;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Granite, 22)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                .AddIngredient(ItemID.FallenStar, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 持杖施法体：位置由锚定驱动（不吃速度积分），发种子前可跟随鼠标微调瞄准
    /// </summary>
    internal class GraniteFlowerHeld : BaseHeldProj
    {
        public override string Texture => GraniteMarbleVFX.GraniteTex + "GraniteFlower";
        private Vector2 aim = Vector2.UnitX;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.friendly = false;
        }

        //位置全程由 AI 直接赋值，禁止速度积分产生逐帧漂移
        public override bool ShouldUpdatePosition() => false;

        public override void OnSpawn(IEntitySource source) {
            aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = Vector2.Zero;
        }

        public override void AI() {
            SetHeld();
            Projectile.velocity = Vector2.Zero;
            int duration = Owner.itemAnimationMax;
            if (duration < 1) {
                duration = 32;
            }
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }

            //发种子前跟随鼠标微调方向（ToMouse 由基类同步，远端同样生效）
            if (Projectile.ai[0] == 0f && ToMouse != Vector2.Zero) {
                aim = UnitToMouseV;
            }

            float life = 1f - Projectile.timeLeft / (float)duration;
            float thrust = MathF.Sin(life * MathHelper.Pi) * 10f;

            Projectile.Center = Owner.GetPlayerStabilityCenter() + aim * (24f + thrust);
            Projectile.rotation = aim.ToRotation();
            SetDirection();

            //蓄势期能量在杖尖汇聚
            if (Projectile.ai[0] == 0f && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 tip = Projectile.Center + aim * 26f;
                Vector2 from = tip + Main.rand.NextVector2CircularEdge(18f, 18f);
                PRTLoader.NewParticle<PRT_Light>(from, from.To(tip) * 0.1f
                    , GraniteMarbleVFX.GraniteCore, 0.26f).Configure(12, 1f, 1.2f);
            }

            if (Projectile.ai[0] == 0f && life >= 0.4f) {
                Projectile.ai[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item43 with { Pitch = -0.1f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    //初速加向上分量，配合种子自重形成上抛弧线
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + aim * 30f
                        , aim * 11f - Vector2.UnitY * 2.4f, ModContent.ProjectileType<GraniteFlowerSeed>()
                        , Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Light>(Projectile.Center + aim * 26f
                            , aim.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f)
                            , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.5f)).Configure(16, 1f, 1.2f);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation + MathHelper.PiOver4;
            Main.EntitySpriteDraw(tex, pos, null, Projectile.GetAlpha(lightColor), rot
                , tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 水晶种子：上抛弧线飞行，命中 / 触地 / 超时后绽放成能量花；
    /// 撞墙时绽放点沿来向反推出墙体，避免花体嵌墙
    /// </summary>
    internal class GraniteFlowerSeed : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private Trail Trail;
        private Vector2 bloomSpot;
        private bool hasBloomSpot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //自重下坠拼出抛物弧线
            if (Projectile.velocity.Y < 12f) {
                Projectile.velocity.Y += 0.14f;
            }
            Projectile.rotation += 0.24f * (Projectile.velocity.X >= 0f ? 1f : -1f);
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.8f);
            if (Main.rand.NextBool(2) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.1f
                    , GraniteMarbleVFX.GraniteCore, 0.3f).Configure(14, 1f, 1.2f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //绽放点沿撞击反方向退出墙体，直到花体包围盒不再嵌入实体物块
            Vector2 back = -oldVelocity.SafeNormalize(Vector2.UnitY);
            Vector2 spot = Projectile.Center + back * 12f;
            for (int i = 0; i < 8 && Collision.SolidCollision(spot - new Vector2(20f), 40, 40); i++) {
                spot += back * 8f;
            }
            bloomSpot = spot;
            hasBloomSpot = true;
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.3f }, Projectile.Center);
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 spawnAt = hasBloomSpot ? bloomSpot : Projectile.Center;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnAt, Vector2.Zero
                    , ModContent.ProjectileType<GraniteBloom>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
        }

        public float GetWidthFunc(float c) {
            //种子电弧细丝：中段半宽上限 7px，贴 16px 种子体
            float p = c > 0.5f ? 1f - c : c;
            return p * 2f * Projectile.scale * 7f;
        }

        public Color GetColorFunc(Vector2 _) => Color.White * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            GraniteMarbleVFX.DrawGraniteArcTrailFromOldPos(Projectile, ref Trail, GetWidthFunc, GetColorFunc);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //种子形体：翻滚的小水晶（主晶面+斜切副晶面+白芯），不再是裸光斑
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D sliver = CWRAsset.Line.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = sliver.Size() / 2f;
            float rot = Projectile.rotation;

            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            Color spark = GraniteMarbleVFX.GraniteSpark; spark.A = 0;

            spriteBatch.Draw(glow, pos, null, deep * 0.8f, 0f, glow.Size() / 2f, Projectile.scale * 0.6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, core * 0.75f, rot + 1.2f, origin, new Vector2(0.08f, 0.05f) * Projectile.scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, spark * 0.95f, rot, origin, new Vector2(0.10f, 0.07f) * Projectile.scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, Color.White * 0.8f, rot, origin, new Vector2(0.05f, 0.055f) * Projectile.scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 驻场能量花：开花 16t → 3 次定向脉冲（每 44t，朝最近敌人扇形迸射花瓣碎片）→
    /// 凋谢 20t 后花瓣碎裂成一圈晶片；全程对范围内敌人保持接触伤害
    /// </summary>
    internal class GraniteBloom : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int OpenTime = 16;
        private const int PulseInterval = 44;
        private const int MaxPulses = 3;
        private const int WitherTime = 20;
        private const int WitherStart = PulseInterval * MaxPulses;
        private const int Life = WitherStart + WitherTime;
        private const int PetalCount = 6;

        //脉冲收张动画计时（1→0），仅本地视觉
        private float pulseAnim;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 24;
        }

        private int Elapsed => Life - Projectile.timeLeft;

        private float Open => MathHelper.Clamp(Elapsed / (float)OpenTime, 0f, 1f);

        private float WitherFade => Elapsed <= WitherStart ? 1f
            : MathHelper.Clamp(Projectile.timeLeft / (float)WitherTime, 0f, 1f);

        //下次脉冲前 14t 蓄势（核心增亮、花瓣内收），脉冲打完恒为 0
        private float Charge {
            get {
                if (Projectile.ai[1] >= MaxPulses) {
                    return 0f;
                }
                float ticksTo = PulseInterval * (Projectile.ai[1] + 1f) - Elapsed;
                return MathHelper.Clamp(1f - ticksTo / 14f, 0f, 1f);
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            //开花：水晶展开声 + 一圈迸散微光
            if (Elapsed == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.6f, Volume = 0.3f }, Projectile.Center);
                for (int i = 0; i < PetalCount; i++) {
                    Vector2 v = (MathHelper.TwoPi / PetalCount * i).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, v
                        , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.5f)).Configure(18, 1f, 1.3f);
                }
            }

            if (Projectile.ai[1] < MaxPulses && Elapsed >= PulseInterval * (Projectile.ai[1] + 1f)) {
                Projectile.ai[1]++;
                Pulse((int)Projectile.ai[1] - 1);
            }

            //蓄势期能量向心汇聚
            if (Charge > 0f && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(50f, 50f);
                PRTLoader.NewParticle<PRT_Light>(from, from.To(Projectile.Center) * 0.07f
                    , GraniteMarbleVFX.GraniteCore, 0.28f).Configure(12, 1f, 1.2f);
            }

            //凋谢起点：花瓣回拢的轻响
            if (Elapsed == WitherStart + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.45f, Volume = 0.4f }, Projectile.Center);
            }

            if (pulseAnim > 0f) {
                pulseAnim = MathF.Max(0f, pulseAnim - 1f / 14f);
            }

            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3()
                * (Open * WitherFade * (1f + 0.5f * Charge)));
        }

        private void Pulse(int index) {
            pulseAnim = 1f;
            //620px 内最近敌人定向；无目标退化为随机方向
            NPC target = Projectile.Center.FindClosestNPC(620f);
            float baseAngle = target != null
                ? Projectile.Center.To(target.Center).ToRotation()
                : Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pulseDir = baseAngle.ToRotationVector2();

            if (!VaultUtils.isServer) {
                //轻迸发，音阶随第几次脉冲递进
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = index * 0.22f, Volume = 0.9f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.2f + index * 0.25f, Volume = 0.25f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.GraniteCore, 0).Configure(0.1f, 0.8f, 22);
                //定向光屑锥指示弹道
                for (int i = 0; i < 10; i++) {
                    Vector2 v = pulseDir.RotatedByRandom(0.45f) * Main.rand.NextFloat(3f, 7f);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center + pulseDir * 16f, v
                        , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.55f)).Configure(18, 1f, 1.4f);
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , pulseDir * 2f, GraniteMarbleVFX.GraniteCore
                        , Main.rand.NextFloat(0.24f, 0.4f)).Configure(Main.rand.Next(3, 6));
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                const int petals = 3;
                for (int i = 0; i < petals; i++) {
                    float off = (i - 1) * 0.36f + Main.rand.NextFloat(-0.06f, 0.06f);
                    Vector2 v = (baseAngle + off).ToRotationVector2() * Main.rand.NextFloat(7.5f, 9.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v
                        , ModContent.ProjectileType<GraniteCrystalShard>()
                        , (int)(Projectile.damage * 0.4f), Projectile.knockBack * 0.3f, Projectile.owner);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //凋谢终点：全体花瓣碎裂成一圈晶片 + 微电弧
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f, Pitch = 0.25f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 14; i++) {
                float a = MathHelper.TwoPi / 14f * i + Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 dir = a.ToRotationVector2();
                PRTLoader.NewParticle<PRT_GraniteShard>(Projectile.Center + dir * 20f
                    , dir * Main.rand.NextFloat(2.5f, 5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f)
                    , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.5f, 0.85f))
                    .Configure(Main.rand.Next(26, 40));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f)
                    , Main.rand.NextVector2Unit() * 2f, GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.26f, 0.42f)).Configure(Main.rand.Next(3, 6));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero
                , GraniteMarbleVFX.GraniteDeep, 0).Configure(0.08f, 0.6f, 20);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, 56f * Open * WitherFade, targetHitbox);

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float vis = Open * WitherFade;
            if (vis <= 0.01f) {
                return;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D blade = CWRAsset.Line.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;

            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            Color spark = GraniteMarbleVFX.GraniteSpark; spark.A = 0;

            float charge = Charge;
            float snap = MathF.Sin(pulseAnim * MathHelper.Pi);
            float breathe = 1f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);

            //地面投光：压扁的深色光晕铺在花体下方
            spriteBatch.Draw(glow, pos + new Vector2(0f, 26f), null, deep * 0.4f * vis, 0f
                , glow.Size() / 2f, new Vector2(3.2f, 0.9f) * vis, SpriteEffects.None, 0f);

            //能量场边界环：与接触判定半径同步张缩
            spriteBatch.Draw(ring, pos, null, deep * (0.45f + 0.3f * charge) * vis, Main.GlobalTimeWrappedHourly
                , ring.Size() / 2f, vis * 0.42f, SpriteEffects.None, 0f);

            //花瓣：开花从"合拢向上"展开为放射状，凋谢时回拢；蓄势内收、脉冲瞬间收张回弹
            float unfold = 1f - (1f - Open) * (1f - Open);
            unfold *= 0.3f + 0.7f * WitherFade;
            float reach = (1f - 0.2f * charge + 0.24f * snap) * vis;
            float widthScale = 0.4f + 0.6f * vis;
            float baseSpin = Main.GlobalTimeWrappedHourly * 0.6f;
            Vector2 bladeOrigin = new Vector2(blade.Width / 2f, blade.Height);

            for (int i = 0; i < PetalCount; i++) {
                float radial = baseSpin + MathHelper.TwoPi / PetalCount * i;
                float ang = (-MathHelper.PiOver2).AngleLerp(radial, unfold);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 root = pos + dir * 10f * reach;
                float rot = ang + MathHelper.PiOver2;
                float petalLen = 46f * reach;
                float lenScale = petalLen / blade.Height;

                //深色底刃 → 青蓝主刃 → 白亮芯，三层叠出晶体花瓣
                spriteBatch.Draw(blade, root, null, deep * 0.8f * vis, rot, bladeOrigin
                    , new Vector2(0.5f * widthScale, lenScale * 1.06f), SpriteEffects.None, 0f);
                spriteBatch.Draw(blade, root, null, spark * 0.95f * vis, rot, bladeOrigin
                    , new Vector2(0.3f * widthScale, lenScale), SpriteEffects.None, 0f);
                spriteBatch.Draw(blade, root, null, Color.White * 0.7f * vis, rot, bladeOrigin
                    , new Vector2(0.13f * widthScale, lenScale * 0.82f), SpriteEffects.None, 0f);
                //花瓣尖端星光，蓄势与脉冲时增亮
                Vector2 tip = root + dir * petalLen;
                spriteBatch.Draw(star, tip, null, spark * (0.6f + 0.25f * charge + 0.3f * snap) * vis, ang
                    , star.Size() / 2f, (0.05f + 0.02f * snap) * vis, SpriteEffects.None, 0f);
            }

            //中心能量核：深晕/亮核/白芯 + 慢旋四芒星
            spriteBatch.Draw(glow, pos, null, deep * 0.85f * vis, 0f, glow.Size() / 2f
                , (1.5f + 0.5f * charge) * vis * breathe, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, core * 0.95f * vis, 0f, glow.Size() / 2f
                , (0.8f + 0.25f * charge + 0.3f * snap) * vis * breathe, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, spark * (0.8f + 0.2f * charge) * vis, -baseSpin * 0.7f
                , star.Size() / 2f, (0.13f + 0.05f * charge + 0.08f * snap) * vis, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, Color.White * 0.6f * vis, 0f, glow.Size() / 2f
                , 0.4f * vis * breathe, SpriteEffects.None, 0f);
        }
    }
}
