using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 凛冬神性
    /// <br/>左键扫射冰雹与追踪水晶，射速随扫射不断攀升
    /// <br/>高转速下每开火20次进入一次超频爆发，轰鸣蓄势后在鼠标处生成巨大的冰柱
    /// </summary>
    internal class DarkFrostSolstice : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolstice";
        public static int ID { get; private set; }

        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 94;
            Item.height = 38;
            Item.damage = 102;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 2f;
            Item.value = Terraria.Item.buyPrice(0, 35, 5, 5);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DarkFrostSolsticeHeld>();
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Snowball;
        }

        //物品使用本身不消耗雪球，由手持弹幕按扫射节奏自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管扫射逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_Kingsbane > 0 && CWRID.Item_ShadowspecBar > 0
                && CWRID.Item_EndothermicEnergy > 0 && CWRID.Tile_DraedonsForge > 0) {
                _ = CreateRecipe().
                AddIngredient<UniversalFrost>().
                AddIngredient(CWRID.Item_Kingsbane).
                AddIngredient(CWRID.Item_ShadowspecBar, 5).
                AddIngredient(CWRID.Item_EndothermicEnergy, 10).
                AddTile(CWRID.Tile_DraedonsForge).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<UniversalFrost>().
                AddIngredient(ItemID.LunarBar, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
            }
        }
    }

    /// <summary>
    /// 凛冬神性HeldProj，射速攀升极寒机枪
    /// <br/>帧0-3: 开火循环, 帧4: 待机
    /// <br/>扫射3冰雹+3追踪水晶；高转速每20发超频
    /// 枪身轰鸣蓄势约1秒，下一次开火在鼠标处掀起巨大的冰柱天罚
    /// </summary>
    internal class DarkFrostSolsticeHeld : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolsticeHeld";
        public override int TargetItemID => ModContent.ItemType<DarkFrostSolstice>();
        protected override int FrameCount => 5;
        protected override float BarrelLength => 54f;
        protected override float MuzzleNormalOffset => 5f;
        protected override float HoldDistance => 24f;

        /// <summary>超频轰鸣蓄势计时，期间下一次开火将引发冰柱天罚</summary>
        private int onFireTime;
        /// <summary>轰鸣期间枪口喷尘的帧动画余辉</summary>
        private int onFireTime2;

        //轰鸣蓄势期间即使松开扳机也不销毁，保证攒出来的超频爆发不会丢失
        protected override bool PendingWork => onFireTime > 0;

        protected override void UpdateGun() {
            if (FireKeyLeft) {
                VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
            }
            else {
                Projectile.frame = 4;
            }

            if (onFireTime2 > 0) {
                onFireTime2--;
            }

            if (onFireTime > 0) {
                //超频轰鸣爬升
                SoundEngine.PlaySound(SoundID.Item23 with {
                    Pitch = (60 - onFireTime) * 0.15f,
                    MaxInstances = 13,
                    Volume = 0.2f + onFireTime * 0.006f
                }, Projectile.Center);

                if (onFireTime % 15 == 0) {
                    SpawnMuzzleDust();
                    onFireTime2 = 8;
                }

                if (onFireTime2 > 0) {
                    VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
                }
                else {
                    Projectile.frame = 4;
                }

                //机匣的狂躁震颤
                Projectile.Center += VaultUtils.RandVr(8f);
                onFireTime--;
            }
            else if (GunState.SolsticeFireRate > 30) {
                //超频爆发结束，转速回落到温热档
                GunState.SolsticeFireRate = 15;
            }

            if (FireKeyLeft && cooldown <= 0 && Projectile.IsOwnedByLocalPlayer()) {
                Fire();
            }
        }

        /// <summary>轰鸣期的枪口喷尘</summary>
        private void SpawnMuzzleDust() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(MuzzlePos, Main.rand.NextBool(3) ? 149 : 76
                    , GunForward.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 7f), 0, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        /// <summary>开火，扫射或超频</summary>
        private void Fire() {
            if (!PickSnowAmmo(out int projToShoot, out int damage, out float knockback)) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item36 with { Pitch = 0.2f }, Projectile.Center);

            Vector2 shootVelocity = GunForward * Item.shootSpeed;

            //枪口的冰晶迸流
            if (!Main.dedServ) {
                for (int i = 0; i < 33; i++) {
                    Vector2 vr = shootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f);
                    Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.BlueCrystalShard, vr, 0, default, 1.1f);
                    d.noGravity = true;
                }
            }

            SnowCannonPlayer state = GunState;
            //蓄势完成→冰柱天罚
            if (onFireTime > 0) {
                FireIcePillar(damage, knockback);
                cooldown = 15;
                state.SolsticeFireRate = 8;
                NetUpdate();
                return;
            }

            recoil = 2.5f;

            //每2发提速，最快6tick
            if (++state.SolsticeFireIndex > 1) {
                if (state.SolsticeFireRate > 6) {
                    state.SolsticeFireRate--;
                }
                state.SolsticeFireIndex = 0;
            }

            //3冰雹，高转速可淬神性
            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), MuzzlePos
                    , shootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.7f, 1.1f)
                    , projToShoot, damage, knockback, Owner.whoAmI, 0);
                proj.extraUpdates += 1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                if (Main.rand.NextBool(2)) {
                    proj.damage /= 2;
                }
                if (Main.rand.NextBool(4) && state.SolsticeFireRate <= 15) {
                    proj.scale += Main.rand.NextFloat(0.35f);
                }
                if (Main.rand.NextBool(3) && state.SolsticeFireRate <= 10) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 5;
                }
            }

            //3颗追踪水晶
            for (int i = 0; i < 3; i++) {
                Projectile iceorb = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), MuzzlePos
                    , shootVelocity.RotatedByRandom(0.06f)
                    , ModContent.ProjectileType<Crystal>(), damage * 3, knockback, Owner.whoAmI, 0, 0);
                iceorb.rotation = iceorb.velocity.ToRotation();
            }

            //每20发超频蓄势
            if (state.SolsticeFireRate <= 8) {
                if (++state.SolsticeFireIndex2 > 20) {
                    state.SolsticeFireRate = 50;
                    onFireTime += 60;
                    state.SolsticeFireIndex2 = 0;
                }
            }

            cooldown = state.SolsticeFireRate;
            NetUpdate();
        }

        /// <summary>超频，鼠标处贴地冰柱阵</summary>
        private void FireIcePillar(int damage, float knockback) {
            recoil = 10f;

            SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.5f, Volume = 0.5f });
            SoundEngine.PlaySound(CWRSound.BelCanto with { PitchRange = (-0.1f, 0.1f), Volume = 0.9f });

            //从鼠标处向下探地，命中地面则附带巨额贯穿伤害
            bool intile = false;
            int overdmg = 1500;
            Vector2 targetPos = InMousePos;
            for (int i = 0; i < 128; i++) {
                Vector2 offset = new Vector2(0, i * 16);
                if (Framing.GetTileSafely(targetPos + offset).HasSolidTile()) {
                    targetPos += offset;
                    intile = true;
                    break;
                }
            }

            if (!intile) {
                overdmg = 0;
                targetPos.Y += 530;
            }

            var source = Projectile.GetSource_FromThis();

            //沿冰柱升腾的冰爆寒雾
            for (int i = 0; i < 35; i++) {
                Projectile.NewProjectile(source, targetPos + new Vector2(0, i * -8)
                    , new Vector2(0, -13).RotatedByRandom(0.5f) * Main.rand.NextFloat(0.35f, 3.12f)
                    , ModContent.ProjectileType<IceExplosionFriend>(), damage, knockback, Owner.whoAmI, 0);
            }

            //拔地而起的巨大冰柱主体
            for (int i = 0; i < 40; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-3, 3) * (i * 0.01f), -3);
                Projectile proj = Projectile.NewProjectileDirect(source
                    , targetPos + new Vector2(Main.rand.Next(-16, 16), Main.rand.Next(-64, 0)) + new Vector2(0, i * -25 + 64)
                    , velocity, ProjectileID.DeerclopsIceSpike, damage * 5 + overdmg, 0f, Owner.whoAmI
                    , 0f, Main.rand.NextFloat(1f, 1.3f) + i * 0.06f);
                proj.rotation = velocity.ToRotation();
                proj.hostile = false;
                proj.friendly = true;
                proj.penetrate = -1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                proj.light = 0.75f;
                proj.CWR().HitAttribute.SuperAttack = true;
            }

            targetPos.Y -= 700;

            //向左右两侧斜掠的冰枪阵
            Vector2 inLVr = new Vector2(-3, -0.5f);
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = inLVr;
                velocity.Y -= Main.rand.NextFloat(0.3f);
                Projectile proj = Projectile.NewProjectileDirect(source, targetPos + inLVr * i * 16, velocity
                    , ProjectileID.DeerclopsIceSpike, damage * 3 + overdmg, 0f, Owner.whoAmI
                    , 0f, Main.rand.NextFloat(1.8f, 2.1f) + i * 0.07f);
                proj.rotation = velocity.ToRotation();
                proj.hostile = false;
                proj.friendly = true;
                proj.penetrate = -1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                proj.light = 0.75f;
            }

            Vector2 inRVr = new Vector2(3, -0.5f);
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = inRVr;
                velocity.Y -= Main.rand.NextFloat(0.3f);
                Projectile proj = Projectile.NewProjectileDirect(source, targetPos + inRVr * i * 16, velocity
                    , ProjectileID.DeerclopsIceSpike, damage * 3 + overdmg, 0f, Owner.whoAmI
                    , 0f, Main.rand.NextFloat(1.8f, 2.1f) + i * 0.07f);
                proj.rotation = velocity.ToRotation();
                proj.hostile = false;
                proj.friendly = true;
                proj.penetrate = -1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                proj.light = 0.75f;
            }

            Owner.CWR().GetScreenShake(5.3f);
            PunchCameraModifier modifier = new PunchCameraModifier(targetPos
                , (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
            Main.instance.CameraModifiers.Add(modifier);
        }
    }

    /// <summary>追踪水晶，远距平滑转向近距直扑，触地反弹迸冰刺</summary>
    internal class Crystal : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "Crystal";
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = -1;
            Projectile.friendly = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
            if (Projectile.ai[0] > 30) {
                NPC target = Projectile.Center.FindClosestNPC(600, false, true);
                if (target != null) {
                    float num = target.Center.Distance(Projectile.Center);
                    if (num > 120) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.22f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }
            if (Projectile.timeLeft == 1) {
                for (int i = 0; i < 33; i++) {
                    int index2 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                        , DustID.BlueCrystalShard, Projectile.velocity.X, Projectile.velocity.Y, 0, default, 1.1f);
                    Main.dust[index2].noGravity = true;
                }
            }
            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 180);
            target.AddBuff(BuffID.Frozen, 30);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] == 0) {
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * Utils.Remap(Projectile.ai[0], 0f, 135f, 0.9f, 2f);
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y * Utils.Remap(Projectile.ai[0], 0f, 135f, 0.9f, 2f);
                }
                for (int i = 0; i < 5; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-5, 5), -3);
                    Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Bottom + new Vector2(Main.rand.Next(-26, 26), i * -2), velocity
                    , ProjectileID.DeerclopsIceSpike, 23, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.8f, 1.2f));
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 20;
                    proj.light = 0.75f;
                }
            }
            Projectile.ai[1]++;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(value, drawPosition, value.GetRectangle(Projectile.frame, 4)
                , Color.White, Projectile.rotation, VaultUtils.GetOrig(value, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
