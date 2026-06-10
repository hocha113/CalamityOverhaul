using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 凛冬神性
    /// <br/>左键按住: 向冬至核心灌注雪压蓄能，分三档
    /// <br/>松开左键: 释放冬至审判——贯穿一切的极寒神性光束，满蓄时命中处绽放冬至冕环
    /// <br/>右键: 极夜新星，以自身为中心的冻爆脉冲
    /// </summary>
    internal class DarkFrostSolstice : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolstice";
        public static int ID { get; private set; }
        /// <summary>右键极夜新星的就绪时间戳，跨使用持久</summary>
        internal uint NovaReadyTime;

        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 94;
            Item.height = 38;
            Item.damage = 230;
            Item.useTime = Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 6f;
            Item.value = Terraria.Item.buyPrice(0, 35, 5, 5);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DarkFrostSolsticeHeld>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Snowball;
            Item.crit = 10;
        }

        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗雪球，由手持弹幕按蓄能档位自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[Item.shoot] > 0) {
                return false;
            }
            //极夜新星冷却完毕前右键无法使用
            return player.altFunctionUse != 2 || Main.GameUpdateCount >= NovaReadyTime;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管蓄能与开火逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<UniversalFrost>().
                AddIngredient(ItemID.LunarBar, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<UniversalFrost>().
                AddIngredient(CWRID.Item_Kingsbane).
                AddIngredient(CWRID.Item_ShadowspecBar, 5).
                AddIngredient(CWRID.Item_EndothermicEnergy, 10).
                AddTile(CWRID.Tile_DraedonsForge).
                Register();
        }
    }

    /// <summary>
    /// 凛冬神性手持弹幕——三档蓄力的神性轨道炮
    /// <br/>帧0-3: 蓄能循环, 帧4: 待机
    /// </summary>
    internal class DarkFrostSolsticeHeld : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolsticeHeld";
        public override int TargetItemID => ModContent.ItemType<DarkFrostSolstice>();
        protected override int FrameCount => 5;
        protected override float BarrelLength => 54f;
        protected override float MuzzleNormalOffset => 5f;
        protected override float HoldDistance => 24f;

        /// <summary>当前蓄能 0~100，只在本次持握期间有效，松开即结算</summary>
        private float charge;
        private const float Tier1 = 30f;
        private const float Tier2 = 65f;
        private const float Tier3 = 100f;
        /// <summary>已经播报过的蓄能档位</summary>
        private int cuedTier;
        /// <summary>释放后的收尾动画计时</summary>
        private int postFireTime;

        private int CurrentTier => charge >= Tier3 ? 3 : charge >= Tier2 ? 2 : charge >= Tier1 ? 1 : 0;

        private DarkFrostSolstice WeaponItem => Item.ModItem as DarkFrostSolstice;
        //蓄能未结算或收尾动画未播完时不销毁，保证松开按键后审判能正常释放
        protected override bool PendingWork => charge > 0 || postFireTime > 0;

        protected override void UpdateGun() {
            if (postFireTime > 0) {
                postFireTime--;
            }

            if (FireKeyLeft && cooldown <= 0) {
                ChargeUp();
                return;
            }

            //松开：按蓄能档位释放
            if (charge > 0) {
                int tier = CurrentTier;
                if (tier <= 0) {
                    //轻点：快速吐出一发冰锥应急
                    QuickShot();
                }
                else if (Projectile.IsOwnedByLocalPlayer()) {
                    FireJudgment(tier);
                }
                //收尾期间保持手持，让后坐与音画播完
                postFireTime = 8 + tier * 4;
                charge = 0;
                cuedTier = 0;
            }

            Projectile.frame = 4;

            if (FireKeyRight && cooldown <= 0 && Projectile.IsOwnedByLocalPlayer() && TimeReady(WeaponItem.NovaReadyTime)) {
                FireNova();
            }
        }

        /// <summary>蓄能：吸聚寒气，分档提示</summary>
        private void ChargeUp() {
            if (charge < Tier3) {
                charge += 1f;
            }
            float charge01 = charge / Tier3;

            VaultUtils.ClockFrame(ref Projectile.frame, charge >= Tier2 ? 2 : 4, 3);

            //蓄能低鸣，音调随充能爬升
            if (Main.GameUpdateCount % 12 == 0) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Pitch = -0.8f + charge01 * 1.1f,
                    Volume = 0.35f + charge01 * 0.3f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            //跨档位提示
            int tier = CurrentTier;
            if (tier > cuedTier) {
                cuedTier = tier;
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.1f + tier * 0.25f, Volume = 1f }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 10 + tier * 6; i++) {
                        Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.IceTorch
                            , Main.rand.NextVector2CircularEdge(2f + tier, 2f + tier), 0, default, 1.4f);
                        d.noGravity = true;
                    }
                }
            }

            //寒气向枪口汇聚
            if (!Main.dedServ && Main.rand.NextBool(3 - Math.Min(tier, 2))) {
                Vector2 from = MuzzlePos + Main.rand.NextVector2CircularEdge(50f, 50f);
                Dust d = Dust.NewDustPerfect(from, DustID.SnowflakeIce
                    , from.To(MuzzlePos).UnitVector() * Main.rand.NextFloat(3f, 6f), 100, default, Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = true;
            }

            Lighting.AddLight(MuzzlePos, 0.3f * charge01, 0.5f * charge01, 0.9f * charge01);
        }

        /// <summary>轻点左键的应急冰锥</summary>
        private void QuickShot() {
            if (!Projectile.IsOwnedByLocalPlayer() || !PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }
            cooldown = 12;
            recoil = 3f;
            SoundEngine.PlaySound(SoundID.Item91 with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), MuzzlePos, GunForward * 19f
                , ModContent.ProjectileType<IcicleNail>(), (int)(damage * 0.8f), knockback, Owner.whoAmI);
            NetUpdate();
        }

        /// <summary>释放冬至审判光束</summary>
        private void FireJudgment(int tier) {
            //按档位消耗雪球，伤害数据以第一颗为准
            if (!PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }
            for (int i = 1; i < tier; i++) {
                _ = PickSnowAmmo(out _, out _);
            }

            float charge01 = tier / 3f;
            cooldown = 35;
            recoil = 8f + tier * 3.5f;

            SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.6f + tier * 0.1f, Volume = 0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 0.8f }, Projectile.Center);
            if (tier >= 3) {
                SoundEngine.PlaySound(CWRSound.BelCanto with { PitchRange = (-0.1f, 0.1f), Volume = 0.9f });
            }

            float damageMult = tier switch { 1 => 2f, 2 => 3.5f, _ => 5f };
            float width = tier switch { 1 => 26f, 2 => 44f, _ => 68f };

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), MuzzlePos, GunForward
                , ModContent.ProjectileType<SolsticeJudgment>(), (int)(damage * damageMult)
                , knockback, Owner.whoAmI, charge01, width);

            if (CWRServerConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(Projectile.Center, GunForward
                    , 4f + tier * 3f, 5f, 14 + tier * 4, 1200f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
            NetUpdate();
        }

        /// <summary>右键极夜新星：以自身为中心的冻爆脉冲</summary>
        private void FireNova() {
            if (!PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                _ = PickSnowAmmo(out _, out _);
            }

            WeaponItem.NovaReadyTime = Main.GameUpdateCount + 600;
            cooldown = 30;
            postFireTime = 14;

            SoundEngine.PlaySound(SoundID.Item120 with { Pitch = -0.5f, Volume = 1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.6f, Volume = 0.9f }, Owner.Center);

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center, Vector2.Zero
                , ModContent.ProjectileType<SolsticeNova>(), (int)(damage * 2.5f), knockback + 6f, Owner.whoAmI);
            NetUpdate();
        }

        public override void PostDraw(Color lightColor) {
            float charge01 = charge / Tier3;
            if (charge01 <= 0.03f) {
                return;
            }
            Texture2D tex = TextureValue;
            SpriteEffects fx = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (5f + charge01 * 8f));
            Color glow = new Color(130, 170, 255, 0) * (charge01 * 0.65f * pulse);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, tex.GetRectangle(Projectile.frame, FrameCount)
                , glow, Projectile.rotation, tex.GetOrig(FrameCount), Projectile.scale * 1.05f, fx, 0);

            //枪口凝聚的极寒星核
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Color starColor = new Color(180, 220, 255, 0) * (charge01 * pulse);
            Main.EntitySpriteDraw(star, MuzzlePos - Main.screenPosition, null, starColor
                , Main.GlobalTimeWrappedHourly * 2f, star.GetOrig(), 0.1f + charge01 * 0.2f, fx, 0);
        }
    }

    /// <summary>
    /// 冬至审判——由 FrostJudgment.fx 渲染的贯穿光束
    /// <br/>ai0: 蓄力比例 0~1, ai1: 光束宽度
    /// <br/>生成时即对路径判定伤害，满蓄时在终点绽放冬至冕环
    /// </summary>
    internal class SolsticeJudgment : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float Charge01 => ref Projectile.ai[0];
        private ref float BeamWidth => ref Projectile.ai[1];
        /// <summary>光束实际长度，首帧射线探测获得</summary>
        private ref float BeamLength => ref Projectile.localAI[0];

        private const int LifeTime = 26;
        private const float MaxLength = 2200f;
        private float Fade => MathHelper.Clamp(Projectile.timeLeft / (float)LifeTime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧：探测光束在地形上的实际落点
            if (BeamLength <= 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.velocity = Vector2.Zero;

                float[] samples = new float[3];
                Collision.LaserScan(Projectile.Center, Projectile.rotation.ToRotationVector2(), BeamWidth * 0.5f, MaxLength, samples);
                BeamLength = 0;
                foreach (float sample in samples) {
                    BeamLength += sample / samples.Length;
                }

                SpawnBeamDust();

                //满蓄：终点绽放冬至冕环
                if (Charge01 >= 0.99f && Main.myPlayer == Projectile.owner) {
                    Vector2 endPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), endPos, Vector2.Zero
                        , ModContent.ProjectileType<SolsticeNova>(), (int)(Projectile.damage * 0.6f), 6f, Projectile.owner, 1f);
                }
            }

            Vector2 mid = Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength * 0.5f;
            Lighting.AddLight(mid, 0.4f * Fade, 0.6f * Fade, 1f * Fade);
        }

        /// <summary>沿光束路径迸出冰星与寒雾</summary>
        private void SpawnBeamDust() {
            if (Main.dedServ) {
                return;
            }
            Vector2 unit = Projectile.rotation.ToRotationVector2();
            int steps = (int)(BeamLength / 40f);
            for (int i = 0; i < steps; i++) {
                Vector2 pos = Projectile.Center + unit * i * 40f + Main.rand.NextVector2Circular(BeamWidth * 0.4f, BeamWidth * 0.4f);
                Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.BlueCrystalShard
                    , unit.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(1f, 4f), 0, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
            //终点炸开
            Vector2 endPos = Projectile.Center + unit * BeamLength;
            for (int i = 0; i < 16; i++) {
                Dust d = Dust.NewDustPerfect(endPos, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(6f, 6f), 0, default, 1.6f);
                d.noGravity = true;
            }
        }

        //只在前8帧造成伤害，余下时间为渐隐余辉
        public override bool? CanDamage() => Projectile.timeLeft > LifeTime - 8 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, end, BeamWidth, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 360);
            if (Charge01 >= 0.6f) {
                target.AddBuff(BuffID.Chilled, 180);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.FrostJudgment?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || BeamLength <= 0) {
                return;
            }

            Vector2 unit = Projectile.rotation.ToRotationVector2();
            Vector2 perp = unit.RotatedBy(MathHelper.PiOver2);
            //余辉期光束逐渐收窄
            float halfW = BeamWidth * (0.5f + Charge01 * 0.35f) * (0.35f + Fade * 0.65f) * 2f;
            Vector2 start = Projectile.Center;
            Vector2 end = start + unit * BeamLength;

            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((start - perp * halfW).ToVector3(), Color.White, new Vector2(0, 0));
            quad[1] = new VertexPositionColorTexture((end - perp * halfW).ToVector3(), Color.White, new Vector2(1, 0));
            quad[2] = new VertexPositionColorTexture((start + perp * halfW).ToVector3(), Color.White, new Vector2(0, 1));
            quad[3] = new VertexPositionColorTexture((end + perp * halfW).ToVector3(), Color.White, new Vector2(1, 1));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(Fade);
            effect.Parameters["uCharge"]?.SetValue(Charge01);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.73f % 10f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 极夜新星——冻爆脉冲，从中心向外扩张的极寒冲击环
    /// <br/>ai0: 1=由冬至审判触发的冕环（更小、更快）
    /// </summary>
    internal class SolsticeNova : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool IsCorona => Projectile.ai[0] == 1f;
        private const int LifeTime = 24;

        private float Progress => 1f - Projectile.timeLeft / (float)LifeTime;
        private float Radius => (IsCorona ? 230f : 360f) * (1f - MathF.Pow(1f - Progress, 3f));
        private float Fade => 1f - Progress;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.timeLeft == LifeTime && !Main.dedServ) {
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                        , Main.rand.NextVector2CircularEdge(9f, 9f), 100, default, Main.rand.NextFloat(1.5f, 2.6f));
                    d.noGravity = true;
                }
            }

            //冲击环边缘的冰晶飞溅
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust d = Dust.NewDustPerfect(Projectile.Center + angle.ToRotationVector2() * Radius
                        , DustID.BlueCrystalShard, angle.ToRotationVector2() * 2f, 0, default, 1.4f);
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.5f * Fade, 0.7f * Fade, 1f * Fade);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius, targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(BuffID.Chilled, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float ringScale = Radius * 2f / ring.Width;
            Main.EntitySpriteDraw(ring, drawPos, null, new Color(140, 200, 255, 0) * (Fade * 0.9f)
                , 0f, ring.GetOrig(), ringScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(ring, drawPos, null, new Color(200, 240, 255, 0) * (Fade * 0.6f)
                , 0f, ring.GetOrig(), ringScale * 0.8f, SpriteEffects.None, 0);
            //中心闪星
            Main.EntitySpriteDraw(star, drawPos, null, new Color(220, 245, 255, 0) * Fade
                , Progress * 2f, star.GetOrig(), 0.5f * Fade, SpriteEffects.None, 0);
            return false;
        }
    }
}
