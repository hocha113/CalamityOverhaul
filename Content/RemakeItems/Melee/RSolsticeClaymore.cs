using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSolsticeClaymore : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SolsticeClaymore>();
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
        }

        public override void AI() {
            // 为弹幕位置添加光照
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

            int dustType = GetDustTypeBySeason(CalamityMod.CalamityMod.CurrentSeason);

            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType,
                                        Projectile.velocity.X * 0.05f, Projectile.velocity.Y * 0.05f);
                Main.dust[dust].noGravity = true;
            }

            if (Projectile.timeLeft < 560) {
                CalamityUtils.HomeInOnNPC(Projectile, false, 620f, 9f, 60f);
            }
        }

        // 根据当前季节返回对应的尘埃类型
        internal static int GetDustTypeBySeason(Season season) {
            return season switch {
                Season.Spring => Utils.SelectRandom(Main.rand, new[] { 74, 157, 107 }),
                Season.Summer => Utils.SelectRandom(Main.rand, new[] { 247, 228, 57 }),
                Season.Fall => Utils.SelectRandom(Main.rand, new[] { 6, 259, 158 }),
                Season.Winter => Utils.SelectRandom(Main.rand, new[] { 67, 229, 185 }),
                _ => 0 // 默认尘埃类型
            };
        }

        internal static Color GetColorBySeason(Projectile projectile) {
            // 根据当前季节定义弹幕颜色
            var color = CalamityMod.CalamityMod.CurrentSeason switch {
                Season.Spring => new Color(0, 250, 0, projectile.alpha),         // 春季：绿色
                Season.Summer => new Color(250, 250, 0, projectile.alpha),       // 夏季：黄色
                Season.Fall => new Color(250, 150, 50, projectile.alpha),        // 秋季：橙色
                Season.Winter => new Color(100, 150, 250, projectile.alpha),     // 冬季：淡蓝色
                _ => new Color(255, 255, 255, projectile.alpha)                  // 默认颜色：白色
            };
            return color;
        }

        public override void OnKill(int timeLeft) {
            int dustType = CalamityMod.CalamityMod.CurrentSeason switch {
                Season.Spring => Utils.SelectRandom(Main.rand, 245, 157, 107), // 春季：绿色系尘埃
                Season.Summer => Utils.SelectRandom(Main.rand, 247, 228, 57),  // 夏季：黄色系尘埃
                Season.Fall => Utils.SelectRandom(Main.rand, 6, 259, 158),     // 秋季：橙色系尘埃
                Season.Winter => Utils.SelectRandom(Main.rand, 67, 229, 185),  // 冬季：蓝色系尘埃
                _ => 0                                                         // 默认值：无效尘埃类型
            };

            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

            for (int i = 1; i <= 27; i++) {
                float factor = 30f / i;
                Vector2 offset = Projectile.oldVelocity * factor;
                Vector2 position = Projectile.oldPosition - offset;

                // 创建两种不同缩放和速度的尘埃效果
                CreateDust(position, dustType, 1.8f, 0.5f);  // 较大缩放，较低速度
                CreateDust(position, dustType, 1.4f, 0.05f); // 较小缩放，非常低速度
            }
        }

        private void CreateDust(Vector2 position, int dustType, float scale, float velocityMultiplier) {
            int dustIndex = Dust.NewDust(position, 8, 8, dustType, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, scale);
            Dust dust = Main.dust[dustIndex];
            dust.noGravity = true;
            dust.velocity *= velocityMultiplier;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int buff = Main.dayTime ? BuffID.Daybreak : ModContent.BuffType<Nightwither>();
            target.AddBuff(buff, 180);
        }

        public override Color? GetAlpha(Color lightColor) => GetColorBySeason(Projectile);

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 595) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class SolsticeClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<SolsticeClaymore>();
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
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = ShootSpanPos + UnitToMouseV.GetNormalVector() * Main.rand.Next(-130, 130);
                Vector2 ver = spanPos.To(InMousePos + UnitToMouseV * 360).UnitVector() * ShootSpeed;
                int type = ModContent.ProjectileType<SolsticeHomeBeam>();
                Projectile.NewProjectile(Source, spanPos, ver, type, Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override void MeleeEffect() {
            int dustType = Main.dayTime ?
            Utils.SelectRandom(Main.rand, new int[] {
                    6,
                    259,
                    158
                }) :
            Utils.SelectRandom(Main.rand, new int[] {
                    173,
                    27,
                    234
                });
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing();
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dayTime) {
                target.AddBuff(BuffID.Daybreak, 300);
            }
            else {
                target.AddBuff(ModContent.BuffType<Nightwither>(), 300);
            }

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (!Main.dayTime) {
                target.AddBuff(ModContent.BuffType<Nightwither>(), 300);
            }
        }
    }
}
