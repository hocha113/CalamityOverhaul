using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    /// 凛冬神性——转速攀升的神性机枪
    /// <br/>左键按住: 持续扫射凛冬弹，枪管转速不断攀升，射速越打越快
    /// <br/>高转速下周期性抛出追猎的寒魂晶；满转速下持续扫射积累神性
    /// <br/>神性蓄满后，下一发子弹将引下冬至审判：光标处天降极寒光柱，大地迸发冰枪与冻爆
    /// </summary>
    internal class DarkFrostSolstice : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolstice";
        public static int ID { get; private set; }
        /// <summary>扫射弹药节流计数，跨使用持久，每2发消耗1颗雪球</summary>
        internal int BoltAmmoThrottle;

        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 94;
            Item.height = 38;
            Item.damage = 165;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 3f;
            Item.value = Terraria.Item.buyPrice(0, 35, 5, 5);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DarkFrostSolsticeHeld>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Snowball;
            Item.crit = 10;
        }

        //物品使用本身不消耗雪球，由手持弹幕按扫射节奏自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管扫射逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type, damage, knockback, player.whoAmI);
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
    /// 凛冬神性手持弹幕——转速攀升的神性机枪
    /// <br/>帧2-3: 扫射循环, 帧4: 待机
    /// <br/>转速与神性都只在本次扫射期间有效，松开扳机即泄压，鼓励持续压制的机枪手感
    /// </summary>
    internal class DarkFrostSolsticeHeld : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolsticeHeld";
        public override int TargetItemID => ModContent.ItemType<DarkFrostSolstice>();
        protected override int FrameCount => 5;
        protected override float BarrelLength => 54f;
        protected override float MuzzleNormalOffset => 5f;
        protected override float HoldDistance => 24f;

        /// <summary>枪管转速 0~1，扫射期间攀升，松开即散</summary>
        private float spin;
        /// <summary>转速拉满所需的扫射时长</summary>
        private const float SpinUpTime = 75f;
        /// <summary>满转速下每发积累的神性，蓄满引下冬至审判</summary>
        private float divinity;
        private const float DivinityMax = 22f;
        /// <summary>神性已蓄满，下一发子弹将携带审判</summary>
        private bool judgmentPrimed;
        /// <summary>累计射出的弹数，用于寒魂晶的节拍</summary>
        private int shotCount;
        /// <summary>审判释放后的收尾计时</summary>
        private int afterglow;

        /// <summary>当前射击间隔：转速越高扫射越快</summary>
        private int FireInterval => (int)MathHelper.Lerp(8f, 3f, spin);

        private DarkFrostSolstice WeaponItem => Item.ModItem as DarkFrostSolstice;
        //审判收尾未播完时不销毁，让后坐与音画完整
        protected override bool PendingWork => afterglow > 0;

        protected override void UpdateGun() {
            if (afterglow > 0) {
                afterglow--;
            }

            if (!FireKeyLeft) {
                Projectile.frame = 4;
                return;
            }

            //转速攀升
            if (spin < 1f) {
                spin = Math.Min(1f, spin + 1f / SpinUpTime);
            }

            //枪管旋转动画随转速加速
            VaultUtils.ClockFrame(ref Projectile.frame, spin > 0.5f ? 2 : 4, 3, 2);

            //高转速的机匣震颤，审判待发时颤得更凶
            float tremble = spin * 1.1f + (judgmentPrimed ? 1.6f : 0f);
            if (tremble > 0.3f) {
                Projectile.Center += Main.rand.NextVector2Circular(tremble, tremble);
            }

            //转子呼啸，音调随转速爬升
            if (Main.GameUpdateCount % 8 == 0) {
                SoundEngine.PlaySound(SoundID.Item22 with {
                    Pitch = -0.6f + spin * 1f,
                    Volume = 0.18f + spin * 0.25f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            //审判待发：寒气向枪口疯狂汇聚
            if (judgmentPrimed && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 from = MuzzlePos + Main.rand.NextVector2CircularEdge(60f, 60f);
                Dust d = Dust.NewDustPerfect(from, DustID.SnowflakeIce
                    , from.To(MuzzlePos).UnitVector() * Main.rand.NextFloat(5f, 9f), 100, default, Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = true;
            }

            Lighting.AddLight(MuzzlePos, 0.25f * spin, 0.4f * spin, 0.7f * spin);

            if (cooldown <= 0) {
                FireBolt();
                cooldown = FireInterval;
            }
        }

        /// <summary>扫射一发凛冬弹，并推进寒魂晶与神性的节拍</summary>
        private void FireBolt() {
            recoil = 1.5f + spin * 2f;

            SoundEngine.PlaySound(SoundID.Item11 with {
                Pitch = -0.15f + spin * 0.35f,
                Volume = 0.35f,
                MaxInstances = 8
            }, Projectile.Center);

            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.BlueCrystalShard
                        , GunForward.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 6f), 0, default, 1.1f);
                    d.noGravity = true;
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //每2发消耗1颗雪球，节流计数存放在物品上跨使用持久
            bool consume = ++WeaponItem.BoltAmmoThrottle >= 2;
            if (consume) {
                WeaponItem.BoltAmmoThrottle = 0;
            }
            if (!PickSnowAmmo(out int damage, out float knockback, consume)) {
                return;
            }

            shotCount++;

            //转速越高散布越紧
            Vector2 velocity = GunForward.RotatedByRandom(0.11f - spin * 0.05f) * (16f + spin * 3f);
            Projectile bolt = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                , ModContent.ProjectileType<SolsticeBolt>(), damage, knockback, Owner.whoAmI);
            //满转速下部分弹头被淬上更重的神性
            if (spin >= 0.99f && Main.rand.NextBool(4)) {
                bolt.penetrate = 3;
                bolt.scale += 0.35f;
            }

            //每第5发抛出一枚追猎的寒魂晶
            if (shotCount % 5 == 0) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), MuzzlePos, velocity.RotatedByRandom(0.06f) * 0.8f
                    , ModContent.ProjectileType<SolsticeSoul>(), (int)(damage * 2.5f), knockback, Owner.whoAmI);
            }

            //审判待发：这一发子弹扣下了天罚的扳机
            if (judgmentPrimed) {
                judgmentPrimed = false;
                divinity = 0;
                FireJudgment(damage);
                NetUpdate();
                return;
            }

            //满转速下持续扫射积累神性
            if (spin >= 0.99f) {
                divinity += 1f;
                if (divinity >= DivinityMax) {
                    judgmentPrimed = true;
                    //蓄满的低吼：短暂屏息后由下一发引爆
                    cooldown = 14;
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 1f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.4f, Volume = 1f }, Projectile.Center);
                }
            }
            NetUpdate();
        }

        /// <summary>引下冬至审判：光标处天降极寒光柱</summary>
        private void FireJudgment(int boltDamage) {
            spin *= 0.55f;
            cooldown = 18;
            afterglow = 16;
            recoil = 10f;

            SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.5f, Volume = 0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 0.8f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.BelCanto with { PitchRange = (-0.1f, 0.1f), Volume = 0.9f });

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), InMousePos, Vector2.Zero
                , ModContent.ProjectileType<SolsticeJudgment>(), boltDamage * 8, 8f, Owner.whoAmI);

            if (CWRServerConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(InMousePos
                    , (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 16f, 6f, 24, 1200f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void PostDraw(Color lightColor) {
            //转速渐亮的枪身辉光
            if (spin <= 0.05f) {
                return;
            }
            Texture2D tex = TextureValue;
            SpriteEffects fx = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * (6f + spin * 8f));
            Color glow = new Color(130, 170, 255, 0) * (spin * 0.6f * pulse);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, tex.GetRectangle(Projectile.frame, FrameCount)
                , glow, Projectile.rotation, tex.GetOrig(FrameCount), Projectile.scale * 1.04f, fx, 0);

            //神性进度：枪口星核随积累渐盛，审判待发时白炽刺目
            float divinity01 = judgmentPrimed ? 1f : divinity / DivinityMax;
            if (divinity01 > 0.05f) {
                Texture2D star = CWRAsset.StarTexture_White.Value;
                Color starColor = new Color(180, 220, 255, 0) * (divinity01 * pulse);
                Main.EntitySpriteDraw(star, MuzzlePos - Main.screenPosition, null, starColor
                    , Main.GlobalTimeWrappedHourly * 3f, star.GetOrig(), 0.08f + divinity01 * 0.22f, fx, 0);
            }
        }
    }

    /// <summary>
    /// 凛冬弹——机枪扫射的霜蚀弹头
    /// </summary>
    internal class SolsticeBolt : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.extraUpdates = 2;
            Projectile.light = 0.25f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch
                    , -Projectile.velocity * 0.1f, 0, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 180);

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 orig = star.GetOrig();

            //霜蚀流光残尾
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - k / (float)Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[k] + Projectile.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(star, trailPos, null, new Color(120, 190, 255, 0) * (0.4f * fade)
                    , Projectile.rotation, orig, Projectile.scale * 0.16f * fade, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(star, drawPos, null, new Color(200, 235, 255, 0)
                , Projectile.rotation, orig, Projectile.scale * 0.22f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 寒魂晶——高转速下抛出的追猎冰晶，远距平滑转向，近距直扑
    /// </summary>
    internal class SolsticeSoul : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "Crystal";

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.light = 0.35f;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);

            if (++Projectile.ai[0] > 20) {
                NPC target = Projectile.Center.FindClosestNPC(600, false, true);
                if (target != null) {
                    if (target.Center.Distance(Projectile.Center) > 120) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.22f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch
                    , -Projectile.velocity * 0.15f, 0, default, 1f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 240);
            target.AddBuff(BuffID.Chilled, 120);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, tex.GetRectangle(Projectile.frame, 4)
                , Color.White, Projectile.rotation, tex.GetOrig(4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 冬至审判——天降的极寒神罚光柱
    /// <br/>生成于光标处并向下吸附至地面；首帧起爆：
    /// 落点绽放冻爆环，沿光柱迸起浮空冰枪，贴地向两侧掀起渐高的冰枪阵
    /// <br/>光柱本体由 FrostJudgment.fx 渲染（uv.x: 0=落点 → 1=高空羽散）
    /// </summary>
    internal class SolsticeJudgment : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        /// <summary>是否已完成首帧起爆</summary>
        private ref float Detonated => ref Projectile.localAI[0];
        /// <summary>落点是否吸附到了地面</summary>
        private bool grounded;

        private const int LifeTime = 36;
        private const float SkyLength = 1600f;
        private const float BeamWidth = 64f;
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
            if (Detonated == 0) {
                Detonated = 1;
                SnapToGround();
                Detonate();
            }

            Vector2 mid = Projectile.Center + new Vector2(0, -SkyLength * 0.3f);
            Lighting.AddLight(Projectile.Center, 0.5f * Fade, 0.7f * Fade, 1f * Fade);
            Lighting.AddLight(mid, 0.3f * Fade, 0.45f * Fade, 0.7f * Fade);
        }

        /// <summary>从生成点向下吸附至地面，悬空过深则原地起爆</summary>
        private void SnapToGround() {
            Vector2 impact = Projectile.Center;
            for (int i = 0; i < 60; i++) {
                Vector2 probe = impact + new Vector2(0, i * 16);
                if (Framing.GetTileSafely(probe).HasSolidTile()) {
                    Projectile.Center = new Vector2(impact.X, (int)(probe.Y / 16) * 16);
                    grounded = true;
                    return;
                }
            }
        }

        /// <summary>首帧起爆：冻爆环 + 寒雾迸发 + 光柱浮冰 + 贴地冰枪阵</summary>
        private void Detonate() {
            Vector2 impact = Projectile.Center;

            if (!Main.dedServ) {
                //沿光柱升腾的冰星
                for (int i = 0; i < 40; i++) {
                    Vector2 pos = impact + new Vector2(Main.rand.NextFloat(-BeamWidth, BeamWidth) * 0.5f, -Main.rand.NextFloat(SkyLength * 0.7f));
                    Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.BlueCrystalShard
                        , new Vector2(0, -Main.rand.NextFloat(2f, 7f)), 0, default, Main.rand.NextFloat(1.1f, 1.8f));
                    d.noGravity = true;
                }
                //落点炸开
                for (int i = 0; i < 24; i++) {
                    Dust d = Dust.NewDustPerfect(impact, DustID.SnowflakeIce
                        , Main.rand.NextVector2Circular(8f, 8f), 100, default, Main.rand.NextFloat(1.4f, 2.4f));
                    d.noGravity = true;
                }
            }

            if (Main.myPlayer != Projectile.owner) {
                return;
            }

            //落点冻爆环
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), impact, Vector2.Zero
                , ModContent.ProjectileType<SolsticeNova>(), (int)(Projectile.damage * 0.4f), 8f, Projectile.owner);

            //寒雾迸泉
            for (int i = 0; i < 12; i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), impact
                    , new Vector2(0, -Main.rand.NextFloat(4f, 13f)).RotatedByRandom(0.55f)
                    , ModContent.ProjectileType<IceExplosionFriend>(), (int)(Projectile.damage * 0.25f), 2f, Projectile.owner);
            }

            //沿光柱浮空迸起的巨型冰枪
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -3f);
                SpawnIceSpike(impact + new Vector2(Main.rand.NextFloat(-14f, 14f), i * -34f), velocity
                    , (int)(Projectile.damage * 0.35f), 1.0f + i * 0.06f);
            }

            //贴地向两侧掀起渐高的冰枪阵
            if (grounded) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    Vector2 line = new Vector2(dir * 3f, -0.5f);
                    for (int i = 1; i <= 8; i++) {
                        Vector2 velocity = line - new Vector2(0, Main.rand.NextFloat(0.3f));
                        SpawnIceSpike(impact + line * i * 18f, velocity
                            , (int)(Projectile.damage * 0.3f), 1.0f + i * 0.1f);
                    }
                }
            }
        }

        /// <summary>以友方形式迸出一根冰川之枪（借用鹿角怪冰刺的形体）</summary>
        private void SpawnIceSpike(Vector2 pos, Vector2 velocity, int damage, float scale) {
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), pos, velocity
                , ProjectileID.DeerclopsIceSpike, damage, 0f, Projectile.owner, 0f, scale);
            proj.rotation = velocity.ToRotation();
            proj.hostile = false;
            proj.friendly = true;
            proj.penetrate = -1;
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = -1;
            proj.light = 0.75f;
        }

        //只在前10帧造成伤害，余下时间为渐隐余辉
        public override bool? CanDamage() => Projectile.timeLeft > LifeTime - 10 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 top = Projectile.Center + new Vector2(0, -SkyLength);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, top, BeamWidth, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 360);
            target.AddBuff(BuffID.Chilled, 240);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.FrostJudgment?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Detonated == 0) {
                return;
            }

            //垂直光柱：uv.x=0 在落点（迅速亮起），uv.x=1 在高空（羽散）
            float halfW = BeamWidth * (0.35f + Fade * 0.65f);
            Vector2 start = Projectile.Center + new Vector2(0, 8);
            Vector2 end = start + new Vector2(0, -SkyLength);

            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((start - new Vector2(halfW, 0)).ToVector3(), Color.White, new Vector2(0, 0));
            quad[1] = new VertexPositionColorTexture((end - new Vector2(halfW, 0)).ToVector3(), Color.White, new Vector2(1, 0));
            quad[2] = new VertexPositionColorTexture((start + new Vector2(halfW, 0)).ToVector3(), Color.White, new Vector2(0, 1));
            quad[3] = new VertexPositionColorTexture((end + new Vector2(halfW, 0)).ToVector3(), Color.White, new Vector2(1, 1));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(Fade);
            effect.Parameters["uCharge"]?.SetValue(1f);
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
    /// 极夜新星——冬至审判落点绽放的冻爆冕环
    /// </summary>
    internal class SolsticeNova : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int LifeTime = 24;

        private float Progress => 1f - Projectile.timeLeft / (float)LifeTime;
        private float Radius => 340f * (1f - MathF.Pow(1f - Progress, 3f));
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
