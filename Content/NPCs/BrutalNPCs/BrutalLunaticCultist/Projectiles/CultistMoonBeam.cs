using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 月明辐条死光:自月亮盘心射出的旋扫光束,恒定角速度=只要早决断永远跑得掉<br/>
    /// ai[0]=起始角 ai[1]=扫向(±1) ai[2]=扫描帧数<br/>
    /// 时间轴:50 帧细线预告(预告即承诺,起始角锁死)→旋扫→20 帧收尾<br/>
    /// 命中挂月咬(禁吸血),月总语汇
    /// </summary>
    internal class CultistMoonBeam : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];
        private float StartAngle => Projectile.ai[0];
        private float SweepDir => Projectile.ai[1] >= 0f ? 1f : -1f;
        private int SweepFrames => (int)MathHelper.Max(Projectile.ai[2], 60f);

        private const int WarnFrames = 50;
        private const int FadeFrames = 20;
        /// <summary>恒定角速度(弧/帧),约 0.7°/帧</summary>
        private const float SweepRate = 0.0122f;
        private const float BeamStart = 480f;
        private const float BeamLength = 2100f;
        private const float BeamHalfWidth = 26f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>当前束角:预告期锁死在起始角,扫描期恒速旋转(各端确定性推导)</summary>
        private float BeamAngle {
            get {
                float sweep = MathHelper.Clamp(Timer - WarnFrames, 0f, SweepFrames);
                return StartAngle + sweep * SweepRate * SweepDir;
            }
        }

        private bool Firing => Timer >= WarnFrames && Timer < WarnFrames + SweepFrames;

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 60;

            if (Timer == WarnFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.1f, Pitch = -0.5f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.4f }, Projectile.Center);
                CultistMotion.Shake(Projectile.Center, 6f, 12);
            }
            if (Firing) {
                Vector2 dir = BeamAngle.ToRotationVector2();
                Lighting.AddLight(Projectile.Center + dir * (BeamStart + 400f),
                    CultistMotion.MoonCore.ToVector3() * 1.1f);
                Lighting.AddLight(Projectile.Center + dir * (BeamStart + 1100f),
                    CultistMotion.MoonCore.ToVector3() * 0.9f);
            }
            if (Timer >= WarnFrames + SweepFrames + FadeFrames) {
                Projectile.Kill();
            }
        }

        public override bool CanHitPlayer(Player target) => Firing;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Firing) {
                return false;
            }
            Vector2 dir = BeamAngle.ToRotationVector2();
            Vector2 start = Projectile.Center + dir * BeamStart;
            Vector2 end = Projectile.Center + dir * (BeamStart + BeamLength);
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamHalfWidth * 2f, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //月咬:禁吸血,月总的招牌规则
            target.AddBuff(BuffID.MoonLeech, 300);
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            //压玩家之上:死光必须永远读得见
            overPlayers.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 dir = BeamAngle.ToRotationVector2();
            Vector2 start = Projectile.Center + dir * BeamStart - Main.screenPosition;
            float rot = BeamAngle;
            Color core = CultistMotion.MoonCore with { A = 0 };

            if (Timer < WarnFrames) {
                //细线预告:亮度缓升,读线时间
                float warnT = Timer / WarnFrames;
                Main.spriteBatch.Draw(glow, start, null, core * (0.20f + 0.25f * warnT), rot,
                    new Vector2(0f, glow.Height * 0.5f),
                    new Vector2(BeamLength / glow.Width, 7f / glow.Height), SpriteEffects.None, 0f);
                return false;
            }

            float fade = Timer < WarnFrames + SweepFrames ? 1f
                : 1f - (Timer - WarnFrames - SweepFrames) / FadeFrames;
            if (fade <= 0.01f) {
                return false;
            }
            //束体:宽晕+主体+白热芯(光,全加色),端头接月
            float flick = 0.92f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 19f);
            Main.spriteBatch.Draw(glow, start, null, core * (0.45f * fade), rot,
                new Vector2(0f, glow.Height * 0.5f),
                new Vector2(BeamLength / glow.Width, BeamHalfWidth * 4.4f / glow.Height), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, start, null, core * (0.85f * fade * flick), rot,
                new Vector2(0f, glow.Height * 0.5f),
                new Vector2(BeamLength / glow.Width, BeamHalfWidth * 2.1f / glow.Height), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, start, null, Color.White with { A = 0 } * (0.9f * fade * flick), rot,
                new Vector2(0f, glow.Height * 0.5f),
                new Vector2(BeamLength / glow.Width, BeamHalfWidth * 0.8f / glow.Height), SpriteEffects.None, 0f);
            //出瞳端头光斑
            Main.spriteBatch.Draw(glow, start, null, core * (0.9f * fade), 0f, glow.Size() * 0.5f,
                1.8f * fade + 0.4f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
