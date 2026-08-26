using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 剃刀松灾变「针叶风暴」：跟随玩家。蓄势 30t 松香螺旋光带；
    /// 爆发 150t 环形针叶旋涡自 240px 向心收紧至 90px（×0.4/12t，判定与可见环带同源）；
    /// 余韵 120t 松针毯钉在脚下地面（踩踏 ×0.15）
    /// </summary>
    internal class GsPineStormDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 30;
        public override int MainTicks => 150;
        public override int AftermathTicks => 120;

        protected override bool FollowOwner => true;

        protected override int HitTickRate => 12;

        protected override float TickDamageMul => Phase == 2 ? 0.15f : 0.4f;

        /// <summary>旋涡环带半厚</summary>
        private const float BandHalf = 36f;
        /// <summary>松针毯半宽/半高</summary>
        private const float MatHalfW = 150f;
        private const float MatHalfH = 22f;

        internal static readonly Color PineGreen = new(122, 205, 118);
        internal static readonly Color PineDeep = new(58, 122, 66);

        private static int NeedleType => ContentSamples.ItemsByType[ItemID.Razorpine].shoot;

        /// <summary>爆发段旋涡半径：向心收紧</summary>
        private float StormRadius() {
            float mainT = MathHelper.Clamp(Elapsed - OmenTicks, 0f, MainTicks);
            return MathHelper.Lerp(240f, 90f, VaultUtils.EaseOutQuad(mainT / MainTicks));
        }

        protected override void UpdateAnchor() {
            if (Phase == 2) {
                //余韵：松针毯钉在进入余韵时脚下的地面（tile 各端一致，同帧计算）
                if (Projectile.localAI[2] == 0f) {
                    Projectile.localAI[2] = 1f;
                    Projectile.localAI[0] = Owner.Center.X;
                    Projectile.localAI[1] = FindGroundY(Owner.Center);
                }
                Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1] - MatHalfH);
                return;
            }
            base.UpdateAnchor();
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item66 with { Volume = 0.55f, Pitch = -0.25f }, Projectile.Center);
            }
            //松香螺旋光带
            if (!VaultUtils.isServer && t % 2 == 0) {
                float angle = t * 0.25f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * (28f + t * 2.6f);
                PRTLoader.NewParticle<PRT_Bloomlight>(pos, new Vector2(0f, -0.6f),
                    PineGreen, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(26);
            }
        }

        protected override void MainUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item66 with { Volume = 0.7f, Pitch = 0.15f }, Projectile.Center);
            }
            float radius = StormRadius();
            Lighting.AddLight(Projectile.Center, PineGreen.ToVector3() * 0.35f);
            //环带松香（约 1/2 帧）
            if (!VaultUtils.isServer && t % 2 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Bloomlight>(
                    Projectile.Center + angle.ToRotationVector2() * (radius + Main.rand.NextFloat(-24f, 24f)),
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2.4f,
                    Color.Lerp(PineGreen, PineDeep, Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.55f))?.Configure(22);
            }
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && t % 8 == 0) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-MatHalfW, MatHalfW), -MatHalfH);
                PRTLoader.NewParticle<PRT_Bloomlight>(pos, new Vector2(0f, -0.4f),
                    PineDeep * 0.8f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(24);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase == 1) {
                //旋涡环带判定与可见半径同源
                float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
                return Math.Abs(dist - StormRadius()) < BandHalf + Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            }
            if (Phase == 2) {
                Rectangle mat = new((int)(Projectile.Center.X - MatHalfW), (int)(Projectile.Center.Y - MatHalfH),
                    (int)(MatHalfW * 2f), (int)(MatHalfH * 2f + 12f));
                return mat.Intersects(targetHitbox);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            int needleType = NeedleType;
            Main.instance.LoadProjectile(needleType);
            Texture2D needle = TextureAssets.Projectile[needleType].Value;

            if (Phase == 1) {
                float radius = StormRadius();
                //外圈 26 支顺旋 + 内圈 12 支逆旋暗层，全部 identity 定相
                for (int i = 0; i < 26; i++) {
                    float angle = MathHelper.TwoPi / 26f * i + Timer * 0.09f + Hash01(i) * 0.24f;
                    float r = radius + (Hash01(i + 40) - 0.5f) * 30f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * r - Main.screenPosition;
                    Main.EntitySpriteDraw(needle, pos, null, PineGreen * 0.9f, angle + MathHelper.PiOver2 + 0.45f,
                        needle.Size() * 0.5f, 1f, SpriteEffects.None, 0);
                }
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi / 12f * i - Timer * 0.065f + Hash01(i + 80) * 0.4f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * (radius * 0.62f) - Main.screenPosition;
                    Main.EntitySpriteDraw(needle, pos, null, PineDeep * 0.6f, angle + MathHelper.PiOver2 - 0.45f,
                        needle.Size() * 0.5f, 0.85f, SpriteEffects.None, 0);
                }
            }
            else if (Phase == 2) {
                //松针毯：斜插静态针 14 支
                float fade = MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f);
                for (int i = 0; i < 14; i++) {
                    float x = MathHelper.Lerp(-MatHalfW, MatHalfW, i / 13f) + (Hash01(i) - 0.5f) * 18f;
                    Vector2 pos = Projectile.Center + new Vector2(x, MatHalfH - 6f - Hash01(i + 20) * 10f) - Main.screenPosition;
                    float rot = -MathHelper.PiOver2 + (Hash01(i + 50) - 0.5f) * 0.9f;
                    Main.EntitySpriteDraw(needle, pos, null, PineDeep * (0.85f * fade), rot,
                        needle.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
