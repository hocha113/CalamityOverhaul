using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTerrorBlade : CWRItemOverride, ICWRLoader
    {
        public const float TerrorBladeMaxRageEnergy = 100; //减少最大能量
        public static Asset<Texture2D> frightEnergyBarAsset;
        public static Asset<Texture2D> frightEnergyBackAsset;

        void ICWRLoader.SetupData() {
            if (!Main.dedServ) {
                frightEnergyBarAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeBar");
                frightEnergyBackAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeBack");
            }
        }

        void ICWRLoader.UnLoadData() {
            frightEnergyBarAsset = null;
            frightEnergyBackAsset = null;
        }

        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 88;
            Item.damage = 510;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 80;
            Item.shoot = ModContent.ProjectileType<WraithBeam>();
            Item.shootSpeed = 20f;
            Item.SetKnifeHeld<TerrorBladeHeld>();
        }

        public static void DrawFrightEnergyChargeBar(Player player, float alp, float charge) {
            Item item = player.GetItem();
            if (item.IsAir) {
                return;
            }

            Texture2D rageEnergyBar = frightEnergyBarAsset.Value;
            Texture2D rageEnergyBack = frightEnergyBackAsset.Value;

            float slp = 1;
            Vector2 drawPos = player.GetPlayerStabilityCenter() + new Vector2(rageEnergyBack.Width / -2, 120) - Main.screenPosition;
            int width = (int)(rageEnergyBar.Width * charge);
            if (width > rageEnergyBar.Width) {
                width = rageEnergyBar.Width;
            }
            Rectangle backRec = new Rectangle(0, 0, width, rageEnergyBar.Height);

            //能量条颜色随充能变化
            Color barColor = charge >= 1f ? Color.Red : Color.Lerp(Color.DarkRed, Color.Red, charge);

            Main.EntitySpriteDraw(rageEnergyBack, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(rageEnergyBar, drawPos + new Vector2(8, 6) * slp, backRec, barColor * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
        }
    }

    internal class TerrorBladeHeld : BaseKnife
    {
        public override int TargetID => CWRItemOverride.GetCalItemID("TerrorBlade");
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "TerrorBlade_Bar";
        public override string GlowTexturePath => CWRConstant.Cay_Wap_Melee + "TerrorBladeGlow";

        private int comboPhase; //连击阶段 0-2为普通攻击，3为终结技
        private bool isFinisher; //是否是终结技
        private float powerMomentum; //力量动量

        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 92;
            canDrawSlashTrail = true;
            distanceToOwner = -35;
            drawTrailBtommWidth = 35;
            drawTrailTopWidth = 30;
            drawTrailCount = 18;
            Length = 100;
            unitOffsetDrawZkMode = -10;
            overOffsetCachesRoting = MathHelper.ToRadians(10);
            SwingData.starArg = 80;
            SwingData.baseSwingSpeed = 6f; //提升基础速度
            SwingData.ler1_UpLengthSengs = 0.12f;
            SwingData.minClampLength = 100;
            SwingData.maxClampLength = 110;
            SwingData.ler1_UpSizeSengs = 0.065f;
            ShootSpeed = 18;
        }

        public override void KnifeInitialize() {
            if (++Item.CWR().MeleeCharge > 3) {
                Item.CWR().MeleeCharge = 0;
            }
        }

        public override bool PreInOwner() {
            //力量动量衰减
            powerMomentum *= 0.93f;

            //判断连击阶段
            comboPhase = (int)Item.CWR().MeleeCharge;
            isFinisher = comboPhase == 3;

            if (comboPhase >= 0 && comboPhase <= 2) {
                //普通连击 - 快速流畅
                SwingAIType = SwingAITypeEnum.None;
                canDrawSlashTrail = true;

                SwingData.baseSwingSpeed = 4f + comboPhase * 0.2f; //连击加速
                SwingData.starArg = 75 + comboPhase * 5;
                SwingData.ler1_UpSpeedSengs = 0.14f + comboPhase * 0.02f;

                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Pitch = -0.2f + comboPhase * 0.15f,
                        Volume = 1.1f + comboPhase * 0.1f
                    }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = 0.2f }, Owner.Center);
                    powerMomentum = 8f + comboPhase * 2f;
                }

                //连击粒子拖尾
                if (Time % 2 == 0 && rotSpeed > 0.12f) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(55, 55);
                    Dust trail = Dust.NewDustPerfect(dustPos, DustID.RedTorch,
                        -Projectile.velocity * 0.4f, 0, Color.DarkRed, 1.8f);
                    trail.noGravity = true;
                    trail.velocity += Owner.velocity * 0.3f;
                }
            }
            else if (isFinisher) {
                //终结技，强力蓄力斩击
                SwingAIType = SwingAITypeEnum.None;
                canDrawSlashTrail = false;
                shootSengs = 0.25f;
                maxSwingTime = 70;

                SwingData.starArg = 15;
                SwingData.baseSwingSpeed = 2f;
                SwingData.ler1_UpLengthSengs = 0.16f;
                SwingData.ler1_UpSpeedSengs = 0.16f;
                SwingData.ler1_UpSizeSengs = 0.085f;
                SwingData.ler2_DownLengthSengs = 0.01f;
                SwingData.ler2_DownSpeedSengs = 0.25f;
                SwingData.maxSwingTime = 45;
                SwingData.minClampLength = 160;
                SwingData.maxClampLength = 180;

                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f, Volume = 1.5f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = 0.8f, Volume = 0.7f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.NPCDeath39, Owner.Center);
                    powerMomentum = 25f;
                }

                //蓄力进度
                float chargeProgress = MathHelper.Clamp(Time / (maxSwingTime * shootSengs), 0f, 1f);

                //蓄力灵魂环绕
                if (Time % 2 == 0) {
                    float angle = Time * 0.25f;
                    Vector2 offset = angle.ToRotationVector2() * (70 + chargeProgress * 50);
                    Dust charge = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Wraith,
                        -offset * 0.1f, 0, Color.Purple, 2f + chargeProgress * 0.8f);
                    charge.noGravity = true;
                }

                //蓄力完成光环
                if (chargeProgress >= 0.8f && Time % 3 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Vector2 sparkPos = Projectile.Center + Main.rand.NextVector2Circular(90, 90);
                        Dust spark = Dust.NewDustPerfect(sparkPos, DustID.RedTorch, Vector2.Zero, 0, Color.Red, 2.5f);
                        spark.noGravity = true;
                    }
                }

                //挥击瞬间爆发
                if (Time == (int)(maxSwingTime * shootSengs)) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1.2f }, Owner.Center);

                    //爆发粒子环
                    for (int i = 0; i < 30; i++) {
                        Vector2 vel = Main.rand.NextVector2Circular(12, 12);
                        Dust explosion = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 0, Color.Purple, 2.5f);
                        explosion.noGravity = true;
                    }
                }
            }

            if (!isFinisher
                && Time > 5 * UpdateRate && Time < 60 * UpdateRate && Time % 30 * UpdateRate == 0
                && Projectile.IsOwnedByLocalPlayer()) {
                Vector2 velocity = ShootVelocity.RotatedByRandom(0.22f);
                Projectile.NewProjectile(
                    Source,
                    ShootSpanPos,
                    velocity,
                    ModContent.ProjectileType<TerrorSpirit>(),
                    (int)(Projectile.damage * 0.6f),
                    Projectile.knockBack * 0.5f,
                    Owner.whoAmI,
                    1, //状态：直线飞行
                    0
                );
            }

            return true;
        }

        public override void Shoot() {
            if (isFinisher) {
                //终结技 - 释放追踪灵魂
                SoundEngine.PlaySound(SoundID.NPCDeath39 with { Volume = 1.2f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.3f }, Owner.Center);

                int spiritCount = 9;
                spiritCount = Math.Clamp(spiritCount, 3, 12);

                for (int i = 0; i < spiritCount; i++) {
                    Vector2 velocity = safeInSwingUnit.RotatedBy((-spiritCount / 2f + i) * 0.15f) * Main.rand.NextFloat(8f, 14f);
                    Projectile.NewProjectile(
                        Source,
                        Owner.Center,
                        velocity,
                        ModContent.ProjectileType<TerrorSpirit>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Owner.whoAmI,
                        0, //状态：追踪敌人
                        Main.rand.NextFloat(0.8f, 1.2f) //随机化
                    );
                }

                //爆发特效
                for (int i = 0; i < 50; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(15, 15);
                    Dust spirit = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 0, Color.Purple, 2.5f);
                    spirit.noGravity = true;
                }
                return;
            }

            SoundEngine.PlaySound(SoundID.Item20 with { MaxInstances = 6, Pitch = 0.2f }, Owner.Center);

            for (int i = 0; i < 2; i++) {
                Vector2 velocity = ShootVelocity.RotatedBy((-0.5f + i) * 0.2f) * 0.9f;
                Projectile.NewProjectile(
                    Source,
                    ShootSpanPos,
                    velocity,
                    ModContent.ProjectileType<TerrorSpirit>(),
                    (int)(Projectile.damage * 0.6f),
                    Projectile.knockBack * 0.5f,
                    Owner.whoAmI,
                    1, //状态：直线飞行
                    0
                );
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //施加灵魂燃烧debuff
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);

            //根据攻击类型给予不同反馈
            if (isFinisher) {
                //终结技击中
                SoundEngine.PlaySound(SoundID.NPCHit53 with { Pitch = -0.4f, Volume = 1.2f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item14, target.Center);

                //强力灵魂爆发
                for (int i = 0; i < 25; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(10, 10);
                    Dust explosion = Dust.NewDustPerfect(target.Center, DustID.Wraith, vel, 0, Color.Purple, 2.5f);
                    explosion.noGravity = true;
                }

                for (int i = 0; i < 15; i++) {
                    Dust fire = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(30, 30),
                        DustID.RedTorch, Main.rand.NextVector2Circular(6, 6), 0, Color.DarkRed, 2f);
                    fire.noGravity = true;
                }
            }
            else {
                //普通连击击中
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundMiss with {
                    Pitch = 0.3f + comboPhase * 0.1f,
                    Volume = 0.7f
                }, target.Center);

                for (int i = 0; i < 8 + comboPhase * 2; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                    Dust hitDust = Dust.NewDustPerfect(target.Center,
                        Main.rand.NextBool() ? DustID.Wraith : DustID.RedTorch,
                        vel, 0, default, 1.8f);
                    hitDust.noGravity = true;
                }
            }

            if (hit.Crit) {
                //暴击额外粒子
                for (int i = 0; i < 5; i++) {
                    Dust crit = Dust.NewDustPerfect(target.Center, DustID.RedTorch,
                        Main.rand.NextVector2Circular(5, 5), 0, Color.Red, 2.2f);
                    crit.noGravity = true;
                }
            }

            powerMomentum += 2f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);
        }

        public override void MeleeEffect() {
            //力量动量粒子
            if (powerMomentum > 8f && Main.rand.NextBool(3)) {
                float momentum = MathHelper.Clamp(powerMomentum / 25f, 0f, 1f);
                Dust power = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RedTorch);
                power.velocity *= 0.5f;
                power.scale = 1.5f * momentum;
                power.noGravity = true;
            }
        }

        public override void PostDrawSwing(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPos, Rectangle rectangle, Color color
            , float drawRoting, Vector2 drawOrigin, float scale, SpriteEffects spriteEffects) {
            if (!isFinisher) {
                return;
            }
            float chargeIntensity = MathHelper.Clamp(Main.GameUpdateCount / 60f, 0f, 1f);
            Color glowColor = Color.Lerp(Color.Red, Color.OrangeRed, chargeIntensity) * (0.5f + chargeIntensity * 0.3f);

            //多层光晕营造蓄力感
            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time, drawPos, null, glowColor * 0.8f
                , drawRoting, drawOrigin, scale * (1f + chargeIntensity * 0.02f),
                DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            Main.EntitySpriteDraw(texture, drawPos, rectangle
                , color, drawRoting, drawOrigin, scale, spriteEffects, 0);
        }
    }

    internal class TerrorSpirit : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "NofaceFire";

        public ref float Status => ref Projectile.ai[0]; //0=追踪 1=直线
        public ref float RandomSeed => ref Projectile.ai[1];

        private int targetNPC = -1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 42;
            Projectile.height = 42;
            Projectile.scale = 1.3f + RandomSeed * 0.3f;
            Projectile.alpha = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3; //允许穿透
            Projectile.timeLeft = 280;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            VaultUtils.ClockFrame(ref Projectile.frameCounter, 5, 2);

            if (Status == 0) {
                //追踪模式
                HuntingBehavior();
            }
            else {
                //直线飞行模式
                StraightFlightBehavior();
            }

            //淡入效果
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 6;
            }

            //生命末期淡出
            if (Projectile.timeLeft < 60) {
                Projectile.alpha = (int)MathHelper.Lerp(Projectile.alpha, 255, 1f - Projectile.timeLeft / 60f);
            }

            //环境粒子效果
            if (Main.rand.NextBool(4)) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    DustID.Wraith,
                    -Projectile.velocity * 0.3f,
                    0, Color.Purple,
                    1.2f
                );
                trail.noGravity = true;
            }

            //光照效果
            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.5f);
        }

        private void HuntingBehavior() {
            //寻找或更新目标
            if (targetNPC == -1 || !Main.npc[targetNPC].active || Main.npc[targetNPC].friendly) {
                var npc = Projectile.Center.FindClosestNPC(1800f, true, true);
                targetNPC = npc != null ? npc.whoAmI : -1;
            }

            if (targetNPC != -1 && Projectile.timeLeft < 220) {
                NPC target = Main.npc[targetNPC];
                Projectile.SmoothHomingBehavior(target.Center, Projectile.numHits == 0 ? 1.02f : 1f, 0.2f);

                //追踪粒子效果
                if (Main.rand.NextBool(6)) {
                    Dust chase = Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.RedTorch,
                        Projectile.velocity,
                        0, Color.DarkRed,
                        1.5f
                    );
                    chase.noGravity = true;
                }
            }
            else {
                if (Projectile.localAI[0] == 0) {
                    Projectile.localAI[0] = Math.Sign(Projectile.velocity.X);
                }

                //无目标时螺旋飞行
                Projectile.velocity = Projectile.velocity.RotatedBy(0.02f * Projectile.localAI[0]);
                Projectile.velocity *= 0.99f;
            }

            Projectile.scale *= 1.001f; //缓慢膨胀
        }

        private void StraightFlightBehavior() {
            //直线飞行模式，轻微摆动
            float wobble = (float)Math.Sin(Projectile.timeLeft * 0.1f) * 0.02f;
            Projectile.velocity = Projectile.velocity.RotatedBy(wobble);
            Projectile.velocity *= 0.995f; //轻微减速
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.4f, Volume = 0.5f }, target.Center);

            //击中粒子
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                Dust hitDust = Dust.NewDustPerfect(target.Center, DustID.Wraith, vel, 0, Color.Purple, 1.8f);
                hitDust.noGravity = true;
            }

            //附加debuff
            target.AddBuff(BuffID.ShadowFlame, 180);
            target.AddBuff(BuffID.OnFire3, 240);

            //穿透后缩小
            Projectile.scale *= 0.85f;
            Projectile.damage = (int)(Projectile.damage * 0.9f);

            //穿透后改变方向（只在追踪模式）
            if (Status == 0) {
                targetNPC = -1; //寻找新目标
            }
        }

        public override void OnKill(int timeLeft) {
            //消散音效
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.6f, Volume = 0.4f }, Projectile.Center);

            //消散粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                Dust fade = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 0, Color.Purple, 1.8f);
                fade.noGravity = true;
                fade.fadeIn = 1.2f;
            }

            //火焰粒子
            for (int i = 0; i < 10; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                    DustID.RedTorch,
                    Main.rand.NextVector2Circular(4, 4),
                    0, Color.DarkRed,
                    1.5f
                );
                fire.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle(Projectile.frameCounter, 3);
            Vector2 origin = rectangle.Size() / 2;

            //绘制拖尾
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (k >= Projectile.oldPos.Length - 1) continue;

                Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                Vector2 drawPos = Projectile.Center - Main.screenPosition - offsetPos;
                float progress = k / (float)Projectile.oldPos.Length;
                float trailAlpha = (1f - progress) * (1f - Projectile.alpha / 255f);

                Color trailColor = Color.Lerp(Color.Purple, Color.DarkRed, progress) * trailAlpha;

                Main.EntitySpriteDraw(
                    mainValue,
                    drawPos,
                    rectangle,
                    trailColor * 0.6f,
                    Projectile.rotation - MathHelper.PiOver2,
                    origin,
                    Projectile.scale * (1f - progress * 0.3f),
                    SpriteEffects.None,
                    0
                );
            }

            //光晕效果
            float alp = (1f - Projectile.alpha / 255f);
            Color glowColor = Color.Lerp(Color.Purple, Color.Red, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f);

            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                rectangle,
                glowColor * alp * 0.4f,
                Projectile.rotation - MathHelper.PiOver2,
                origin,
                Projectile.scale * 1.2f,
                SpriteEffects.None,
                0
            );

            //主体绘制
            Color mainColor = Color.Lerp(Color.DarkRed, Color.Purple, 0.5f);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                rectangle,
                mainColor * alp,
                Projectile.rotation - MathHelper.PiOver2,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
