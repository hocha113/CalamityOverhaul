using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>露的演出与伴生弹幕集中处</summary>
    internal static class FuLuFX
    {
        /// <summary>晨青白身份色，定义与演出同源取此</summary>
        internal static readonly Color Accent = new(172, 222, 208);
    }

    /// <summary>
    /// 露·露珠拾取物：命中点抛物弹出，落地静候；归属玩家碰触回 2 生命。
    /// 治疗与销毁只在归属端判定（Kill 自然同步），闪烁与拾取水纹各端本地
    /// </summary>
    internal class FuLuDewDrop : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 360;
        /// <summary>出生宽限：先弹出来再能捡，不在敌人身上原地入袋</summary>
        private const int PickupGraceFrames = 12;
        private const int FadeFrames = 30;

        private int bounces;
        private bool landed;
        private float life;

        /// <summary>确定性相位：闪烁呼吸各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) {
            //露珠要能停在平台上,实战才捡得到;默认碰撞对平台/半砖穿透
            fallThrough = false;
            return true;
        }

        public override void AI() {
            life++;
            //微重力抛物；落定后横向滞干
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 11f);
            if (landed) {
                Projectile.velocity.X *= 0.8f;
            }
            else {
                Projectile.velocity.X *= 0.995f;
                Projectile.rotation += Projectile.velocity.X * 0.04f;
            }

            //偶发晶闪：露珠在光里眨眼（端本地）
            if (!Main.dedServ && Main.rand.NextBool(26)) {
                PRTLoader.NewParticle<PRT_Line>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 3f),
                    -Vector2.UnitY * 0.3f,
                    Color.Lerp(FuLuFX.Accent, Color.White, 0.6f) * 0.6f, 0.3f)?.Configure(false, 8);
            }

            //拾取：只在归属端判定并治疗；Heal 自带治疗数字与联机广播
            if (Main.myPlayer != Projectile.owner || life <= PickupGraceFrames) {
                return;
            }
            Player player = Main.player[Projectile.owner];
            if (player?.active != true || player.dead || player.ghost) {
                return;
            }
            Rectangle box = Projectile.Hitbox;
            box.Inflate(16, 16);
            if (box.Intersects(player.Hitbox)) {
                player.Heal(FuLu.DewHealHp);
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //一次轻弹再落定；落定后贴地滞留
            if (!landed && MathF.Abs(oldVelocity.Y) > 2.2f && bounces < 1) {
                bounces++;
                Projectile.velocity.Y = -oldVelocity.Y * 0.42f;
                Projectile.velocity.X = oldVelocity.X * 0.55f;
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(Projectile.Bottom, -Vector2.UnitY * 1.2f,
                        FuLuFX.Accent, 0.2f)?.Configure(12);
                }
            }
            else {
                landed = true;
                Projectile.velocity = Vector2.Zero;
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (timeLeft > FadeFrames + 8) {
                //被拾取：水纹一圈 + 清音 + 上浮晶点
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                    FuLuFX.Accent * 0.5f, 0.08f)?.Configure(0.08f, 0.62f, 12);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Line>(
                        Projectile.Center + Main.rand.NextVector2Circular(6f, 3f),
                        -Vector2.UnitY * Main.rand.NextFloat(1.2f, 2.4f),
                        Color.Lerp(FuLuFX.Accent, Color.White, 0.65f) * 0.7f,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(false, 12);
                }
                KikasaInk.Play(SoundID.Grab, Projectile.Center, 0.5f, 0.2f, 3);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.3f, 0.75f, 3);
            }
            else {
                //晒干蒸发：一缕薄雾散场
                PRTLoader.NewParticle<PRT_KikasaInkMist>(Projectile.Center, -Vector2.UnitY * 0.4f,
                    FuLuFX.Accent * 0.5f, 0.5f)?.Configure(20);
            }
        }

        /// <summary>晶亮露珠：青白体+白芯高光+一点 A=0 加色玻头，落定微微塌成豆形</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float alpha = MathHelper.Clamp(life / 6f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
            if (alpha <= 0.02f) {
                return false;
            }
            float breath = 1f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + Seed * 2f);
            //空中随速拉长，落定压扁成一颗趴着的露
            Vector2 squash = landed
                ? new Vector2(1.15f, 0.82f)
                : new Vector2(1f - 0.12f * MathF.Abs(Projectile.velocity.Y) * 0.06f,
                    1f + 0.16f * MathF.Abs(Projectile.velocity.Y) * 0.06f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float basePx = 13f * breath;

            //深缘→亮体→白芯，芯偏上读作天光
            Main.EntitySpriteDraw(tex, pos, null, new Color(78, 128, 122) * (alpha * 0.8f), 0f,
                origin, basePx * 1.2f * squash / tex.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null,
                Color.Lerp(FuLuFX.Accent, Color.White, 0.35f) * (alpha * 0.95f), 0f,
                origin, basePx * squash / tex.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(-1.5f, -2.5f), null,
                Color.White * (alpha * 0.7f), 0f, origin,
                basePx * 0.4f * squash / tex.Width, SpriteEffects.None, 0);
            //玻头微光：小面积 A=0 加色
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color add = new(FuLuFX.Accent.R, FuLuFX.Accent.G, FuLuFX.Accent.B, 0);
                Main.EntitySpriteDraw(glow, pos, null, add * (alpha * 0.4f * breath), 0f,
                    glow.Size() * 0.5f, basePx * 2.6f / glow.Width, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
