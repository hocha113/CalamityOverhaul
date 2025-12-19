using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSolsticeClaymore : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.UseSound = null;
            Item.damage = 381;
            Item.SetKnifeHeld<SolsticeClaymoreHeld>();
        }
    }

    internal class SolsticeHomeBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "SeasonalKunai";
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            //为弹幕位置添加光照
            Lighting.AddLight(Projectile.Center, GetColorBySeason(Projectile).ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.ai[1] == 0f) {
                Projectile.ai[1] = 1f;
                SoundEngine.PlaySound(SoundID.Item60, Projectile.position);
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.scale -= 0.02f;
                Projectile.alpha += 30;
                if (Projectile.alpha >= 250) {
                    Projectile.alpha = 255;
                    Projectile.localAI[0] = 1f;
                }
            }
            else if (Projectile.localAI[0] == 1f) {
                Projectile.scale += 0.02f;
                Projectile.alpha -= 30;
                if (Projectile.alpha <= 0) {
                    Projectile.alpha = 0;
                    Projectile.localAI[0] = 0f;
                }
            }

            int dustType = GetDustTypeBySeason(CWRRef.GetCurrentSeason());

            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType,
                                        Projectile.velocity.X * 0.05f, Projectile.velocity.Y * 0.05f);
                Main.dust[dust].noGravity = true;
            }

            //如果是白天，弹幕行为为直射且穿透
            if (Projectile.ai[0] == 1) {
                Projectile.extraUpdates = 4;
                Projectile.penetrate = 5;
            }
            //如果是晚上，弹幕行为为追踪
            else {
                if (Projectile.timeLeft < 560) {
                    CWRRef.HomeInOnNPC(Projectile, false, 620f, 9f, 60f);
                }
            }
        }

        //根据当前季节返回对应的尘埃类型
        internal static int GetDustTypeBySeason(int season) {
            return season switch {
                1 => Utils.SelectRandom(Main.rand, [74, 157, 107]),
                2 => Utils.SelectRandom(Main.rand, [247, 228, 57]),
                3 => Utils.SelectRandom(Main.rand, [6, 259, 158]),
                0 => Utils.SelectRandom(Main.rand, [67, 229, 185]),
                _ => 0 //默认尘埃类型
            };
        }

        internal static Color GetColorBySeason(Projectile projectile) {
            //根据当前季节定义弹幕颜色
            var color = CWRRef.GetCurrentSeason() switch {
                1 => new Color(0, 250, 0, projectile.alpha),         //春季：绿色
                2 => new Color(250, 250, 0, projectile.alpha),       //夏季：黄色
                3 => new Color(250, 150, 50, projectile.alpha),        //秋季：橙色
                0 => new Color(100, 150, 250, projectile.alpha),     //冬季：淡蓝色
                _ => new Color(255, 255, 255, projectile.alpha)                  //默认颜色：白色
            };
            return color;
        }

        public override void OnKill(int timeLeft) {
            int dustType = CWRRef.GetCurrentSeason() switch {
                1 => Utils.SelectRandom(Main.rand, 245, 157, 107), //春季：绿色系尘埃
                2 => Utils.SelectRandom(Main.rand, 247, 228, 57),  //夏季：黄色系尘埃
                3 => Utils.SelectRandom(Main.rand, 6, 259, 158),     //秋季：橙色系尘埃
                0 => Utils.SelectRandom(Main.rand, 67, 229, 185),  //冬季：蓝色系尘埃
                _ => 0                                                         //默认值：无效尘埃类型
            };

            //白天模式下产生更剧烈的爆炸效果
            if (Projectile.ai[0] == 1) {
                SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
                for (int i = 0; i < 30; i++) {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, 0f, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 3f;
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
                for (int i = 1; i <= 27; i++) {
                    float factor = 30f / i;
                    Vector2 offset = Projectile.oldVelocity * factor;
                    Vector2 position = Projectile.oldPosition - offset;

                    //创建两种不同缩放和速度的尘埃效果
                    CreateDust(position, dustType, 1.8f, 0.5f);  //较大缩放，较低速度
                    CreateDust(position, dustType, 1.4f, 0.05f); //较小缩放，非常低速度
                }
            }
        }

        private void CreateDust(Vector2 position, int dustType, float scale, float velocityMultiplier) {
            int dustIndex = Dust.NewDust(position, 8, 8, dustType, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, scale);
            Dust dust = Main.dust[dustIndex];
            dust.noGravity = true;
            dust.velocity *= velocityMultiplier;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int buff = Main.dayTime ? BuffID.Daybreak : CWRID.Buff_Nightwither;
            target.AddBuff(buff, 180);
        }

        public override Color? GetAlpha(Color lightColor) => GetColorBySeason(Projectile);

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 595) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            //白天模式下绘制更大的弹幕
            float scale = Projectile.ai[0] == 1 ? Projectile.scale * 1.5f : Projectile.scale;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class SolsticeClaymoreHeld : BaseKnife
    {
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "SolsticeClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5;
            ShootSpeed = 11;
        }

        public override void Shoot() {
            //白天发射强力的穿透光束
            if (Main.dayTime) {
                int type = ModContent.ProjectileType<SolsticeHomeBeam>();
                Projectile.NewProjectile(Source, ShootSpanPos, UnitToMouseV * ShootSpeed * 1.5f, type, Projectile.damage, Projectile.knockBack, Owner.whoAmI, 1);
            }
            //晚上发射多发追踪飞弹
            else {
                for (int i = 0; i < 3; i++) {
                    Vector2 spanPos = ShootSpanPos + UnitToMouseV.GetNormalVector() * Main.rand.Next(-130, 130);
                    Vector2 ver = spanPos.To(InMousePos + UnitToMouseV * 360).UnitVector() * ShootSpeed;
                    int type = ModContent.ProjectileType<SolsticeHomeBeam>();
                    Projectile.NewProjectile(Source, spanPos, ver, type, Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI, 0);
                }
            }
        }

        public override void MeleeEffect() {
            int dustType = Main.dayTime ?
            Utils.SelectRandom(Main.rand, [
                    6,
                    259,
                    158
                ]) :
            Utils.SelectRandom(Main.rand, [
                    173,
                    27,
                    234
                ]);
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing();
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dayTime) {
                target.AddBuff(BuffID.Daybreak, 300);
            }
            else {
                target.AddBuff(CWRID.Buff_Nightwither, 300);
            }

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (!Main.dayTime) {
                target.AddBuff(CWRID.Buff_Nightwither, 300);
            }
        }
    }
}

