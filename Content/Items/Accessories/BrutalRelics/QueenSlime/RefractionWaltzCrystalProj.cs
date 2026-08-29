using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenSlime
{
    /// <summary>
    /// 折射水晶：凝胶滴凝结成晶的悬浮节点。
    /// ai[0]=布晶序号(FIFO) ai[1]=色相种子 ai[2]=0活跃/2让位快碎(owner写+netUpdate)。
    /// 敌人撞碎时向来敌迸出碎晶；光束端点取 <see cref="Projectile.Center"/>(静止)，
    /// 呼吸浮动只进绘制(<see cref="VisualBob"/> 各端同式自算)
    /// </summary>
    internal class RefractionWaltzCrystalProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override Terraria.Localization.LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "折射水晶");

        /// <summary>撞碎它要挨的一下(基伤，挂通用加成)</summary>
        internal const int ContactDamage = 70;
        /// <summary>凝胶→晶体物化帧数</summary>
        internal const int MaterializeFrames = 26;
        /// <summary>驻场寿命(帧)</summary>
        internal const int LifeFrames = 600;
        /// <summary>寿终收场帧数</summary>
        private const int FadeOutFrames = 20;
        /// <summary>让位快碎帧数</summary>
        private const int EvictFrames = 8;
        /// <summary>晶壳绘制半尺寸(px)</summary>
        private const float ShellHalfSize = 40f;

        private float HueSeed => Projectile.ai[1];
        /// <summary>0活跃 2让位快碎</summary>
        private ref float EvictState => ref Projectile.ai[2];

        private int age;
        private int evictTimer;
        private int linkCountCache;
        private int linkCountTimer;
        /// <summary>满编时的装饰假束搭档槽位(-1无)，纯视觉</summary>
        private int fakePartnerIdx = -1;
        /// <summary>被敌人撞碎标记(owner 端命中钩写)，碎晶朝这个方向迸</summary>
        private bool shatteredByEnemy;
        private Vector2 shatterDir = Vector2.UnitY;
        //quad 顶点成员缓存，免每帧分配
        private readonly VertexPositionColorTexture[] shellVerts = new VertexPositionColorTexture[4];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        /// <summary>可参与连线：已成形且不在收场</summary>
        internal bool LinkReady => age > MaterializeFrames && EvictState < 1.5f && Projectile.timeLeft > FadeOutFrames;

        /// <summary>成形进度(含寿终/让位收缩)，绘制与判定共用</summary>
        internal float Grow {
            get {
                float grow = QueenMotion.SnapOut(age / (float)MaterializeFrames, 4);
                if (Projectile.timeLeft < FadeOutFrames) {
                    grow *= Projectile.timeLeft / (float)FadeOutFrames;
                }
                if (EvictState > 1.5f) {
                    grow *= MathHelper.Clamp(1f - evictTimer / (float)EvictFrames, 0f, 1f);
                }
                return grow;
            }
        }

        /// <summary>呼吸浮动(纯视觉)，各端按 identity 同式自算，水晶壳与光束端点共用</summary>
        internal static Vector2 VisualBob(Projectile p) {
            return new Vector2(0f, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.1f + p.identity * 1.7f) * 5f);
        }

        public override void AI() {
            age++;
            Projectile.velocity = Vector2.Zero;

            //出生：凝胶落点
            if (age == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.3f, Pitch = -0.15f, MaxInstances = 5 }, Projectile.Center);
                QueenMotion.GelSplashBurst(Projectile.Center, 0.5f, 4);
            }

            //凝结相变：凝胶滴自四周弧线坠入成形点
            if (age < MaterializeFrames - 6 && age % 3 == 0 && !VaultUtils.isServer) {
                Vector2 off = Main.rand.NextVector2CircularEdge(32f, 26f);
                PRTLoader.NewParticle<PRT_QueenGelDrop>(Projectile.Center + off,
                    -off * 0.085f + new Vector2(0f, -1.3f), QueenMotion.RoyalPink * 0.9f,
                    Main.rand.NextFloat(0.6f, 1f));
            }

            //成晶拍：波纹+晶鸣
            if (age == MaterializeFrames && !VaultUtils.isServer) {
                QueenMotion.LandingRingFX(Projectile.Center, 0.55f, HueSeed);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.55f, MaxInstances = 5 }, Projectile.Center);
            }

            //让位快碎推进(各端按同步的 ai[2] 各自演)
            if (EvictState > 1.5f) {
                evictTimer++;
                if (evictTimer >= EvictFrames && Projectile.owner == Main.myPlayer) {
                    shatteredByEnemy = false;
                    Projectile.Kill();
                }
            }

            //连线数与假束搭档缓存(供蓄能辉光/满编暗弦)，每12帧一趟
            if (++linkCountTimer >= 12) {
                linkCountTimer = 0;
                RefreshLinkCaches();
            }

            //owner 端逐帧刷新接触伤害，长驻水晶吃到加成变化
            if (Projectile.owner == Main.myPlayer) {
                Projectile.damage = (int)Main.player[Projectile.owner]
                    .GetTotalDamage(DamageClass.Generic).ApplyTo(ContactDamage);
            }

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * (0.5f * Grow));

            //驻场微光尘
            if (!VaultUtils.isServer && LinkReady && Main.rand.NextBool(24)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 20f),
                    DustID.TintableDust, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f), 160,
                    QueenMotion.GetQueenDustColor(), 0.85f);
                d.noGravity = true;
            }
        }

        /// <summary>
        /// 单趟缓存刷新：数与自己相连的光束(蓄能辉光用)；
        /// 满编(6颗全部成形)时按布晶序取对角搭档(rank 0↔3、1↔4)，供装饰假束绘制。
        /// 假束无判定不入和弦，纯加色视觉
        /// </summary>
        private void RefreshLinkCaches() {
            int beamType = ModContent.ProjectileType<RefractionWaltzBeamProj>();
            int count = 0;
            Span<(float seq, int idx)> formed = stackalloc (float, int)[RefractionWaltzPlayer.MaxCrystals + 2];
            int formedCount = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != Projectile.owner) {
                    continue;
                }
                if (proj.type == beamType) {
                    if ((int)proj.ai[0] == Projectile.identity || (int)proj.ai[1] == Projectile.identity) {
                        count++;
                    }
                    continue;
                }
                if (proj.type == Projectile.type && formedCount < formed.Length
                    && proj.ModProjectile is RefractionWaltzCrystalProj sibling && sibling.LinkReady) {
                    formed[formedCount++] = (proj.ai[0], proj.whoAmI);
                }
            }
            linkCountCache = count;

            fakePartnerIdx = -1;
            if (formedCount != RefractionWaltzPlayer.MaxCrystals || !LinkReady) {
                return;
            }
            //布晶序插入排序(规模≤6)
            for (int i = 1; i < formedCount; i++) {
                (float seq, int idx) cur = formed[i];
                int j = i - 1;
                while (j >= 0 && formed[j].seq > cur.seq) {
                    formed[j + 1] = formed[j];
                    j--;
                }
                formed[j + 1] = cur;
            }
            for (int rank = 0; rank < 2; rank++) {
                if (formed[rank].idx == Projectile.whoAmI) {
                    fakePartnerIdx = formed[rank + 3].idx;
                    return;
                }
            }
        }

        /// <summary>让位快碎(owner 端调用)：写状态并同步，几帧后自杀</summary>
        internal void BeginEvict() {
            if (EvictState > 1.5f) {
                return;
            }
            EvictState = 2f;
            Projectile.netUpdate = true;
        }

        /// <summary>成形前与收场中无判定</summary>
        public override bool? CanDamage() {
            return age > MaterializeFrames * 0.6f && EvictState < 1.5f && Projectile.timeLeft > FadeOutFrames ? null : false;
        }

        /// <summary>被撞碎：盖折光、记迸射方向、当场碎裂(owner 端命中钩)</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<RefractionTag>(), RefractionTag.TagFrames);
            shatteredByEnemy = true;
            shatterDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            bool violent = shatteredByEnemy || EvictState > 1.5f;
            if (!VaultUtils.isServer) {
                QueenMotion.CrystalShatterBurst(Projectile.Center, violent ? 0.62f : 0.38f, HueSeed, playSound: violent);
                if (!violent) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.7f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //碎晶朝来敌迸出(owner 端生成，走原版同步；短寿命弹生成时折算即可)
            if (shatteredByEnemy && Projectile.owner == Main.myPlayer) {
                int shardDamage = (int)Main.player[Projectile.owner]
                    .GetTotalDamage(DamageClass.Generic).ApplyTo(RefractionWaltzShardProj.ShardDamage);
                int count = 5;
                for (int i = 0; i < count; i++) {
                    float spread = MathHelper.Lerp(-0.5f, 0.5f, i / (float)(count - 1));
                    Vector2 vel = shatterDir.RotatedBy(spread) * Main.rand.NextFloat(9.5f, 11.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<RefractionWaltzShardProj>(), shardDamage,
                        2f, Projectile.owner, (HueSeed + i * 0.11f) % 1f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>晶壳(棱晶着色器 quad，预乘进 AlphaBlend)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            float grow = Grow;
            if (grow <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.QueenPrismCrystal?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return; //着色器缺失时加色层仍有可读晶体，无隐形判定
            }

            Vector2 center = Projectile.Center + VisualBob(Projectile);
            float half = ShellHalfSize * (0.4f + 0.6f * grow);
            VertexPositionColorTexture[] verts = shellVerts;
            verts[0] = new VertexPositionColorTexture(new Vector3(center.X - half, center.Y - half, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(center.X + half, center.Y - half, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(center.X - half, center.Y + half, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(center.X + half, center.Y + half, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            //共享参数化 shader：调用点全参数重设
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(0f);
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uShatter"]?.SetValue(EvictState > 1.5f ? evictTimer / (float)EvictFrames : 0f);
            effect.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(linkCountCache * 0.14f, 0f, 0.42f));
            effect.Parameters["uHueSeed"]?.SetValue(HueSeed);
            effect.Parameters["seed"]?.SetValue(Projectile.identity * 0.173f % 1f);
            //噪声显式绑 s1(shader 内 register(s1))，用毕交还
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
            device.Textures[1] = null;

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>光冕与凝胶前体(真 Additive 批，染色带 alpha)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float grow = Grow;
            Vector2 drawPos = Projectile.Center + VisualBob(Projectile) - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Color hue = QueenMotion.PrismHue(HueSeed);

            //凝胶前体：未成形时的抖动胶滴(表面张力呼吸)
            float gelPhase = 1f - MathHelper.Clamp(age / (float)MaterializeFrames, 0f, 1f);
            if (gelPhase > 0.01f) {
                float wob = 0.14f * (float)Math.Sin(age * 0.55f);
                Vector2 gelScale = new Vector2(0.5f + wob, 0.5f - wob) * (0.5f + 0.5f * gelPhase);
                spriteBatch.Draw(glow, drawPos, null, QueenMotion.RoyalPink * (0.85f * gelPhase), 0f,
                    glow.Size() / 2f, gelScale, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, Color.White * (0.5f * gelPhase), 0f,
                    glow.Size() / 2f, gelScale * 0.45f, SpriteEffects.None, 0f);
            }

            if (grow <= 0.02f) {
                return;
            }

            //光冕：受连线数增辉
            float charge = 1f + linkCountCache * 0.1f;
            float flick = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.identity * 3f);
            spriteBatch.Draw(glow, drawPos, null, hue * (0.5f * grow * charge), 0f,
                glow.Size() / 2f, 0.85f * flick * grow, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, Color.White * (0.3f * grow), 0f,
                glow.Size() / 2f, 0.4f * grow, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, hue * (0.5f * grow * charge),
                Main.GlobalTimeWrappedHourly * 1.4f + Projectile.identity, star.Size() / 2f,
                0.3f * grow, SpriteEffects.None, 0f);

            //装饰假束：满编时的对角暗弦，补"光网琴弦"观感密度(无判定、不入和弦)
            if (fakePartnerIdx >= 0 && fakePartnerIdx < Main.maxProjectiles) {
                Projectile partner = Main.projectile[fakePartnerIdx];
                if (partner.active && partner.type == Projectile.type && partner.owner == Projectile.owner
                    && partner.ModProjectile is RefractionWaltzCrystalProj pc && pc.LinkReady) {
                    DrawFakeBeam(spriteBatch, partner, pc.Grow);
                }
            }
        }

        /// <summary>对角暗弦：分段光条正弦包络，两端归零，亮度压在0.4档之下</summary>
        private void DrawFakeBeam(SpriteBatch spriteBatch, Projectile partner, float partnerGrow) {
            Texture2D lineTex = CWRAsset.LightShot.Value;
            Vector2 a = Projectile.Center + VisualBob(Projectile);
            Vector2 b = partner.Center + VisualBob(partner);
            float len = Vector2.Distance(a, b);
            if (len < 24f) {
                return;
            }
            Vector2 dir = (b - a) / len;
            Color hue = QueenMotion.PrismHue((HueSeed + partner.ai[1]) * 0.5f + 0.31f);
            float bright = 0.4f * Grow * partnerGrow;
            int segments = Math.Max((int)(len / 30f), 3);
            float segLen = len / segments;
            for (int i = 0; i < segments; i++) {
                float t = (i + 0.5f) / segments;
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                Vector2 pos = a + dir * len * t - Main.screenPosition;
                spriteBatch.Draw(lineTex, pos, null, hue * (bright * envelope), dir.ToRotation(),
                    lineTex.Size() / 2f, new Vector2(segLen * 1.25f / lineTex.Width, 0.03f + 0.05f * envelope),
                    SpriteEffects.None, 0f);
            }
        }
    }
}
