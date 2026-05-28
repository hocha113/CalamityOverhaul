using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 沙之下：抛射一对沙之涌动，落地后从地表喷涌出垂直沙刺，掀飞接触的敌人
    /// </summary>
    internal class UnderTheSand : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "UnderTheSand";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 11;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.mana = 6;
            Item.knockBack = 2.5f;
            Item.shoot = ModContent.ProjectileType<UnderTheSandSurge>();
            Item.shootSpeed = 8;
            Item.UseSound = SoundID.Item20;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 50, 15);
            Item.SetHeldProj<UnderTheSandHeld>();
        }
    }

    internal class UnderTheSandHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "UnderTheSand";
        public override int TargetID => ModContent.ItemType<UnderTheSand>();
        public override void SetMagicProperty() {
            HandFireDistanceX = 18;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 8;
            GunPressure = 0.18f;
            ControlForce = 0.05f;
            RecoilRetroForceMagnitude = 10;
            RecoilOffsetRecoverValue = 0.6f;
            EnableRecoilRetroEffect = true;
            FiringDefaultSound = false;
            CanCreateSpawnGunDust = CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void PreInOwner() {
            if (fireIndex > 0) {
                fireIndex--;
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);

            //沿射击方向以微小扇形喷出两枚沙之涌动
            int surgeType = ModContent.ProjectileType<UnderTheSandSurge>();
            const int surgeCount = 2;
            for (int i = 0; i < surgeCount; i++) {
                float spread = (i - (surgeCount - 1) / 2f) * 0.16f;
                Vector2 vel = ShootVelocity.RotatedBy(spread) * Main.rand.NextFloat(0.95f, 1.1f);
                Projectile.NewProjectile(Source, ShootPos, vel
                    , surgeType, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }

            //枪口卷起一道环形沙环作为开火反馈
            const int dustRing = 14;
            for (int i = 0; i < dustRing; i++) {
                Vector2 vr = (MathHelper.TwoPi / dustRing * i).ToRotationVector2() * Main.rand.NextFloat(3f, 5f);
                int dustId = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                int d = Dust.NewDust(ShootPos, 1, 1, dustId, vr.X, vr.Y, 0, default, 0.95f);
                Main.dust[d].noGravity = true;
            }
        }

        public override void SetShootAttribute() => fireIndex = 22;

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Color color = Color.Goldenrod;
            color.A = 0;
            float slp = 1 + 0.012f * fireIndex;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, color * 0.4f
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale * slp
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            base.GunDraw(drawPos, ref lightColor);
        }
    }

    /// <summary>
    /// 沙之涌动：飞行段为带拖尾的沙弹，撞地后立即转入“沙刺爆发”阶段，向上耸起一根沙刺造成持续伤害
    /// </summary>
    internal class UnderTheSandSurge : ModProjectile
    {
        //贴上原 SandThorn 纹理，让沙弹/沙刺都有合适的视觉
        public override string Texture => CWRConstant.Projectile_Magic + "SandThorn";

        private const int FlightLifetime = 240;
        private const int EruptDuration = 32;
        private const int EruptRiseFrames = 8;
        private const int EruptHoldFrames = 18;
        private const float SpikeHalfWidth = 12f;
        private const float SpikeMaxHeight = 110f;

        //0 = 飞行，1 = 爆发
        private ref float Phase => ref Projectile.ai[0];
        //爆发阶段计时
        private ref float EruptTimer => ref Projectile.ai[1];
        //视觉相位
        private ref float SwirlT => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.timeLeft = FlightLifetime;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            SwirlT += 0.2f;

            if (Phase == 0f) {
                //飞行阶段：轻重力 + 速度上限
                Projectile.velocity.Y += 0.32f;
                if (Projectile.velocity.Y > 13f) {
                    Projectile.velocity.Y = 13f;
                }
                Projectile.rotation = Projectile.velocity.ToRotation();

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    int dustId = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                    int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustId, 0f, 0f, 100, default, 1.0f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity *= 0.4f;
                }
            }
            else {
                //爆发阶段：保持原地，时序推进
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = 0f;
                EruptTimer++;

                if (!Main.dedServ) {
                    SpawnEruptionDust();
                    Lighting.AddLight(Projectile.Bottom - new Vector2(0, CurrentSpikeHeight() * 0.5f)
                        , new Color(225, 190, 110).ToVector3() * 0.65f);
                }

                if (EruptTimer >= EruptDuration) {
                    Projectile.Kill();
                }
            }
        }

        //当前沙刺的可视/受击高度，含上升、保持、淡出三段
        private float CurrentSpikeHeight() {
            if (Phase != 1f) {
                return 0f;
            }
            if (EruptTimer < EruptRiseFrames) {
                float t = EruptTimer / (float)EruptRiseFrames;
                //缓出曲线，让沙刺有冲出地表的爆发感
                t = 1f - (1f - t) * (1f - t);
                return SpikeMaxHeight * t;
            }
            if (EruptTimer < EruptRiseFrames + EruptHoldFrames) {
                return SpikeMaxHeight;
            }
            float fadeT = MathHelper.Clamp((EruptDuration - EruptTimer) / (float)(EruptDuration - EruptRiseFrames - EruptHoldFrames), 0f, 1f);
            return SpikeMaxHeight * fadeT;
        }

        private void SpawnEruptionDust() {
            float height = CurrentSpikeHeight();
            if (height <= 0f) {
                return;
            }
            int spawns = EruptTimer < EruptRiseFrames ? 4 : 2;
            for (int i = 0; i < spawns; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                float yT = Main.rand.NextFloat(0f, 1f);
                Vector2 spawnPos = Projectile.Bottom - new Vector2(Main.rand.NextFloat(-SpikeHalfWidth, SpikeHalfWidth), yT * height);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-3.5f, -0.5f));
                int dustId = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                int d = Dust.NewDust(spawnPos, 1, 1, dustId, vel.X, vel.Y, 80, default, 1.1f);
                Main.dust[d].noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Phase == 0f && oldVelocity.Y > 0f) {
                //撞地转入爆发阶段
                Phase = 1f;
                EruptTimer = 0;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = EruptDuration + 4;
                //沙刺在 X 方向不再受 tile collide 限制，避免被一块凸起的地形错位推开
                Projectile.tileCollide = false;
                Projectile.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.35f }, Projectile.Center);
                //着地扬尘
                for (int i = 0; i < 18; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -0.5f));
                    int dustId = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                    int d = Dust.NewDust(Projectile.Bottom, 1, 1, dustId, vel.X, vel.Y, 100, default, 1.25f);
                    Main.dust[d].noGravity = true;
                }
                return false;
            }
            //飞行中蹭到墙壁：轻度反弹再继续坠落
            if (Phase == 0f && oldVelocity.X != Projectile.velocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.45f;
                return false;
            }
            return false;
        }

        public override bool? CanHitNPC(NPC target) {
            if (Phase == 0f) {
                return null;
            }
            //爆发阶段以一个垂直矩形作为命中判定，宽度恒定，高度随沙刺成长
            float height = CurrentSpikeHeight();
            if (height <= 0f) {
                return false;
            }
            Vector2 bottom = Projectile.Bottom;
            Rectangle spikeRect = new Rectangle((int)(bottom.X - SpikeHalfWidth)
                , (int)(bottom.Y - height), (int)(SpikeHalfWidth * 2f), (int)height);
            return spikeRect.Intersects(target.Hitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中后将敌人向上掀飞，呼应"从沙下掀起"
            if (target.knockBackResist > 0f) {
                target.velocity.Y -= 4.2f * target.knockBackResist;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rect = texture.GetRectangle();
            Vector2 origin = rect.Size() / 2f;

            if (Phase == 0f) {
                DrawFlight(texture, rect, origin);
            }
            else {
                DrawSpike(texture, rect, origin);
            }

            return false;
        }

        private void DrawFlight(Texture2D texture, Rectangle rect, Vector2 origin) {
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2f);
                Main.EntitySpriteDraw(texture, drawPos, rect, color
                    , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rect
                , Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        }

        private void DrawSpike(Texture2D texture, Rectangle rect, Vector2 origin) {
            float height = CurrentSpikeHeight();
            if (height <= 0f) {
                return;
            }
            //总淡出，仅在尾段衰减
            float fadeOut = MathHelper.Clamp((EruptDuration - EruptTimer) / 10f, 0f, 1f);
            //刚冲出地表的几帧加亮，增强张力
            float impactFlash = MathHelper.Clamp(1f - EruptTimer / 6f, 0f, 1f);

            Color baseColor = new Color(225, 195, 110);
            Color tipColor = new Color(255, 235, 175);

            //自下向上堆叠多片 SandThorn，模拟沙刺从地里耸出
            const int slices = 6;
            Vector2 bottom = Projectile.Bottom - Main.screenPosition;
            for (int i = 0; i < slices; i++) {
                float t01 = i / (float)(slices - 1);
                float yOff = -t01 * height;
                //靠下贴地的层次更宽更扁，越靠尖端越细
                float wid = MathHelper.Lerp(1.05f, 0.55f, t01);
                float hei = MathHelper.Lerp(0.95f, 0.7f, t01);
                float wobble = MathF.Sin(SwirlT + i) * 0.06f;
                Color c = Color.Lerp(baseColor, tipColor, t01) * fadeOut;
                Vector2 drawPos = bottom + new Vector2(MathF.Sin(SwirlT * 0.8f + i * 1.3f) * 1.5f, yOff);
                //SandThorn 自身是横向的，旋转 -90° 让尖端指向上方
                Main.EntitySpriteDraw(texture, drawPos, rect, c
                    , -MathHelper.PiOver2 + wobble, origin, new Vector2(wid, hei), SpriteEffects.None, 0);
            }

            //着地刹那的金光闪烁
            if (impactFlash > 0f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Color flash = new Color(255, 220, 140, 0) * impactFlash * 0.8f;
                    Main.spriteBatch.Draw(glow, bottom, null, flash, 0f, glow.Size() / 2f
                        , SpikeMaxHeight / 32f * 0.45f * (1f + impactFlash * 0.6f), SpriteEffects.None, 0f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //结束时再补一波细沙
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                int dustId = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                int d = Dust.NewDust(Projectile.Center, 1, 1, dustId, vel.X, vel.Y, 100, default, 1.05f);
                Main.dust[d].noGravity = true;
            }
        }
    }
}
