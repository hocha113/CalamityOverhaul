using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 风暴预警线；ai[0] 锚NPC(-1定点) ai[1] 追玩家(-1不追) ai[2]=PackParams(模式,时长)。<br/>
    /// 模式0 锚定追转（冲刺瞄准线）；模式1 天雷垂直落线；模式2 固定斜线（俯冲航道）
    /// </summary>
    internal class FishronTelegraph : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>末段停追+白闪帧数</summary>
        internal const int LockTime = 14;

        /// <summary>模式+时长打进 ai[2]，随生成同步</summary>
        internal static float PackParams(int mode, int duration) => mode + duration * 4f;

        private int AnchorNpc => (int)Projectile.ai[0];
        private int TrackPlayer => (int)Projectile.ai[1];
        private int Mode => (int)Projectile.ai[2] % 4;
        private int Duration => (int)Projectile.ai[2] / 4;
        private bool Locked => Projectile.timeLeft <= LockTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧套打包时长
            if (Projectile.localAI[0] == 0f) {
                if (Duration > 0) {
                    Projectile.timeLeft = Duration;
                }
                Projectile.localAI[0] = Projectile.timeLeft;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            NPC anchor = AnchorNpc.TryGetNPC(out NPC a) ? a : null;
            if (anchor.Alives()) {
                Projectile.Center = anchor.Center;
            }

            Player player = TrackPlayer.TryGetPlayer(out Player p) ? p : null;
            if (!Locked && player.Alives() && Mode == 0) {
                //硬追踪：与状态侧的冻结时机严格同拍，锁线即是承诺的冲刺线
                Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Lighting.AddLight(Projectile.Center, new Vector3(0.1f, 0.4f, 0.45f));
        }

        public override bool PreDraw(ref Color lightColor) {
            float total = Math.Max(Projectile.localAI[0], 1f);
            float lifeT = 1f - Projectile.timeLeft / total;
            float fadeIn = MathHelper.Clamp(lifeT * 4f, 0f, 1f);
            float lockT = Locked ? 1f - Projectile.timeLeft / (float)LockTime : 0f;

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float pulse = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 13f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            float lineLength = Mode == 1 ? 2600f : 1900f;

            if (!Locked) {
                //追踪期海青呼吸线
                Color warn = new Color(45, 200, 210, 0) * (0.4f * fadeIn * pulse);
                Main.EntitySpriteDraw(tex, drawPos, null, warn, Projectile.rotation,
                    origin, new Vector2(lineLength, 0.4f + 0.22f * pulse), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * 0.65f, Projectile.rotation,
                    origin, new Vector2(lineLength, 1.05f), SpriteEffects.None, 0);
            }
            else {
                //锁定白闪
                float flash = 0.7f + 0.3f * (float)Math.Sin(lockT * MathHelper.Pi * 6f);
                Color core = new Color(220, 250, 250, 0) * (0.85f * flash);
                Color glow = new Color(60, 220, 235, 0) * (0.7f * flash);
                Main.EntitySpriteDraw(tex, drawPos, null, glow, Projectile.rotation,
                    origin, new Vector2(lineLength, 2f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(lineLength, 0.7f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
