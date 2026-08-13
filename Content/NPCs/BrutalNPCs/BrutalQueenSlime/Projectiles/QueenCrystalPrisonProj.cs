using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>
    /// 棱晶囚茧(投技载体)：包裹被抓玩家的六面宝石晶壳，随囚舞轨道公转，终结拍被贯碎。
    /// 自身零伤害，仅演出与世界空间锚点；位置由舞轨公式逐帧驱动(各端一致)。
    /// ai[0]=皇后whoAmI ai[1]=舞心X ai[2]=舞心Y(随出生包同步)。
    /// </summary>
    internal class QueenCrystalPrisonProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float BodyRadius = 74f;

        private int QueenIndex => (int)Projectile.ai[0];
        /// <summary>出生点缓存(成茧过渡起点)，各端从出生包位置各自缓存</summary>
        private ref float SpawnX => ref Projectile.localAI[1];
        private ref float SpawnY => ref Projectile.localAI[2];
        /// <summary>属主失联宽限计数(等服务端杀令)</summary>
        private ref float OrphanTimer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
        }

        /// <summary>属主皇后，无效返回null</summary>
        private NPC ResolveQueen() {
            if (QueenIndex < 0 || QueenIndex >= Main.maxNPCs) {
                return null;
            }
            NPC queen = Main.npc[QueenIndex];
            if (!queen.active || queen.type != NPCID.QueenSlimeBoss) {
                return null;
            }
            return queen;
        }

        public override void AI() {
            //首帧缓存出生位置(即压点)，各端在本地首见时各响一次成茧音
            if (SpawnX == 0f && SpawnY == 0f) {
                SpawnX = Projectile.Center.X;
                SpawnY = Projectile.Center.Y;
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = -0.15f }, Projectile.Center);
            }

            NPC queen = ResolveQueen();
            int victim = queen != null ? QueenCrystalPrisonWaltzState.VictimIndex(queen) : -1;
            bool grabValid = queen != null && victim >= 0;

            if (!grabValid) {
                //失联宽限：客户端等服务端杀令，权威端直接消亡
                OrphanTimer++;
                if (!VaultUtils.isClient || OrphanTimer > 20f) {
                    Projectile.Kill();
                }
                Projectile.velocity = Vector2.Zero;
                return;
            }
            OrphanTimer = 0f;

            //轨道驱动：成茧期从压点滑入舞轨，之后贴轨公转
            int t = Timer();
            Vector2 socket = QueenCrystalPrisonWaltzState.PrisonSocket(QueenCrystalPrisonWaltzState.WaltzCenter(Projectile), t);
            if (t < QueenCrystalPrisonWaltzState.CocoonTime) {
                float p = QueenMotion.SnapOut(t / (float)QueenCrystalPrisonWaltzState.CocoonTime, 4);
                Projectile.Center = Vector2.Lerp(new Vector2(SpawnX, SpawnY), socket, p);
            }
            else {
                Projectile.Center = socket;
            }
            Projectile.velocity = Vector2.Zero;

            //茧内溢彩
            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(QueenCrystalPrisonWaltzState.RoyalHue).ToVector3() * 0.8f);
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_QueenGelDrop>(Projectile.Bottom + Main.rand.NextVector2Circular(24f, 8f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), 1.5f), QueenMotion.RoyalPink * 0.8f, Main.rand.NextFloat(0.5f, 0.8f));
            }
        }

        /// <summary>从属主皇后读囚舞时钟</summary>
        private int Timer() {
            NPC queen = ResolveQueen();
            return queen != null ? QueenCrystalPrisonWaltzState.GrabTick(queen) : 0;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //终结拍附近死亡=贯茧主爆，其余(异常释放)中等碎裂
            bool finisher = false;
            NPC queen = ResolveQueen();
            if (queen != null && (int)queen.ai[2] == (int)QueenSlimeStateIndex.CrystalPrisonWaltz
                && QueenCrystalPrisonWaltzState.GrabTick(queen) >= QueenCrystalPrisonWaltzState.FinisherTick - 4) {
                finisher = true;
            }

            float scale = finisher ? 2.2f : 1.2f;
            QueenMotion.CrystalShatterBurst(Projectile.Center, scale, QueenCrystalPrisonWaltzState.RoyalHue);
            QueenMotion.GelSplashBurst(Projectile.Center, scale * 0.7f, finisher ? 14 : 7);
            if (finisher) {
                //碎晶花瓣三环
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        QueenMotion.PrismHue(QueenCrystalPrisonWaltzState.RoyalHue + i * 0.12f) * 0.9f, 0.35f + i * 0.15f)?
                        .Configure(new Vector2(1f, 1f), 0f, 1.8f + i * 0.5f, 22);
                }
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1.1f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = -0.6f }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>棱晶茧本体(六面宝石quad，罩在玩家绘制层之上呈半透明封晶感)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.QueenPrismCrystal?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            NPC queen = ResolveQueen();
            if (effect == null || noise == null || queen == null) {
                return;
            }

            int t = Timer();
            float grow = MathHelper.Clamp(t / (float)QueenCrystalPrisonWaltzState.CocoonTime, 0.05f, 1f);
            float charge = 0f;
            if (t > QueenCrystalPrisonWaltzState.FinisherChargeTick) {
                charge = MathHelper.Clamp((t - QueenCrystalPrisonWaltzState.FinisherChargeTick)
                    / (float)(QueenCrystalPrisonWaltzState.FinisherTick - QueenCrystalPrisonWaltzState.FinisherChargeTick), 0f, 1f);
            }
            //踢击受击白闪(短脉冲)
            float hitFlash = 0f;
            foreach (int k in QueenCrystalPrisonWaltzState.KickTicks) {
                if (t >= k && t <= k + 6) {
                    hitFlash = Math.Max(hitFlash, 1f - (t - k) / 6f);
                }
            }

            float half = BodyRadius * 2.1f;
            Vector2 c = Projectile.Center;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(c.X - half, c.Y - half, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(c.X + half, c.Y - half, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(c.X - half, c.Y + half, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(c.X + half, c.Y + half, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(0f);
            effect.Parameters["uGrow"]?.SetValue(grow);
            //踢击瞬间借碎裂裂纹当受击白线
            effect.Parameters["uShatter"]?.SetValue(hitFlash * 0.3f);
            effect.Parameters["uCharge"]?.SetValue(charge + hitFlash * 0.5f);
            effect.Parameters["uHueSeed"]?.SetValue(QueenCrystalPrisonWaltzState.RoyalHue);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.211f % 1f);
            //噪声显式绑 s1(shader 内 register(s1))
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>茧体辉光与终结蓄能(真加色批，染色必须带A)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            NPC queen = ResolveQueen();
            if (queen == null) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(QueenCrystalPrisonWaltzState.RoyalHue);
            int t = Timer();

            float breath = 0.85f + 0.15f * (float)Math.Sin(t * 0.2f);
            spriteBatch.Draw(glow, drawPos, null, hue * (0.45f * breath), 0f, glow.Size() / 2f, 1.7f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, Color.White * 0.5f, t * 0.03f, star.Size() / 2f, 0.5f, SpriteEffects.None, 0f);

            //终结蓄能：白热渐起
            if (t > QueenCrystalPrisonWaltzState.FinisherChargeTick) {
                float p = MathHelper.Clamp((t - QueenCrystalPrisonWaltzState.FinisherChargeTick)
                    / (float)(QueenCrystalPrisonWaltzState.FinisherTick - QueenCrystalPrisonWaltzState.FinisherChargeTick), 0f, 1f);
                float flick = 1f + 0.15f * (float)Math.Sin(t * 1.1f);
                spriteBatch.Draw(glow, drawPos, null, Color.White * (0.6f * p * flick), 0f, glow.Size() / 2f, 1.2f + p, SpriteEffects.None, 0f);
            }
        }
    }
}
