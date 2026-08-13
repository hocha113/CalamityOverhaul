using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 处刑触须抽打：自头颅口须甩向持握点的一记鞭击。
    /// 原地判定弹（生成即定点），卷缩预备→甩直炸响→余韵消散。
    /// ai[0]=头颅 whoAmI，ai[1]=弯折侧（±1，视觉用）
    /// </summary>
    internal class MLordGrabLashProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>卷缩预备帧</summary>
        internal const int WindupTime = 8;
        /// <summary>甩直命中窗结束帧</summary>
        internal const int SnapEnd = 14;
        /// <summary>总寿命</summary>
        internal const int TotalLife = 28;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Head => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private float Side => Projectile.ai[1] >= 0f ? 1f : -1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 92;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            if ((int)Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.6f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            }
            if ((int)Timer == WindupTime && !VaultUtils.isServer) {
                //甩直炸响：鞭梢星尘爆
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.25f, MaxInstances = 4 }, Projectile.Center);
                MLordScreenFX.Punch(Projectile.Center, 3.5f, 8);
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel,
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.5f, 0.95f))?.Configure(false, Main.rand.Next(14, 22));
                }
            }

            Timer++;
            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.4f);
        }

        //命中窗与甩直动作精确对齐
        public override bool? CanDamage() => Timer >= WindupTime && Timer <= SnapEnd ? null : false;

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (streak == null || glow == null) {
                return;
            }
            NPC head = Head;
            Vector2 mouth = head.Alives()
                ? head.Center + new Vector2(0f, 214f)
                : Projectile.Center + new Vector2(0f, -260f);

            float t = Timer;
            //甩鞭进度：预备期慢伸，甩直一瞬绷紧（poly 陡出）
            float extend = t < WindupTime
                ? 0.35f + 0.4f * (t / WindupTime)
                : 1f;
            //弯折量：预备期卷起，甩直后归零绷直，余韵回弹
            float bend;
            if (t < WindupTime) {
                bend = MathHelper.Lerp(90f, 130f, t / WindupTime);
            }
            else if (t <= SnapEnd) {
                bend = 8f;
            }
            else {
                bend = 30f * (float)Math.Sin((t - SnapEnd) * 0.6f) * (1f - (t - SnapEnd) / (TotalLife - SnapEnd));
            }
            float fade = t > SnapEnd ? MathHelper.Clamp(1f - (t - SnapEnd) / (float)(TotalLife - SnapEnd), 0f, 1f) : 1f;

            //二次贝塞尔取样：口须根→控制点（侧向外凸）→鞭梢
            Vector2 tip = Vector2.Lerp(mouth, Projectile.Center, extend);
            Vector2 mid = Vector2.Lerp(mouth, tip, 0.5f)
                + (tip - mouth).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2) * bend * Side;

            const int segments = 14;
            Vector2 prev = mouth;
            for (int i = 1; i <= segments; i++) {
                float p = i / (float)segments;
                Vector2 pos = Vector2.Lerp(Vector2.Lerp(mouth, mid, p), Vector2.Lerp(mid, tip, p), p);
                Vector2 delta = pos - prev;
                float rot = delta.ToRotation();
                float len = delta.Length();
                //根粗梢细的鞭身，三层叠色
                float thick = MathHelper.Lerp(22f, 7f, p);
                Vector2 anchor = new(0f, streak.Height * 0.5f);
                Main.EntitySpriteDraw(streak, prev - Main.screenPosition, null,
                    MLordDirector.DeepViolet with { A = 0 } * (0.5f * fade), rot, anchor,
                    new Vector2(len * 1.15f / streak.Width, thick / streak.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(streak, prev - Main.screenPosition, null,
                    MLordDirector.Phantasmal with { A = 0 } * (0.7f * fade), rot, anchor,
                    new Vector2(len * 1.1f / streak.Width, thick * 0.5f / streak.Height), SpriteEffects.None, 0);
                prev = pos;
            }

            //鞭梢光核：命中窗满亮，余韵衰减
            float tipHeat = t >= WindupTime ? 1f : 0.35f;
            Main.EntitySpriteDraw(glow, tip - Main.screenPosition, null,
                MLordDirector.MoonWhite with { A = 0 } * (0.7f * tipHeat * fade), 0f,
                glow.Size() / 2f, 0.9f * tipHeat, SpriteEffects.None, 0);
        }
    }
}
