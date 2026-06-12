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
    /// <summary>
    /// 万象霜天
    /// <br/>左键: 高速连发霜辉弹，每一发都为霜穹蓄能
    /// <br/>右键: 蓄能满后在光标上空展开极光霜幕，幕下降下霜光贯击
    /// </summary>
    internal class UniversalFrost : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "UniversalFrost";
        /// <summary>霜穹蓄能 0~<see cref="MaxCharge"/>，跨使用持久</summary>
        internal float AuroraCharge;
        internal const float MaxCharge = 100f;
        /// <summary>蓄满提示只播一次的标记</summary>
        internal bool ChargeCueDone;
        /// <summary>弹药节流计数，跨使用持久，每2发消耗1颗雪球</summary>
        internal int GlimmerAmmoThrottle;

        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.width = 96;
            Item.height = 38;
            Item.damage = 190;
            Item.useTime = Item.useAnimation = 5;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 2.5f;
            Item.value = Terraria.Item.buyPrice(0, 32, 0, 0);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<UniversalFrostHeld>();
            Item.shootSpeed = 24f;
            Item.crit = 12;
            Item.useAmmo = AmmoID.Snowball;
        }

        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗雪球，由手持弹幕按速射节奏自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseSnowCannonHeld.AmmoConsumeContext;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[Item.shoot] > 0) {
                return false;
            }
            //蓄能不满时右键无法展开霜幕
            return player.altFunctionUse != 2 || AuroraCharge >= MaxCharge;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //使用瞬间生成手持弹幕，它会自己接管开火逻辑，松开按键后自动销毁
            Projectile.NewProjectile(source, player.MountedCenter, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_CosmiliteBar > 0 && CWRID.Item_EndothermicEnergy > 0
                && CWRID.Item_EssenceofEleum > 0 && CWRID.Tile_CosmicAnvil > 0) {
                _ = CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(CWRID.Item_CosmiliteBar, 5).
                AddIngredient(CWRID.Item_EndothermicEnergy, 20).
                AddIngredient(CWRID.Item_EssenceofEleum, 3).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(ItemID.LunarBar, 8).
                AddTile(TileID.LunarCraftingStation).
                Register();
            }
        }
    }

    /// <summary>
    /// 万象霜天手持弹幕
    /// <br/>帧0-3: 开火循环, 帧4: 待机
    /// </summary>
    internal class UniversalFrostHeld : BaseSnowCannonHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "UniversalFrostHeld";
        public override int TargetItemID => ModContent.ItemType<UniversalFrost>();
        protected override int FrameCount => 5;
        protected override float BarrelLength => 50f;
        protected override float MuzzleNormalOffset => 3f;
        protected override float HoldDistance => 22f;

        /// <summary>开火动画余辉</summary>
        private int fireAnimTime;

        private UniversalFrost WeaponItem => Item.ModItem as UniversalFrost;
        //开火动画播完之前不销毁
        protected override bool PendingWork => fireAnimTime > 0;

        protected override void UpdateGun() {
            if (fireAnimTime > 0) {
                fireAnimTime--;
                VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
            }
            else {
                Projectile.frame = 4;
            }

            //蓄满提示
            if (WeaponItem.AuroraCharge >= UniversalFrost.MaxCharge && !WeaponItem.ChargeCueDone) {
                WeaponItem.ChargeCueDone = true;
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.4f, Volume = 0.9f }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 18; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch
                            , Main.rand.NextVector2CircularEdge(3f, 3f), 0, default, 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (FireKeyLeft && cooldown <= 0) {
                FireGlimmer();
            }

            if (FireKeyRight && WeaponItem.AuroraCharge >= UniversalFrost.MaxCharge && cooldown <= 0) {
                DeployAurora();
            }
        }

        /// <summary>速射霜辉弹</summary>
        private void FireGlimmer() {
            //每2发消耗1颗雪球，节流计数存放在物品上跨使用持久
            bool consume = ++WeaponItem.GlimmerAmmoThrottle >= 2;
            if (consume) {
                WeaponItem.GlimmerAmmoThrottle = 0;
            }
            if (!PickSnowAmmo(out int damage, out float knockback, consume)) {
                return;
            }

            cooldown = 5;
            fireAnimTime = 8;
            recoil = 2.5f;

            if (WeaponItem.AuroraCharge < UniversalFrost.MaxCharge) {
                WeaponItem.AuroraCharge += 10f;
            }

            SoundEngine.PlaySound(CWRSound.Gun_Snowblindness_Shoot with {
                Volume = 0.2f,
                Pitch = 0.1f + WeaponItem.AuroraCharge / UniversalFrost.MaxCharge * 0.2f,
                MaxInstances = 8
            }, Projectile.Center);

            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(MuzzlePos, DustID.IceTorch
                        , GunForward.RotatedByRandom(0.25f) * Main.rand.NextFloat(2f, 6f), 0, default, 1.1f);
                    d.noGravity = true;
                }
            }

            Vector2 velocity = GunForward.RotatedByRandom(0.045f) * 24f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), MuzzlePos, velocity
                , ModContent.ProjectileType<FrostGlimmer>(), damage, knockback, Owner.whoAmI);
            NetUpdate();
        }

        /// <summary>展开极光霜幕</summary>
        private void DeployAurora() {
            WeaponItem.AuroraCharge = 0;
            WeaponItem.ChargeCueDone = false;
            cooldown = 6;
            recoil = 6f;

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item120 with { Pitch = -0.2f, Volume = 0.8f }, Owner.Center);

            //幕体悬在光标上空
            Vector2 deployPos = InMousePos + new Vector2(0, -160);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), deployPos, Vector2.Zero
                , ModContent.ProjectileType<AuroraCurtain>(), Owner.GetWeaponDamage(Item), 2f, Owner.whoAmI);
            NetUpdate();
        }

        public override void PostDraw(Color lightColor) {
            //蓄能渐亮的枪身辉光
            float charge01 = WeaponItem.AuroraCharge / UniversalFrost.MaxCharge;
            if (charge01 <= 0.05f) {
                return;
            }
            Texture2D tex = TextureValue;
            SpriteEffects fx = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
            Color glow = new Color(110, 220, 255, 0) * (charge01 * 0.55f * pulse);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, tex.GetRectangle(Projectile.frame, FrameCount)
                , glow, Projectile.rotation, tex.GetOrig(FrameCount), Projectile.scale * 1.04f, fx, 0);
        }
    }

    /// <summary>
    /// 霜辉弹——高速飞行的霜光星屑，飞行途中轻微追踪
    /// </summary>
    internal class FrostGlimmer : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 9;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 200;
            Projectile.extraUpdates = 1;
            Projectile.light = 0.3f;
        }

        public override void AI() {
            Projectile.rotation += 0.3f;
            //短暂直飞后开始弱追踪
            if (++Projectile.ai[0] > 30) {
                NPC target = Projectile.Center.FindClosestNPC(420, false, true);
                if (target != null) {
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity
                        , Projectile.Center.To(target.Center).UnitVector() * Projectile.velocity.Length(), 0.045f);
                }
            }
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch
                    , -Projectile.velocity * 0.1f, 0, default, 0.95f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 180);

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D snow = TextureAssets.Item[ItemID.Snowball].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //星屑残尾
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                Vector2 trailPos = Projectile.oldPos[k] + Projectile.Size / 2 - Main.screenPosition;
                float factor = 1f - k / (float)Projectile.oldPos.Length;
                Color trailColor = new Color(100, 200, 255, 0) * (0.4f * factor);
                Main.EntitySpriteDraw(glow, trailPos, null, trailColor, 0f, glow.GetOrig()
                    , 0.5f * factor, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(star, drawPos, null, new Color(200, 240, 255, 0), Projectile.rotation
                , star.GetOrig(), 0.12f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(120, 210, 255, 0) * 0.9f, 0f
                , glow.GetOrig(), 0.7f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(snow, drawPos, null, new Color(120, 210, 255, 0) * 0.9f, 0f
                , snow.GetOrig(), 1f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 极光霜幕——由 FrostAurora.fx 渲染的天空光幕
    /// <br/>悬空展开，周期性向幕下的敌人降下霜光贯击，幕区内的敌人持续受霜灼
    /// </summary>
    internal class AuroraCurtain : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const float CurtainWidth = 720f;
        private const float CurtainHeight = 300f;
        private const int LifeTime = 360;
        private const int FadeInTime = 25;
        private const int FadeOutTime = 35;

        private float FadeIn => MathHelper.Clamp((LifeTime - Projectile.timeLeft) / (float)FadeInTime, 0f, 1f);
        private float FadeOut => MathHelper.Clamp(Projectile.timeLeft / (float)FadeOutTime, 0f, 1f);
        private float Visibility => FadeIn * FadeOut;

        public override void SetDefaults() {
            Projectile.width = (int)CurtainWidth;
            Projectile.height = (int)CurtainHeight;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            //幕下飘雪
            if (!Main.dedServ && Visibility > 0.3f && Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-0.45f, 0.45f) * CurtainWidth
                    , Main.rand.NextFloat(-0.2f, 0.5f) * CurtainHeight);
                Dust snow = Dust.NewDustPerfect(pos, DustID.SnowflakeIce
                    , new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1f, 2.5f)), 120, default, Main.rand.NextFloat(1f, 1.8f));
                snow.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.3f * Visibility, 0.7f * Visibility, 0.8f * Visibility);

            if (Visibility < 0.6f) {
                return;
            }

            //周期性贯击：从幕体上挑选位置打向幕下的敌人
            if (++Projectile.ai[0] >= 4 && Main.myPlayer == Projectile.owner) {
                Projectile.ai[0] = 0;
                NPC target = FindLanceTarget();
                if (target != null) {
                    Vector2 spawnPos = new Vector2(
                        MathHelper.Clamp(target.Center.X + Main.rand.NextFloat(-30f, 30f)
                            , Projectile.Center.X - CurtainWidth * 0.45f, Projectile.Center.X + CurtainWidth * 0.45f)
                        , Projectile.Center.Y + Main.rand.NextFloat(-40f, 40f));
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, new Vector2(0, 14f)
                        , ModContent.ProjectileType<AuroraLance>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                    Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 6 }, spawnPos);
                }
            }

            //幕区内的敌人持续霜灼
            if (Main.GameUpdateCount % 30 == 0) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.friendly && !npc.dontTakeDamage && InCurtainZone(npc.Center)) {
                        npc.AddBuff(BuffID.Frostburn2, 120);
                    }
                }
            }
        }

        /// <summary>判断坐标是否处于幕体正下方的压制区</summary>
        private bool InCurtainZone(Vector2 pos) {
            return Math.Abs(pos.X - Projectile.Center.X) < CurtainWidth * 0.5f
                && pos.Y > Projectile.Center.Y - CurtainHeight * 0.5f
                && pos.Y < Projectile.Center.Y + 900f;
        }

        /// <summary>在压制区内随机选择一个可被追击的敌人</summary>
        private NPC FindLanceTarget() {
            NPC best = null;
            int seen = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || !InCurtainZone(npc.Center)) {
                    continue;
                }
                seen++;
                if (Main.rand.NextBool(seen)) {
                    best = npc;
                }
            }
            return best;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.FrostAurora?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Visibility <= 0.02f) {
                return;
            }

            Vector2 c = Projectile.Center;
            float halfW = CurtainWidth * 0.5f * (0.6f + FadeIn * 0.4f);
            float halfH = CurtainHeight * 0.5f;

            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((c + new Vector2(-halfW, -halfH)).ToVector3(), Color.White, new Vector2(0, 0));
            quad[1] = new VertexPositionColorTexture((c + new Vector2(halfW, -halfH)).ToVector3(), Color.White, new Vector2(1, 0));
            quad[2] = new VertexPositionColorTexture((c + new Vector2(-halfW, halfH)).ToVector3(), Color.White, new Vector2(0, 1));
            quad[3] = new VertexPositionColorTexture((c + new Vector2(halfW, halfH)).ToVector3(), Color.White, new Vector2(1, 1));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(Visibility);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.37f % 10f);
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
    /// 霜光贯击——从极光幕降下的纵向光矛
    /// </summary>
    internal class AuroraLance : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.extraUpdates = 2;
            Projectile.light = 0.4f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , DustID.IceTorch, -Projectile.velocity * 0.05f, 0, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 240);

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 6 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard
                    , new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)), 0, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //光矛主体：箭头状光束贴图顺速度方向拉伸
            Main.EntitySpriteDraw(shot, drawPos, null, new Color(140, 220, 255, 0) * 0.9f
                , Projectile.rotation, shot.GetOrig(), new Vector2(0.8f, 0.22f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, drawPos, null, new Color(220, 250, 255, 0) * 0.8f
                , Projectile.rotation, shot.GetOrig(), new Vector2(0.55f, 0.12f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(150, 230, 255, 0) * 0.7f
                , 0f, glow.GetOrig(), 0.8f, SpriteEffects.None, 0);
            return false;
        }
    }
}
