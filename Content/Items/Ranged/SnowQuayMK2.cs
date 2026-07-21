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
    /// 雪蝰MK2
    /// <br/>加装制冷压缩机的军用改型，把雪球压铸成高速冰锥
    /// <br/>左键: 三连点射冰锥钉，每组点射的最后一发为重锥，命中迸出冰晶
    /// <br/>右键: 超压霰射，扇形喷出一片短程冰锥
    /// </summary>
    internal class SnowQuayMK2 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuayMK2";

        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 80;
            Item.height = 30;
            Item.damage = 35;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 3f;
            Item.value = Terraria.Item.buyPrice(0, 3, 50, 0);
            Item.rare = ItemRarityID.Pink;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SnowQuayMK2Held>();
            Item.shootSpeed = 17f;
            Item.useAmmo = AmmoID.Snowball;
            Item.crit = 8;
        }

        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗雪球，由手持弹幕按点射节奏自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[Item.shoot] > 0) {
                return false;
            }
            SnowCannonPlayer state = player.GetModPlayer<SnowCannonPlayer>();
            return player.altFunctionUse == 2
                ? Main.GameUpdateCount >= state.MK2ScatterReadyTime
                : Main.GameUpdateCount >= state.MK2BurstReadyTime;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管开火逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_CryonicBar > 0 && CWRID.Item_EssenceofEleum > 0) {
                _ = CreateRecipe().
                AddIngredient<SnowQuay>().
                AddIngredient(CWRID.Item_CryonicBar, 5).
                AddIngredient(CWRID.Item_EssenceofEleum, 5).
                AddIngredient(ItemID.IceBlock, 1000).
                AddTile(TileID.IceMachine).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<SnowQuay>().
                AddIngredient(ItemID.IceBlock, 1000).
                AddTile(TileID.IceMachine).
                Register();
            }
        }
    }

    /// <summary>雪蝰MK2 HeldProj，冰锥点射步枪</summary>
    internal class SnowQuayMK2Held : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuayMK2";
        public override int TargetItemID => ModContent.ItemType<SnowQuayMK2>();
        protected override float BarrelLength => 46f;
        protected override float MuzzleNormalOffset => 6f;
        protected override float HoldDistance => 50f;

        /// <summary>当前点射还剩几发</summary>
        private int burstLeft;
        /// <summary>点射内的发与发间隔</summary>
        private int burstGap;

        //一组点射没吐完之前即使松开按键也不销毁
        protected override bool PendingWork => burstLeft > 0;

        protected override void UpdateGun() {
            //点射吐完剩余冰锥
            if (burstLeft > 0) {
                if (--burstGap <= 0) {
                    burstGap = 4;
                    burstLeft--;
                    FireNail(isHeavy: burstLeft == 0);
                }
                return;
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            SnowCannonPlayer state = GunState;
            if (FireKeyLeft && TimeReady(state.MK2BurstReadyTime)) {
                if (!PickSnowAmmo(out _, out _)) {
                    return;
                }
                state.MK2BurstReadyTime = Main.GameUpdateCount + 26;
                burstLeft = 3;
                burstGap = 0;
                NetUpdate();
                return;
            }

            if (FireKeyRight && TimeReady(state.MK2ScatterReadyTime) && TimeReady(state.MK2BurstReadyTime)) {
                FireScatter();
            }
        }

        /// <summary>发射一枚冰锥钉，重锥更大更痛且命中迸冰</summary>
        private void FireNail(bool isHeavy) {
            recoil = isHeavy ? 5f : 3f;
            SoundEngine.PlaySound(SoundID.Item91 with {
                Pitch = isHeavy ? -0.35f : 0.1f,
                Volume = 0.6f,
                MaxInstances = 5
            }, Projectile.Center);

            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.IceTorch
                        , GunForward.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 5f), 0, default, 1.1f);
                    d.noGravity = true;
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //点射期间不再扣弹药，进入点射时已消耗；中途弹药见底就用武器面板数据兜底
            if (!PickSnowAmmo(out int damage, out float knockback, consume: false)) {
                damage = Owner.GetWeaponDamage(Item);
                knockback = Item.knockBack;
            }

            Vector2 velocity = GunForward.RotatedByRandom(isHeavy ? 0.01f : 0.035f) * 17f;
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                , ModContent.ProjectileType<IcicleNail>(), isHeavy ? (int)(damage * 1.6f) : damage
                , knockback, Owner.whoAmI, isHeavy ? 1f : 0f);
            if (isHeavy) {
                proj.scale = 1.3f;
            }
        }

        /// <summary>右键超压扇形短程冰锥</summary>
        private void FireScatter() {
            if (!PickSnowAmmo(out int damage, out float knockback)) {
                return;
            }
            //霰射额外多扣一颗雪球，打不出来也认了，超压就是要烧弹药
            _ = PickSnowAmmo(out _, out _);

            SnowCannonPlayer state = GunState;
            state.MK2ScatterReadyTime = Main.GameUpdateCount + 60;
            //霰射后点射也要缓一口气
            if (state.MK2BurstReadyTime < Main.GameUpdateCount + 20) {
                state.MK2BurstReadyTime = Main.GameUpdateCount + 20;
            }
            recoil = 9f;

            SoundEngine.PlaySound(SoundID.Item38 with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);

            for (int i = 0; i < 13; i++) {
                Vector2 velocity = GunForward.RotatedByRandom(0.3f) * Main.rand.NextFloat(12f, 18f);
                Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                    , ModContent.ProjectileType<IcicleNail>(), damage, knockback * 0.6f, Owner.whoAmI);
                proj.timeLeft = 40;//短射程衰减
            }
            NetUpdate();
        }
    }

    /// <summary>
    /// 冰锥钉，压缩机铸成的高速冰锥
    /// <br/>ai0: 1=重锥，命中或破碎时向上迸出冰晶
    /// </summary>
    internal class IcicleNail : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder3;
        private bool IsHeavy => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
            Projectile.extraUpdates = 2;
            Projectile.light = 0.2f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            //飞行一段距离后开始轻微下坠
            if (++Projectile.ai[1] > 60) {
                Projectile.velocity.Y += 0.04f;
            }
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch
                    , -Projectile.velocity * 0.1f, 0, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 180);
            if (IsHeavy && Main.myPlayer == Projectile.owner) {
                BurstCrystals();
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.1f);
                d.noGravity = true;
            }
            if (IsHeavy && timeLeft > 0 && Main.myPlayer == Projectile.owner) {
                BurstCrystals();
            }
        }

        /// <summary>重锥的冰晶迸发</summary>
        private void BurstCrystals() {
            for (int i = 0; i < 3; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(4f, 7f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity
                    , ModContent.ProjectileType<SnowQuayShard>(), (int)(Projectile.damage * 0.5f), 0.5f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.IceBolt);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.IceBolt].Value;
            Vector2 orig = tex.GetOrig();
            //贴图朝上，旋转补 PiOver2
            float drawRot = Projectile.rotation + MathHelper.PiOver2;

            //冰蓝残影
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                Vector2 drawPos = Projectile.oldPos[k] + Projectile.Size / 2 - Main.screenPosition;
                Color trailColor = new Color(120, 200, 255, 0) * (0.45f * (1f - k / (float)Projectile.oldPos.Length));
                Main.EntitySpriteDraw(tex, drawPos, null, trailColor, drawRot, orig
                    , Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor
                , drawRot, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
