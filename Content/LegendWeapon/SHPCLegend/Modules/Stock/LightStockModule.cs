using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>轻量枪托，主光束拖 2 节光链，各 40% 伤</summary>
    internal sealed class LightStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //轻量银白
        public override Color TintColor => new(220, 230, 240);

        private const float SegmentDamageRatio = 0.4f;

        /// <summary>已挂载子节的光束，whoAmI→identity 防槽位复用误判，OnBeamKill 清理</summary>
        private readonly Dictionary<int, int> linkedBeams = [];
        private readonly List<int> purgeScratch = [];

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.2f;
            ctx.DamageMul += -0.3f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            int who = beam.Projectile.whoAmI;
            int identity = beam.Projectile.identity;
            if (linkedBeams.TryGetValue(who, out int linkedId) && linkedId == identity) return;
            linkedBeams[who] = identity;

            //挂 2 节，滞后 12/24 帧；ai0 传 identity，whoAmI 槽位跨端不一致
            int dmg = Math.Max((int)(beam.Projectile.damage * SegmentDamageRatio), 1);
            for (int i = 1; i <= 2; i++) {
                Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                    beam.Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<SHPCBeamSegmentProj>(),
                    dmg, beam.Projectile.knockBack * 0.5f, beam.Projectile.owner,
                    ai0: identity,
                    ai1: i * 12,
                    ai2: beam.Projectile.ai[0]); //继承主题色索引
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            linkedBeams.Remove(beam.Projectile.whoAmI);
        }

        public override void OnPlayerUpdate(Player player) {
            //卸改件窗口 OnBeamKill 不达，周期清扫防 whoAmI 复用堵住新束
            if (linkedBeams.Count == 0) return;
            purgeScratch.Clear();
            foreach ((int who, int id) in linkedBeams) {
                Projectile p = Main.projectile[who];
                if (!p.active || p.identity != id || p.owner != player.whoAmI
                    || p.ModProjectile is not CyberTraceBeamProj) {
                    purgeScratch.Add(who);
                }
            }
            foreach (int who in purgeScratch) {
                linkedBeams.Remove(who);
            }
        }
    }

    /// <summary>光链子节，沿父束 oldPos 滞后，节间张力链缆，父亡链断散节</summary>
    internal sealed class SHPCBeamSegmentProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //主题色对齐 CyberTraceBeamProj 索引
        private static readonly Color[] ThemeMain = [
            new Color(110, 180, 255),
            new Color(255, 215, 110),
            new Color(110, 245, 225),
        ];
        private static readonly Color[] ThemeEdge = [
            new Color(35, 70, 190),
            new Color(190, 130, 30),
            new Color(25, 150, 140),
        ];
        //超驱熔岩色，对齐 CyberTraceBeamProj.OverdriveTheme
        private static readonly Color ODMain = new(255, 170, 60);
        private static readonly Color ODEdge = new(255, 55, 20);

        private const int LinkSamples = 5;      //链缆采样点数
        private const int LinkStep = 3;         //oldPos 采样步长
        private const int LinkGraceFrames = 20; //远端父束生成包迟到宽限

        private int ParentIdentity => (int)Projectile.ai[0];
        private int LagFrames => (int)Projectile.ai[1];
        private int ThemeIndex => Math.Clamp((int)Projectile.ai[2], 0, ThemeMain.Length - 1);

        private float fadeAlpha;
        private bool orphaned;
        private bool everLinked;
        private int linkGrace = LinkGraceFrames;
        private int cachedParentIdx = -1;
        private float odAmount;
        private readonly Vector2[] linkPoints = new Vector2[LinkSamples];
        private int linkCount;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => orphaned;

        /// <summary>owner+identity+type 解析父束，whoAmI 槽位跨端不一致；缓存槽位取用时校验</summary>
        private Projectile ResolveParent() {
            if (cachedParentIdx >= 0) {
                Projectile cached = Main.projectile[cachedParentIdx];
                if (cached.active && cached.identity == ParentIdentity
                    && cached.owner == Projectile.owner
                    && cached.ModProjectile is CyberTraceBeamProj) {
                    return cached;
                }
                cachedParentIdx = -1;
            }
            if (ParentIdentity < 0) {
                return null;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.identity == ParentIdentity
                    && p.ModProjectile is CyberTraceBeamProj) {
                    cachedParentIdx = i;
                    return p;
                }
            }
            return null;
        }

        /// <summary>沿父束 oldPos 取链缆采样点，末点吸附父束头</summary>
        private void BuildLinkPoints(Projectile parent) {
            int lag = Math.Clamp(LagFrames, 0, parent.oldPos.Length - 1);
            int pred = Math.Max(lag - 12, 0);
            Vector2 half = parent.Size * 0.5f;
            linkCount = 0;
            for (int idx = lag; idx >= pred && linkCount < LinkSamples; idx -= LinkStep) {
                Vector2 raw = parent.oldPos[idx];
                linkPoints[linkCount++] = idx == 0 || raw == Vector2.Zero
                    ? parent.Center : raw + half;
            }
        }

        public override void AI() {
            //散链不可逆，跳过全表解析
            Projectile parent = orphaned ? null : ResolveParent();

            if (parent != null) {
                everLinked = true;
                Projectile.timeLeft = 60; //时缓下父束实际帧寿命可超默认值，跟随期持续续命
                //父束 oldPos 滞后，未填则用当前位置
                int lag = Math.Clamp(LagFrames, 0, parent.oldPos.Length - 1);
                Vector2 raw = parent.oldPos[lag];
                Vector2 targetPos = raw == Vector2.Zero ? parent.Center : raw + parent.Size * 0.5f;
                Vector2 delta = targetPos - Projectile.Center;
                Projectile.Center = targetPos;
                if (delta.LengthSquared() > 0.01f) {
                    Projectile.rotation = delta.ToRotation();
                    Projectile.velocity = delta * 0.4f; //仅作击退方向参考
                }
                BuildLinkPoints(parent);
                fadeAlpha = MathF.Min(fadeAlpha + 0.1f, 1f);
            }
            else if (!everLinked) {
                //远端生成包可能先于父束解析到达，宽限期隐身等待
                linkCount = 0;
                fadeAlpha = 0f;
                linkGrace--;
                if (linkGrace <= 0 && Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }
            else {
                //父亡，链断散节 18 帧
                linkCount = 0;
                if (!orphaned) {
                    orphaned = true;
                    Projectile.timeLeft = 18;
                    Projectile.velocity = Projectile.rotation.ToRotationVector2() * 7f;
                    SnapFX();
                }
                Projectile.velocity *= 0.93f;
                fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            }

            //超驱色跟随，与父束同条件同速率
            bool inDomain = Cyberspace.IsInsideDomainOf(Projectile.owner, Projectile.Center);
            odAmount = MathHelper.Lerp(odAmount, inDomain ? 1f : 0f, 0.055f);
            if (odAmount < 0.005f) odAmount = 0f;

            Color mainCol = Color.Lerp(ThemeMain[ThemeIndex], ODMain, odAmount);
            Lighting.AddLight(Projectile.Center, mainCol.ToVector3() * 0.35f * fadeAlpha);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    mainCol, Main.rand.NextFloat(0.3f, 0.6f))
                    .Configure(Color.Lerp(ThemeEdge[ThemeIndex], ODEdge, odAmount), Main.rand.Next(8, 16));
            }
        }

        /// <summary>链断火花</summary>
        private void SnapFX() {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(2.4f, 2.4f),
                    Color.Lerp(ThemeMain[ThemeIndex], ODMain, odAmount), Main.rand.NextFloat(0.35f, 0.7f))
                    .Configure(Color.Lerp(ThemeEdge[ThemeIndex], ODEdge, odAmount), Main.rand.Next(8, 14));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.25f, Pitch = 0.6f }, target.Center);
            Color mainCol = Color.Lerp(ThemeMain[ThemeIndex], ODMain, odAmount);
            Color edgeCol = Color.Lerp(ThemeEdge[ThemeIndex], ODEdge, odAmount);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center,
                    Main.rand.NextVector2CircularEdge(3f, 3f),
                    mainCol, Main.rand.NextFloat(0.4f, 0.8f))
                    .Configure(edgeCol, Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) return;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color main = Color.Lerp(ThemeMain[ThemeIndex], ODMain, odAmount);
            Color edge = Color.Lerp(ThemeEdge[ThemeIndex], ODEdge, odAmount);
            float time = (float)Main.timeForVisualEffects;
            float pulse = 0.85f + 0.15f * MathF.Sin(time * 0.2f + LagFrames);

            //节间链缆压节点之下，张力脉冲沿链向节点回流
            if (linkCount >= 2 && white != null) {
                for (int i = 0; i < linkCount - 1; i++) {
                    Vector2 a = linkPoints[i];
                    Vector2 d = linkPoints[i + 1] - a;
                    float len = d.Length();
                    if (len < 0.5f) continue;
                    float rot = d.ToRotation();
                    //相位按 oldPos 索引 LagFrames-i*LinkStep 参数化，两节缆跨共享端点连续，波峰自束头回流向节点
                    float wave = 0.55f + 0.45f * MathF.Sin(time * 0.22f - (LagFrames - i * LinkStep) * 0.45f);
                    spriteBatch.Draw(white, a - Main.screenPosition, null,
                        edge * (fadeAlpha * 0.7f * wave), rot,
                        new Vector2(0f, 0.5f), new Vector2(len, 2.4f), SpriteEffects.None, 0f);
                    spriteBatch.Draw(white, a - Main.screenPosition, null,
                        Color.Lerp(main, Color.White, 0.5f) * (fadeAlpha * 0.5f * wave), rot,
                        new Vector2(0f, 0.5f), new Vector2(len, 1f), SpriteEffects.None, 0f);
                }
            }

            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, edge * fadeAlpha * 0.6f * pulse, 0f,
                    glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, main * fadeAlpha * 0.8f * pulse, 0f,
                    glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0f);
            }
            if (white != null) {
                //节体胶囊光块，后节更小，散链随速拉伸
                float bodyLen = LagFrames > 12 ? 16f : 20f;
                if (orphaned) {
                    bodyLen *= 1f + Projectile.velocity.Length() * 0.1f;
                }
                spriteBatch.Draw(white, drawPos, null, main * fadeAlpha * 0.95f,
                    Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(bodyLen, 7f), SpriteEffects.None, 0f);
                spriteBatch.Draw(white, drawPos, null, Color.White * fadeAlpha * 0.85f,
                    Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(bodyLen * 0.55f, 3.5f), SpriteEffects.None, 0f);
            }
        }
    }
}
