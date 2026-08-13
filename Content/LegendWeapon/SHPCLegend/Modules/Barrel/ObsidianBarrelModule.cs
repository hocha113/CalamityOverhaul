using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>自然元素枪管共享 VFX，屏震/晕/owned 计数与近距节流</summary>
    internal static class SHPCNaturalFx
    {
        /// <summary>多层柔光，inner→outer</summary>
        public static void GlowLayered(SpriteBatch sb, Texture2D tex, Vector2 screenPos,
            Color inner, Color outer, float scale, float rotation, int layers = 3) {
            if (tex == null) return;
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < layers; i++) {
                float t = layers <= 1 ? 0f : i / (float)(layers - 1);
                Color c = Color.Lerp(inner, outer, t);
                float layerScale = scale * (1f + t * 0.6f);
                sb.Draw(tex, screenPos, null, c, rotation, origin, layerScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>屏震，仅本地</summary>
        public static void Shake(float amount) {
            if (amount <= 0f || Main.netMode == NetmodeID.Server) return;
            Player p = Main.LocalPlayer;
            if (p == null) return;
            if (p.TryGetModPlayer(out CWRPlayer cp)) cp.GetScreenShake(amount);
        }

        /// <summary>同主同类型活跃数</summary>
        public static int CountOwned(int owner, int type) {
            int n = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == type) n++;
            }
            return n;
        }

        /// <summary>半径内是否已有同主同型</summary>
        public static bool HasOwnedNear(int owner, int type, Vector2 center, float radius) {
            float r2 = radius * radius;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != owner || p.type != type) continue;
                if (Vector2.DistanceSquared(p.Center, center) <= r2) return true;
            }
            return false;
        }
    }

    /// <summary>黑曜石枪管，命中叠裂纹，满层碎为寻敌碎片</summary>
    internal sealed class ObsidianBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(95, 55, 135);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += -0.15f;
            ctx.DamageMul += -0.1f;
            ctx.ManaCostMul += 0.35f;
            ctx.BeamExtraPierce += 1;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyObsidianCrack(target, 300, beam.Projectile.owner, Math.Max(damageDone / 5, 1));
            }
            //壳裂即时反馈,命中点崩落暗玻碎屑;钩子仅非服务端触发,节流防多束刷屏
            if (Main.netMode == NetmodeID.Server || !Main.rand.NextBool(2)) return;
            Vector2 hitPos = beam.Projectile.Center;
            Vector2 backDir = -beam.Projectile.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = backDir.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 3.4f) - Vector2.UnitY * 1.2f;
                PRTLoader.NewParticle<PRT_SHPCObsidianChip>(hitPos, vel, new Color(30, 16, 44),
                    Main.rand.NextFloat(0.5f, 0.8f)).Configure(new Color(255, 120, 45), Main.rand.Next(20, 32), 0.7f);
            }
            PRTLoader.NewParticle<PRT_Sparkle>(hitPos, Vector2.Zero, new Color(210, 170, 255), 0.4f)
                .Configure(new Color(120, 60, 200), 8, 0.1f, 0.6f);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > 520f * 520f) continue;
                if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) continue;
                if (eff.ObsidianCrackTime <= 0 || eff.ObsidianCrackOwner != orb.Projectile.owner) continue;
                //联机下 BurstObsidian 在客户端是 no-op，直调会让爆发永远不发生；走请求通道
                eff.RequestObsidianBurst(npc, orb.Projectile.owner,
                    Math.Max(orb.Projectile.damage / 3, 1));
            }
        }
    }

    /// <summary>黑曜石碎晶，速冷玻璃薄片；暗玻剪影+断口热缘随飞行冷却+镜闪，Trail 同步降温</summary>
    internal sealed class SHPCObsidianShardProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TrailLen = 10;
        private const int LifeFrames = 90;
        private static readonly Color CoreColor = new(255, 110, 50);
        private static readonly Color EdgeColor = new(80, 35, 110);
        //出膛断口炽热,飞行中降温收紫,颜色演化即飞行期时间签名
        private static readonly Vector3 HotCoreVec = new Color(255, 200, 110).ToVector3();
        private static readonly Vector3 HotGlowVec = new Color(255, 90, 40).ToVector3();
        private static readonly Vector3 HotAuraVec = new Color(70, 25, 90).ToVector3();
        private static readonly Vector3 CoolCoreVec = new Color(130, 82, 195).ToVector3();
        private static readonly Vector3 CoolGlowVec = new Color(72, 40, 128).ToVector3();
        private static readonly Vector3 CoolAuraVec = new Color(36, 16, 58).ToVector3();

        private Vector2[] trailPoints;
        private Trail trail;
        private float fadeAlpha;
        private float cool01;   //0=出膛热 1=完全冷却
        private float tumble;   //翻面相位,纯视觉

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            //碎片由服务端代生成（BurstObsidian），SyncProjectile 伤害是 short，带全量兜底
            writer.Write(Projectile.damage);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int fullDamage = reader.ReadInt32();
            if (fullDamage > 0) {
                Projectile.damage = fullDamage;
            }
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            //玻璃降温曲线,约2/3行程冷透
            cool01 = MathHelper.Clamp((LifeFrames - Projectile.timeLeft) / (float)LifeFrames * 1.5f, 0f, 1f);
            tumble += 0.23f;
            //首帧玻璃喷出点缀
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2.5f), CoreColor, Main.rand.NextFloat(0.3f, 0.6f)).Configure(EdgeColor, Main.rand.Next(8, 16), Main.rand.NextFloat(-0.2f, 0.2f), 0.7f);
                    }
                }
            }
            //光色随冷却由橙热转暗紫
            Vector3 lightVec = Vector3.Lerp(new Vector3(0.85f, 0.32f, 0.18f), new Vector3(0.3f, 0.16f, 0.42f), cool01);
            Lighting.AddLight(Projectile.Center, lightVec * fadeAlpha);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.45f, Pitch = 0.4f }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, Pitch = -0.2f }, target.Center);
            //小爆，CyberDetonation 50px
            if (Projectile.owner == Main.myPlayer) {
                int dmg = Math.Max(Projectile.damage / 8, 1);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, Projectile.owner, ai0: 0.3f, ai1: 0f, ai2: 50f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].usesLocalNPCImmunity = true;
                    Main.projectile[idx].localNPCHitCooldown = 30;
                }
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4.2f, 4.2f);
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, vel, CoreColor, Main.rand.NextFloat(0.5f, 1.0f)).Configure(EdgeColor, Main.rand.Next(14, 26), Main.rand.NextFloat(-0.3f, 0.3f), 0.9f);
            }
            //玻璃碎裂,暗片带重力坠落
            float hitHeat = MathF.Pow(1f - cool01, 1.5f);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.2f, 3.2f) - Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f);
                PRTLoader.NewParticle<PRT_SHPCObsidianChip>(target.Center + vel * 2f, vel, new Color(28, 15, 42),
                    Main.rand.NextFloat(0.55f, 1f)).Configure(new Color(255, 120, 45), Main.rand.Next(22, 36), hitHeat);
            }
            SHPCNaturalFx.Shake(2.5f);
        }

        public override void OnKill(int timeLeft) {
            //碎屑余韵,独立实体活得比弹幕久,尾向散落
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            float heat = MathF.Pow(1f - cool01, 1.5f);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = back.RotatedByRandom(0.7f) * Main.rand.NextFloat(1.2f, 3f) - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.4f);
                PRTLoader.NewParticle<PRT_SHPCObsidianChip>(Projectile.Center + back * Main.rand.NextFloat(0f, 18f), vel,
                    new Color(28, 15, 42), Main.rand.NextFloat(0.5f, 0.9f)).Configure(new Color(255, 110, 45), Main.rand.Next(18, 30), heat);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + back * Main.rand.NextFloat(6f, 26f),
                    back * Main.rand.NextFloat(0.5f, 1.5f), new Color(190, 150, 255), Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(EdgeColor, Main.rand.Next(8, 14), 0.15f, 0.7f);
            }
        }

        private float WidthFunction(float progress) {
            //冷却收窄
            float head = MathHelper.Lerp(8f, 5.2f, cool01);
            float taper = MathHelper.Lerp(head, 0f, progress);
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.5f + progress * 6f);
            return taper * pulse;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2 || fadeAlpha < 0.05f) return;

            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //oldPos→trail，零向量首帧用 head
            trailPoints ??= new Vector2[TrailLen];
            Vector2 head = Projectile.Center;
            for (int i = 0; i < TrailLen; i++) {
                Vector2 raw = i < Projectile.oldPos.Length ? Projectile.oldPos[i] : Vector2.Zero;
                trailPoints[i] = raw == Vector2.Zero ? head : raw + Projectile.Size * 0.5f;
            }

            trail ??= new Trail(trailPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = trailPoints;

            //拖尾颜色跟随玻璃降温
            Vector3 coreVec = Vector3.Lerp(HotCoreVec, CoolCoreVec, cool01);
            Vector3 glowVec = Vector3.Lerp(HotGlowVec, CoolGlowVec, cool01);
            Vector3 auraVec = Vector3.Lerp(HotAuraVec, CoolAuraVec, cool01);
            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["coreColor"]?.SetValue(coreVec);
            shader.Parameters["glowColor"]?.SetValue(glowVec);
            shader.Parameters["auraColor"]?.SetValue(auraVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(0f);
            shader.Parameters["glitchBurst"]?.SetValue(0f);
            shader.Parameters["odCoreColor"]?.SetValue(coreVec);
            shader.Parameters["odGlowColor"]?.SetValue(glowVec);
            shader.Parameters["odAuraColor"]?.SetValue(auraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.05f) return;
            float heat = MathF.Pow(1f - cool01, 1.5f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            //底层辉光随冷却由橙热退成冷紫微光,只作衬垫
            Color inner = Color.Lerp(new Color(255, 200, 110), new Color(150, 100, 220), cool01) * (fadeAlpha * (0.4f + 0.6f * heat));
            Color outer = Color.Lerp(new Color(120, 40, 90), new Color(56, 28, 86), cool01) * (fadeAlpha * 0.3f);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screenPos, inner, outer, 0.6f, Projectile.rotation, 3);
            //方向拖芒随热度熄灭;真加色批 tint 必须带 A,A=0 整张不显示
            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null && heat > 0.05f) {
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, screenPos, null,
                    new Color(255, 130, 60) * (fadeAlpha * 0.55f * heat),
                    Projectile.rotation, origin, new Vector2(0.5f, 0.35f), SpriteEffects.None, 0f);
            }
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            //暗玻剪影本体,压在拖尾与辉光之上;AlphaBlend 层才画得出暗色
            if (fadeAlpha < 0.05f) return;
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) return;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float heat = MathF.Pow(1f - cool01, 1.5f);
            //横宽随翻面相位呼吸,读作薄片翻滚
            float flip = 0.45f + 0.55f * MathF.Abs(MathF.Sin(tumble));
            float rot = Projectile.rotation + MathHelper.PiOver2;
            Vector2 scale = new(0.16f * flip, 0.46f);
            //冷紫衬缘在下,暗体在上
            Color rim = Color.Lerp(new Color(140, 80, 200), new Color(70, 40, 110), cool01) * (fadeAlpha * 0.5f);
            spriteBatch.Draw(tex, pos, null, rim, rot, origin, scale * new Vector2(1.5f, 1.06f), SpriteEffects.None, 0f);
            Color body = new Color(26, 14, 38) * (fadeAlpha * 0.95f);
            spriteBatch.Draw(tex, pos, null, body, rot, origin, scale, SpriteEffects.None, 0f);
            //断口热尖,A=0 预乘加亮,冷却即熄
            if (heat > 0.04f) {
                Vector2 tip = Projectile.velocity.SafeNormalize(Vector2.Zero) * (72f * scale.Y * 0.36f);
                Color hot = new Color(255, 170, 70, 0) * (fadeAlpha * heat * 0.85f);
                spriteBatch.Draw(tex, pos + tip, null, hot, rot, origin, scale * new Vector2(0.7f, 0.3f), SpriteEffects.None, 0f);
            }
            //翻面对齐瞬间的窄镜闪,玻璃质签名
            float glint = MathF.Pow(MathF.Max(MathF.Cos(tumble * 2f), 0f), 20f);
            if (glint > 0.15f) {
                Color gc = new Color(226, 214, 248, 0) * (glint * 0.55f * fadeAlpha);
                spriteBatch.Draw(tex, pos, null, gc, rot, origin, scale * new Vector2(0.45f, 1.28f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //Trail+Additive+Overlay 接管，PreDraw 空
            return false;
        }
    }
}
