using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 雪蝰
    /// <br/>左键按住: 鼓风机式持续吹雪，近中距离的密集雪流，同时向压雪仓积蓄雪压
    /// <br/>松开左键: 雪压足够时将压实的大雪球轰出，落点炸裂成冰碴与雪暴
    /// </summary>
    internal class SnowQuay : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuay";
        /// <summary>鼓风弹药节流计数，跨使用持久，每3次吹雪消耗1颗雪球</summary>
        internal int StreamAmmoThrottle;

        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 66;
            Item.height = 36;
            Item.damage = 24;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 2f;
            Item.value = Terraria.Item.buyPrice(0, 1, 75, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SnowQuayHeld>();
            Item.shootSpeed = 13f;
            Item.useAmmo = AmmoID.Snowball;
            Item.crit = 4;
        }

        //物品使用本身不消耗雪球，由手持弹幕按鼓风节奏自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管开火逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_FlurrystormCannon > 0 && CWRID.Item_EssenceofEleum > 0) {
                _ = CreateRecipe().
                AddIngredient(CWRID.Item_FlurrystormCannon).
                AddIngredient(CWRID.Item_EssenceofEleum, 10).
                AddIngredient(ItemID.IceBlock, 600).
                AddTile(TileID.IceMachine).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.IceBlock, 600).
                AddTile(TileID.IceMachine).
                Register();
            }
        }
    }

    /// <summary>
    /// 雪蝰手持弹幕——鼓风吹雪机
    /// <br/>帧0: 待机, 帧1: 起转, 帧2-3: 鼓风循环, 帧4-5: 压雪弹发射后坐
    /// </summary>
    internal class SnowQuayHeld : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuayHeld";
        public override int TargetItemID => ModContent.ItemType<SnowQuay>();
        protected override int FrameCount => 6;
        protected override float BarrelLength => 36f;
        protected override float MuzzleNormalOffset => 4f;
        protected override float HoldDistance => 36f;

        /// <summary>压雪仓当前蓄压值</summary>
        private float pressure;
        private const float MaxPressure = 100f;
        /// <summary>发射压实雪球所需的最低蓄压</summary>
        private const float MinShellPressure = 30f;
        /// <summary>发射大雪球后的炮口动画计时</summary>
        private int shellAnimTime;

        private SnowQuay WeaponItem => Item.ModItem as SnowQuay;
        //蓄压未结算或炮口动画未播完时不销毁，保证松开按键后压雪弹能正常轰出
        protected override bool PendingWork => pressure > 0 || shellAnimTime > 0;

        protected override void UpdateGun() {
            if (shellAnimTime > 0) {
                shellAnimTime--;
                Projectile.frame = shellAnimTime > 5 ? 4 : 5;
            }

            if (FireKeyLeft) {
                BlowSnow();
                return;
            }

            //松开左键：蓄压足够就把压实雪球轰出去
            if (pressure >= MinShellPressure) {
                FirePackedBall();
            }
            pressure = 0;

            if (shellAnimTime <= 0) {
                Projectile.frame = 0;
            }
        }

        /// <summary>鼓风吹雪：高频低伤的雪流，并积蓄雪压</summary>
        private void BlowSnow() {
            VaultUtils.ClockFrame(ref Projectile.frame, 2, 3, 2);

            if (pressure < MaxPressure) {
                pressure += 1.2f;
            }

            //鼓风嗡鸣，音调随雪压缓慢爬升
            if (Main.GameUpdateCount % 10 == 0) {
                SoundEngine.PlaySound(SoundID.Item23 with {
                    MaxInstances = 3,
                    Pitch = -0.4f + pressure / MaxPressure * 0.5f,
                    Volume = 0.25f
                }, Projectile.Center);
            }

            //枪口的吹雪气流尘
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust snow = Dust.NewDustPerfect(MuzzlePos, DustID.SnowflakeIce
                    , GunForward.RotatedByRandom(0.35f) * Main.rand.NextFloat(4f, 9f), 100, default, Main.rand.NextFloat(0.8f, 1.4f));
                snow.noGravity = true;
            }

            if (cooldown > 0 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //每3次吹雪消耗1颗雪球，节流计数存放在物品上跨使用持久
            bool consume = ++WeaponItem.StreamAmmoThrottle >= 3;
            if (consume) {
                WeaponItem.StreamAmmoThrottle = 0;
            }
            if (!PickSnowAmmo(out int damage, out float knockback, consume)) {
                return;
            }

            cooldown = 3;
            recoil = 1.5f;

            Vector2 velocity = GunForward.RotatedByRandom(0.22f) * Main.rand.NextFloat(11f, 14f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                , ModContent.ProjectileType<SnowQuayFlake>(), damage, knockback * 0.5f, Owner.whoAmI);
            NetUpdate();
        }

        /// <summary>发射压实雪球，威力与体积随蓄压提升</summary>
        private void FirePackedBall() {
            float power = pressure / MaxPressure;
            shellAnimTime = 10;
            recoil = 8f;

            SoundEngine.PlaySound(SoundID.Item36 with { Pitch = -0.3f + power * 0.2f }, Projectile.Center);

            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.BlueCrystalShard
                        , GunForward.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 8f), 0, default, 1.2f);
                    d.noGravity = true;
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }

            Vector2 velocity = GunForward * (12f + power * 4f);
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                , ModContent.ProjectileType<SnowQuayPackedBall>(), (int)(damage * (2f + power * 2f)), knockback * 2f, Owner.whoAmI, power);
            proj.scale = 0.75f + power * 0.5f;
            NetUpdate();
        }
    }

    /// <summary>
    /// 鼓风雪流中的小雪团，轻微受重力，命中附加霜火
    /// </summary>
    internal class SnowQuayFlake : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.light = 0.1f;
        }

        public override void AI() {
            Projectile.rotation += Projectile.velocity.X * 0.08f;
            Projectile.velocity.Y += 0.06f;
            if (Main.rand.NextBool(4)) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , Projectile.velocity * 0.2f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                snow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn, 120);

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SnowBlock
                    , Main.rand.NextVector2Circular(2f, 2f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SnowBallFriendly].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, tex.GetOrig(), Projectile.scale * 0.7f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 压实雪球——雪蝰的蓄压主炮弹
    /// <br/>受重力滚落，可在地面弹跳两次，最终炸裂成雪暴与冰碴扇
    /// <br/>ai0: 蓄压比例 0~1，决定爆裂规模
    /// </summary>
    internal class SnowQuayPackedBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        private ref float Power => ref Projectile.ai[0];
        private ref float BounceCount => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.light = 0.25f;
            Projectile.ArmorPenetration = 10;
        }

        public override void AI() {
            Projectile.rotation += Projectile.velocity.X * 0.06f;
            Projectile.velocity.Y += 0.22f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                    , DustID.BlueCrystalShard, Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f, 0, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn, 240);

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (BounceCount >= 2) {
                return true;
            }
            BounceCount++;
            SoundEngine.PlaySound(SoundID.Item48 with { Volume = 0.5f, Pitch = -0.4f }, Projectile.Center);
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.6f;
            }
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.SnowBlock
                    , new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)), 100, default, 1.3f);
                d.noGravity = true;
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item51 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

            //雪暴尘环
            for (int i = 0; i < 26; i++) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , Main.rand.NextVector2Circular(6f, 6f), 100, default, Main.rand.NextFloat(1.2f, 2.2f));
                snow.noGravity = true;
            }
            for (int i = 0; i < 14; i++) {
                Dust ice = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2CircularEdge(5f, 5f), 0, default, 1.4f);
                ice.noGravity = true;
            }

            if (Main.myPlayer != Projectile.owner) {
                return;
            }

            //向上扇形迸出冰碴
            int shardCount = 4 + (int)(Power * 4);
            for (int i = 0; i < shardCount; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(5f, 9f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity
                    , ModContent.ProjectileType<SnowQuayShard>(), (int)(Projectile.damage * 0.4f), 0.5f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //三层叠绘营造压实雪球的厚重体积感
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SnowBallFriendly].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 orig = tex.GetOrig();
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, orig, Projectile.scale + 0.2f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, Color.White, Projectile.rotation, orig, Projectile.scale + 0.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, Color.White, -Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 压实雪球炸裂迸出的冰碴，受重力下落，触地即碎
    /// </summary>
    internal class SnowQuayShard : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.light = 0.15f;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.25f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                , Projectile.velocity * 0.1f, 0, default, Main.rand.NextFloat(0.7f, 1.1f));
            d.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn, 90);

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(2f, 2f), 0, default, 1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
