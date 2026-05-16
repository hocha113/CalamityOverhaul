using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.NeutronBows;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 黑洞使者 — 战士向的中子飞镰
    /// 左键：抛出一柄高速自旋的飞镰，沿途留下翘曲点并向敌人射出伽马射线
    /// 右键：黑洞爆发，向四周同时投掷十三柄飞镰
    /// </summary>
    internal class NeutronScythe : ModItem
    {
        public override string Texture => CWRConstant.Item + "Melee/NeutronScythe";

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 13));
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 482;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = 11;
            Item.useAnimation = 11;
            Item.shootSpeed = 17f;
            Item.knockBack = 7.5f;
            Item.crit = 16;
            Item.value = Item.buyPrice(12, 73, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<NeutronScytheHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronScythe;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 22;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                return player.CWR().CustomCooldownCounter <= 0;
            }
            //左键允许场上同时存在 5 把飞镰用于堆叠输出
            return player.ownedProjectileCounts[Item.shoot] <= 4;
        }

        public override float UseSpeedMultiplier(Player player) {
            //右键开大稍慢，避免抽风式连发
            return player.altFunctionUse == 2 ? 0.55f : 1f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse != 2) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.65f }, position);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }

            //右键 — 黑洞爆发：替换原潜伏攻击，向四周丢出一整圈飞镰
            SoundEngine.PlaySound(CWRSound.BlackHole with { Pitch = -0.1f, Volume = 0.9f }, player.Center);
            if (CWRServerConfig.Instance.ScreenVibration) {
                Vector2 shakeDir = velocity.SafeNormalize(Vector2.UnitX);
                PunchCameraModifier modifier = new PunchCameraModifier(player.Center, shakeDir, 6f, 7f, 14, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            const int count = 13;
            for (int i = 0; i < count; i++) {
                Vector2 vr = (MathHelper.TwoPi / count * i).ToRotationVector2() * Item.shootSpeed * 0.85f;
                Projectile.NewProjectile(source, player.Center, vr, type
                    , (int)(damage * 0.7f), knockback, player.whoAmI, ai2: 1f);
            }

            player.CWR().CustomCooldownCounter = 180;
            return false;
        }
    }

    /// <summary>
    /// 中子镰飞行体 — 旋飞回旋型近战弹幕，替换原 <see cref="BaseThrowable"/> 设计
    /// 阶段：飞出 → 末端制动追踪 → 高速回收
    /// </summary>
    internal class NeutronScytheHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Melee/NeutronScythe";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<NeutronScythe>()).DisplayName;

        //ai[2] 由 Item.Shoot 写入：0 = 普通飞镰，1 = 黑洞爆发飞镰
        private bool IsBurst => Projectile.ai[2] > 0.5f;

        private const int OutboundTime = 80;
        private const int LaserInterval = 12;
        private const int WarpInterval = 20;
        private const float ReturnSpeed = 30f;
        private const float MaxStraySqr = 1700f * 1700f;

        private bool returning;
        private bool playedReturnSound;
        private int firePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            if (IsBurst) {
                Projectile.scale = 1.2f;
            }

            //出手粒子，强化"砸出去"的力量感
            if (!Main.dedServ) {
                Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 8; i++) {
                    float spread = Main.rand.NextFloat(-0.55f, 0.55f);
                    Vector2 vel = forward.RotatedBy(spread) * Main.rand.NextFloat(2.5f, 7f);
                    PRT_Spark spark = new PRT_Spark(Projectile.Center, vel
                        , false, Main.rand.Next(10, 18)
                        , Main.rand.NextFloat(1f, 1.8f), Color.CornflowerBlue);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //高速自旋 — 镰刀本体随飞行速度滚转，方向跟随抛出方向
            float spinSign = Projectile.velocity.X >= 0 ? 1f : -1f;
            Projectile.rotation += Projectile.velocity.Length() * 0.035f * spinSign;
            VaultUtils.ClockFrame(ref Projectile.frame, 3, 12);

            float distToOwner = Projectile.Distance(Owner.Center);

            if (!returning) {
                //阶段 1：抛出 — 缓慢减速，末段做轻微追踪
                Projectile.ai[0]++;
                Projectile.velocity *= 0.985f;

                if (Projectile.ai[0] > OutboundTime * 0.5f) {
                    NPC target = Projectile.Center.FindClosestNPC(440f);
                    if (target != null) {
                        Projectile.SmoothHomingBehavior(target.Center, 1.04f, 0.08f);
                    }
                }

                if (Projectile.ai[0] >= OutboundTime || Projectile.DistanceSQ(Owner.Center) > MaxStraySqr) {
                    returning = true;
                }
            }
            else {
                //阶段 2：高速回收 — 加快反馈，让玩家立即能再次抛出
                if (!playedReturnSound) {
                    SoundEngine.PlaySound(SoundID.Item7 with {
                        Pitch = IsBurst ? -0.3f : 0.15f,
                        Volume = 0.45f
                    }, Projectile.Center);
                    playedReturnSound = true;
                }

                Vector2 toOwner = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float chaseSpeed = (IsBurst ? 32f : ReturnSpeed) + Projectile.ai[0] * 0.04f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner * chaseSpeed, 0.18f);

                if (distToOwner < 40f) {
                    Projectile.Kill();
                    return;
                }
            }

            //保留原"飞行途中射伽马射线 + 留下翘曲点"的攻击循环
            firePhase++;
            if (firePhase % LaserInterval == 0) {
                FireGammaLaser();
            }
            if (firePhase % WarpInterval == 0) {
                SpawnWarpPoint(Projectile.Center, Projectile.damage / 3);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.55f, 0.95f));

            //航迹粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustVel = Projectile.velocity.RotatedByRandom(0.4f) * -0.2f;
                PRT_Spark spark = new PRT_Spark(Projectile.Center + Main.rand.NextVector2Circular(8, 8)
                    , dustVel, false, 12, Main.rand.NextFloat(0.9f, 1.4f)
                    , IsBurst ? Color.BlueViolet : Color.CadetBlue);
                PRTLoader.AddParticle(spark);
            }
        }

        private void FireGammaLaser() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            NPC target = Projectile.Center.FindClosestNPC(1200f);
            if (target == null) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.Pecharge with {
                Pitch = -0.6f + Main.rand.NextFloat(-0.1f, 0.2f),
                Volume = 0.45f
            }, Projectile.Center);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                , Projectile.Center.To(target.Center).UnitVector() * 22f
                , ModContent.ProjectileType<NeutronLaser>(), Projectile.damage / 3, 0f, Projectile.owner);
        }

        private void SpawnWarpPoint(Vector2 pos, int dmg) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, Vector2.Zero
                , ModContent.ProjectileType<NeutronScytheExplosion>(), dmg, 0f, Projectile.owner);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中即时引爆翘曲点，强化战士向击打反馈
            SpawnWarpPoint(target.Center, Projectile.damage / 2);

            SoundEngine.PlaySound(SoundID.NPCHit5 with { Pitch = 0.2f, Volume = 0.5f }, target.Center);

            if (CWRServerConfig.Instance.ScreenVibration && Projectile.numHits == 0) {
                Vector2 hitDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                PunchCameraModifier modifier = new PunchCameraModifier(target.Center, hitDir, 2.2f, 4f, 6, 400f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                PRT_Spark spark = new PRT_Spark(target.Center, vel, false
                    , Main.rand.Next(10, 18), Main.rand.NextFloat(1.2f, 2f), Color.CornflowerBlue);
                PRTLoader.AddParticle(spark);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureValue;
            Rectangle frameRect = texture.GetRectangle(Projectile.frame, 13);
            Vector2 origin = frameRect.Size() / 2f;
            SpriteEffects effects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (MathHelper.PiOver4 + 0.35f) * (Projectile.velocity.X > 0 ? 1 : -1);

            //余像拖尾 — 强调高速旋飞
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float prog = 1f - k / (float)Projectile.oldPos.Length;
                Color trailColor = (IsBurst ? Color.BlueViolet : Color.CornflowerBlue) * (prog * 0.45f);
                trailColor.A = 0;
                Vector2 trailPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.6f, 1f, prog);
                Main.EntitySpriteDraw(texture, trailPos, frameRect, trailColor
                    , drawRot, origin, trailScale, effects, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frameRect, lightColor
                , drawRot, origin, Projectile.scale, effects, 0);
            return false;
        }
    }

    /// <summary>
    /// 中子镰的翘曲爆破点 — 替代原 <c>NeutronExplosionRogue</c>
    /// </summary>
    internal class NeutronScytheExplosion : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 110;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 6;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 13; j++) {
                        BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                            , vr * 0.24f, false, 30, 1.2f, Color.CornflowerBlue);
                        PRTLoader.AddParticle(spark);
                    }
                }
                Projectile.ai[2]++;
            }

            Projectile.ai[0] += 0.15f;
            if (Projectile.timeLeft > 10) {
                Projectile.localAI[0] += 0.06f;
                Projectile.ai[1] += 0.1f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.7f, 1f));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 320f * scale,
                screenHeight: 320f * scale,
                intensity: Projectile.ai[1] * 0.7f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "ShockwaveRing"
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
