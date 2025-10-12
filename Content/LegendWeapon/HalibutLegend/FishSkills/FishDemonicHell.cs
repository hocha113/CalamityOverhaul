using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDemonicHell : FishSkill
    {
        public override int UnlockFishID => ItemID.DemonicHellfish;
        public override int DefaultCooldown => 60 * 25; //25s
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (Cooldown > 0) return false;
                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return base.CanUseItem(item, player);
        }
        public override void Use(Item item, Player player) {
            SetCooldown();
            //在玩家前方生成法阵（与鼠标方向）
            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 spawnPos = player.Center + dir * 160f; //距离玩家 160
            Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, dir,
                ModContent.ProjectileType<HellRitualCircle>(), 0, 0f, player.whoAmI, ai0: player.direction);
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.8f, Pitch = -0.7f }, spawnPos);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.4f }, spawnPos);
        }
    }

    /// <summary>
    /// 地狱法阵，充能并发射地狱炎爆
    /// </summary>
    internal class HellRitualCircle : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private ref float ChargeTimer => ref Projectile.ai[0];
        private const int ChargeTime = 60; //1s 充能
        private const int FadeTime = 20; //消散
        private float progress => MathHelper.Clamp(ChargeTimer / ChargeTime, 0f, 1f);
        public override void SetDefaults() {
            Projectile.width = 300;
            Projectile.height = 300;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeTime + FadeTime + 2;
        }
        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }
            //保持面对玩家到光标的朝向（法阵固定在初次位置）
            ChargeTimer++;
            if (ChargeTimer == ChargeTime) {
                FireBlast();
            }
        }

        private void FireBlast() {
            // 发射主爆炸弹幕
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitY);
            int damage = (int)(Owner.GetShootState().WeaponDamage * 6.5f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 6f,
                ModContent.ProjectileType<HellFireBlast>(), damage, 6f, Owner.whoAmI);
            // 环形冲击尘埃
            for (int i = 0; i < 80; i++) {
                float ang = MathHelper.TwoPi * i / 80f;
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 150, new Color(255, 100, 20), Main.rand.NextFloat(1.3f, 2.2f));
                d.noGravity = true; d.fadeIn = 1.2f;
            }
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float baseScale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.05f;
            float glow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
            float p = progress;

            //多层旋转圆环
            DrawRing(sb, pixel, center, 140f, 6f, new Color(255, 90, 30), p, 0.8f);
            DrawRing(sb, pixel, center, 140f * p, 10f, new Color(255, 160, 40) * p, p, 0.4f);
            DrawRing(sb, pixel, center, 120f, 2f, new Color(255, 40, 10), p, 1.2f);

            //动态符纹：正多边形与星形叠加
            DrawPolygon(sb, pixel, center, 8, 110f, 4f, Color.Lerp(Color.OrangeRed, Color.Yellow, glow) * p, rot: Main.GlobalTimeWrappedHourly * 0.8f);
            DrawStar(sb, pixel, center, 7, 40f, 115f, 3f, new Color(255, 120, 50) * (0.6f + 0.4f * glow) * p, Main.GlobalTimeWrappedHourly * -0.6f);

            //径向 runes（使用星纹理）
            Main.instance.LoadItem(ItemID.GuideVoodooDoll);
            Texture2D rune = TextureAssets.Item[ItemID.GuideVoodooDoll].Value; // 暂用人偶纹理抽象 runes
            for (int i = 0; i < 10; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 1.2f + i * MathHelper.TwoPi / 10f;
                Vector2 pos = center + ang.ToRotationVector2() * (60f + (float)Math.Sin(ang * 3f + p * 6f) * 12f) * p;
                float sc = 0.5f + 0.2f * (float)Math.Sin(ang * 2f + p * 10f);
                sb.Draw(rune, pos, null, new Color(255, 150, 70, 0) * p * 0.9f, ang + MathHelper.PiOver2, rune.Size() / 2f, sc * 0.4f, SpriteEffects.None, 0f);
            }

            // 内核脉冲
            for (int i = 0; i < 3; i++) {
                float scale = (0.3f + i * 0.18f + p * 0.4f) * baseScale;
                sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), new Color(255, 120, 40, 0) * (0.4f - i * 0.1f) * p,
                    0f, Vector2.Zero, new Vector2(220f * scale, 220f * scale * 0.6f), SpriteEffects.None, 0f);
            }

            return false;
        }

        private void DrawRing(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float thickness, Color c, float progress, float rotSpeed) {
            int segments = 180;
            float angleStep = MathHelper.TwoPi / segments;
            float rotOffset = Main.GlobalTimeWrappedHourly * rotSpeed;
            for (int i = 0; i < segments; i++) {
                float ang = i * angleStep + rotOffset;
                Vector2 pos = center + ang.ToRotationVector2() * radius;
                float pulse = (float)Math.Sin(ang * 6f + progress * 15f);
                Color col = c * (0.4f + 0.6f * (pulse * 0.5f + 0.5f)) * progress;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col, ang, Vector2.Zero, new Vector2(thickness, 2f), SpriteEffects.None, 0f);
            }
        }
        private void DrawPolygon(SpriteBatch sb, Texture2D pixel, Vector2 center, int sides, float radius, float thickness, Color col, float rot) {
            if (sides < 3) return;
            Vector2 prev = center + (rot).ToRotationVector2() * radius;
            for (int i = 1; i <= sides; i++) {
                float ang = rot + i * MathHelper.TwoPi / sides;
                Vector2 curr = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, pixel, prev, curr, thickness, col);
                prev = curr;
            }
        }
        private void DrawStar(SpriteBatch sb, Texture2D pixel, Vector2 center, int points, float innerR, float outerR, float thickness, Color col, float rot) {
            int total = points * 2;
            Vector2 prev = center + (rot).ToRotationVector2() * outerR;
            for (int i = 1; i <= total; i++) {
                float ang = rot + i * MathHelper.TwoPi / total;
                float r = (i % 2 == 0) ? outerR : innerR;
                Vector2 curr = center + ang.ToRotationVector2() * r;
                DrawLine(sb, pixel, prev, curr, thickness, col * (0.7f + 0.3f * (float)Math.Sin(ang * 4f + progress * 10f)));
                prev = curr;
            }
        }
        private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color col) {
            Vector2 diff = end - start;
            float len = diff.Length();
            float rot = diff.ToRotation();
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 地狱炎爆弹幕：向前飞行后在较小延迟内爆炸，生成大量火焰粒子
    /// </summary>
    internal class HellFireBlast : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int FlyTime = 24;
        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }
        public override void AI() {
            if (Projectile.timeLeft == 90) {
                //初始爆闪
                for (int i = 0; i < 30; i++) {
                    Vector2 v = Main.rand.NextVector2Circular(8f, 8f);
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, v, 150, new Color(255, 120, 40), Main.rand.NextFloat(1.2f, 1.8f));
                    d.noGravity = true;
                }
            }
            float life = 90 - Projectile.timeLeft;
            if (life < FlyTime) {
                Projectile.scale = 0.6f + life / FlyTime * 0.8f;
                Projectile.velocity *= 1.02f;
            }
            else {
                //减速并扩散
                Projectile.velocity *= 0.94f;
                Projectile.scale *= 1.01f;
                if (Projectile.timeLeft == 40) {
                    Explode();
                }
            }
            Lighting.AddLight(Projectile.Center, 1.6f, 0.6f, 0.2f);
        }
        private void Explode() {
            //伤害区域扩大
            Projectile.width = Projectile.height = 220;
            Projectile.position -= new Vector2(110);
            for (int i = 0; i < 140; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float spd = Main.rand.NextFloat(4f, 18f);
                Vector2 vel = ang.ToRotationVector2() * spd;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 80, new Color(255, 140, 50), Main.rand.NextFloat(1.4f, 2.4f));
                d.noGravity = true; d.fadeIn = 1.3f;
                if (i % 6 == 0) {
                    Dust d2 = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel * 0.4f, 200, default, Main.rand.NextFloat(1.8f, 3f));
                    d2.velocity.Y -= 1f; d2.noGravity = true; d2.color = new Color(60, 20, 10);
                }
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
        }
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.5f + 0.5f;
            float scale = Projectile.scale;
            for (int i = 0; i < 4; i++) {
                float rot = MathHelper.TwoPi * i / 4f + Main.GlobalTimeWrappedHourly * 0.4f;
                Vector2 off = rot.ToRotationVector2() * 18f * scale;
                sb.Draw(pixel, center + off, new Rectangle(0, 0, 1, 1), new Color(255, 100, 30, 0) * 0.5f, 0f, Vector2.Zero, new Vector2(160f * scale, 160f * scale), SpriteEffects.None, 0f);
            }
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), new Color(255, 160, 60, 0) * 0.8f, 0f, Vector2.Zero, new Vector2(120f * scale, 120f * scale), SpriteEffects.None, 0f);
            return false;
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //附加灼烧（原版地狱火）
            target.AddBuff(BuffID.OnFire3, 300);
        }
    }
}
