using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles
{
    /// <summary>钢环绞缠警告圈，无伤纯视觉；ai[0]头whoAmI；定在锁死的环心，
    /// 锁环期画逃逸边界+收拢外环，绞缠期画收紧电光环；头离开投技状态即淡出</summary>
    internal class DestroyerCoilRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>失效后淡出帧数</summary>
        private const int FadeTime = 14;

        private ref float LocalTimer => ref Projectile.localAI[0];
        private ref float FadeTimer => ref Projectile.localAI[1];

        private NPC Head => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>头是否仍处于投技两态(各端从同步的ai[2]自行判断)</summary>
        private bool HostValid {
            get {
                NPC head = Head;
                if (!head.Alives() || head.type != NPCID.TheDestroyer) {
                    return false;
                }
                int state = (int)head.ai[2];
                return state == (int)DestroyerStateIndex.CoilLock
                    || state == (int)DestroyerStateIndex.CoilCrush;
            }
        }

        public override void AI() {
            LocalTimer++;

            if (HostValid) {
                //有效期各端本地续命，避免服务端直改timeLeft不入包
                Projectile.timeLeft = 90;
                FadeTimer = 0f;
                //环心钉死在头ai[0/1](随NPC同步)
                NPC head = Head;
                Projectile.Center = new Vector2(head.ai[0], head.ai[1]);
            }
            else if (LocalTimer > 20f) {
                //出生宽限后宿主仍不在投技态才淡出自毁——
                //防客户端先收到本弹生成包、后收到头的ai[2]状态包时误自杀
                FadeTimer++;
                if (FadeTimer >= FadeTime) {
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.16f, 0.08f));
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC head = Head;
            float fade = 1f - MathHelper.Clamp(FadeTimer / FadeTime, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }

            bool crushMode = head.Alives() && (int)head.ai[2] == (int)DestroyerStateIndex.CoilCrush;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            //活半径直接取头到环心的实距(已同步)，天然与收缩动作对齐
            float liveRadius = head.Alives()
                ? MathHelper.Clamp(head.Distance(Projectile.Center), 120f, 1400f)
                : DestroyerCoilLockState.GrabRadius;

            if (!crushMode) {
                DrawWarningMode(drawPos, ring, glow, liveRadius, fade);
            }
            else {
                DrawCrushMode(drawPos, ring, glow, liveRadius, fade);
            }
            return false;
        }

        /// <summary>锁环警告：逃逸边界圈+沿头半径收拢的外环+环上巡回光点</summary>
        private void DrawWarningMode(Vector2 drawPos, Texture2D ring, Texture2D glow, float liveRadius, float fade) {
            float grabRadius = DestroyerCoilLockState.GrabRadius;
            //锁环后期告警提速
            float urgency = MathHelper.Clamp(LocalTimer / DestroyerCoilLockState.LockDuration, 0f, 1f);
            float pulse = 0.62f + 0.38f * (float)Math.Sin(LocalTimer * MathHelper.Lerp(0.18f, 0.42f, urgency));

            //逃逸边界：双层旋转呼吸圈
            Color warn = new Color(255, 58, 36, 0) * (0.62f * pulse * fade);
            float boundaryScale = grabRadius * 2f / ring.Width;
            Main.EntitySpriteDraw(ring, drawPos, null, warn, LocalTimer * 0.02f,
                ring.Size() / 2f, boundaryScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(ring, drawPos, null, warn * 0.6f, -LocalTimer * 0.014f,
                ring.Size() / 2f, boundaryScale * 1.06f, SpriteEffects.None, 0);

            //收拢外环：贴着头的实际螺旋半径，读作"钢环正在合拢"
            Color closing = new Color(255, 120, 50, 0) * (0.4f * fade);
            float closingScale = liveRadius * 2f / ring.Width;
            Main.EntitySpriteDraw(ring, drawPos, null, closing, LocalTimer * -0.03f,
                ring.Size() / 2f, closingScale, SpriteEffects.None, 0);

            //边界巡回光点
            for (int i = 0; i < 12; i++) {
                float a = MathHelper.TwoPi / 12f * i + LocalTimer * 0.05f;
                Vector2 dotPos = drawPos + a.ToRotationVector2() * grabRadius;
                Main.EntitySpriteDraw(glow, dotPos, null,
                    new Color(255, 90, 50, 0) * (0.7f * pulse * fade), 0f,
                    glow.Size() / 2f, 0.42f + 0.16f * pulse, SpriteEffects.None, 0);
            }

            //环心危险度呼吸光
            Main.EntitySpriteDraw(glow, drawPos, null,
                new Color(255, 46, 30, 0) * (0.35f * pulse * urgency * fade), 0f,
                glow.Size() / 2f, 2.6f, SpriteEffects.None, 0);
        }

        /// <summary>绞缠期：贴环体的电光收紧圈+环心闷红光</summary>
        private void DrawCrushMode(Vector2 drawPos, Texture2D ring, Texture2D glow, float liveRadius, float fade) {
            float pulse = 0.7f + 0.3f * (float)Math.Sin(LocalTimer * 0.3f);

            Color hot = new Color(255, 130, 55, 0) * (0.5f * pulse * fade);
            float scale = liveRadius * 2f / ring.Width;
            Main.EntitySpriteDraw(ring, drawPos, null, hot, LocalTimer * 0.045f,
                ring.Size() / 2f, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(ring, drawPos, null, hot * 0.55f, -LocalTimer * 0.03f,
                ring.Size() / 2f, scale * 0.94f, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(glow, drawPos, null,
                new Color(255, 60, 34, 0) * (0.3f * pulse * fade), 0f,
                glow.Size() / 2f, 1.8f, SpriteEffects.None, 0);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
