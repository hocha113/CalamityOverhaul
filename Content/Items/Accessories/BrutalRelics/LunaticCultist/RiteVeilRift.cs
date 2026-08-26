using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist
{
    /// <summary>
    /// 纱幕步裂幕：瞬移出入口的竖向撕裂+符文剥落；入口额外留一具苍白假身残影，
    /// 沿教徒帷幕挪移语汇。纯演出无判定，owner 生成走原版弹幕同步让旁观者可见<br/>
    /// ai[0]=0 入口(带假身残影)/1 出口 ai[1]=朝向(±1)
    /// </summary>
    internal class RiteVeilRift : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];
        private bool IsOrigin => Projectile.ai[0] < 0.5f;
        private int Facing => Projectile.ai[1] >= 0f ? 1 : -1;

        private const int LifeFrames = 26;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            if ((int)Timer == 1 && !VaultUtils.isServer && CultistMotion.OnScreen(Projectile.Center, 240f)) {
                Color color = IsOrigin ? CultistMotion.PaleClone : CultistMotion.MoonCore;
                CultistMotion.RuneBurst(Projectile.Center, color, IsOrigin ? 10 : 7, 6f);
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.55f,
                    Pitch = IsOrigin ? -0.3f : 0.15f
                }, Projectile.Center);
            }
            //撕裂缘符屑缓落
            if (!VaultUtils.isServer && Timer < LifeFrames - 8 && Main.rand.NextBool(3)) {
                CultistMotion.RuneBurst(Projectile.Center + new Vector2(0f, Main.rand.NextFloat(-30f, 30f)),
                    CultistMotion.PaleClone, 1, 2.5f);
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.MoonCore.ToVector3() * 0.4f
                * (1f - Timer / LifeFrames));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D slit = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (slit == null || glow == null) {
                return false;
            }
            float life = 1f - Timer / (float)LifeFrames;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color moon = CultistMotion.MoonCore with { A = 0 };
            Color pale = CultistMotion.PaleClone with { A = 0 };

            //竖向裂幕：宽度随寿命闭合，白热缝心+冷青缘晕
            float closeT = (float)Math.Pow(life, 0.7);
            Main.EntitySpriteDraw(glow, pos, null, moon * (0.4f * life), 0f, glow.Size() * 0.5f,
                new Vector2(0.8f, 1.7f) * closeT, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(slit, pos, null, moon * (0.9f * life), 0f, slit.Size() * 0.5f,
                new Vector2(0.42f * closeT, 2.5f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(slit, pos, null, Color.White with { A = 0 } * (0.7f * life), 0f,
                slit.Size() * 0.5f, new Vector2(0.16f * closeT, 2.1f), SpriteEffects.None, 0);

            //入口假身残影：帷幕后留下的谎言，缓缓上飘散形
            if (IsOrigin) {
                Main.instance.LoadNPC(NPCID.CultistBossClone);
                Texture2D clone = TextureAssets.Npc[NPCID.CultistBossClone].Value;
                int frameCount = Math.Max(Main.npcFrameCount[NPCID.CultistBossClone], 1);
                Rectangle frame = new(0, 0, clone.Width, clone.Height / frameCount);
                Vector2 ghostPos = pos + new Vector2(0f, -(1f - life) * 14f);
                SpriteEffects flip = Facing == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Main.EntitySpriteDraw(clone, ghostPos, frame, pale * (0.5f * life * life), 0f,
                    frame.Size() * 0.5f, 1f, flip, 0);
            }
            return false;
        }
    }
}
