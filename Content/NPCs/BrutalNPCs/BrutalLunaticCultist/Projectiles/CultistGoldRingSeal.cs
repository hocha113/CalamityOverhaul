using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 金环封阵:司祭以浑天仪为模铸出的金环,飞抵预判位翻转平铺钉界,环缘点燃成囚阵<br/>
    /// ai[0]=宿主npc ai[1]/ai[2]=钉界圆心世界坐标(出手即锁死,预告即承诺)<br/>
    /// 公平阀:GapHalfAngle 声明缺口门(可见开口略窄于安全区);缺口以 GapDrift 匀速缓行(环径处步行可跟);<br/>
    /// 飞行/预热/淡出全程无伤,伤害窗=点燃可见窗;缺口初向场心=向开阔处逃生
    /// </summary>
    internal class CultistGoldRingSeal : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int FlyFrames = 24;
        private const int WarnFrames = 18;
        private const int FireFrames = 96;
        private const int FadeFrames = 14;
        internal const int Lifetime = FlyFrames + WarnFrames + FireFrames + FadeFrames;
        /// <summary>钉界环半径(px)</summary>
        internal const float SealRadius = 340f;
        /// <summary>判定带半宽:窄于可见金带</summary>
        private const float BandHalf = 26f;
        /// <summary>声明缺口半角(rad):判定跳过与绘制开口同参</summary>
        internal const float GapHalfAngle = 0.52f;
        /// <summary>缺口进动速率(rad/f):环径处切速约 3.4px/f,步行可跟</summary>
        internal const float GapDrift = 0.010f;

        private int OwnerWho => (int)Projectile.ai[0];
        private Vector2 SealCenter => new(Projectile.ai[1], Projectile.ai[2]);
        private float Age => Lifetime - Projectile.timeLeft;
        private bool Planted => Age >= FlyFrames;
        private bool Firing => Age >= FlyFrames + WarnFrames && Age < FlyFrames + WarnFrames + FireFrames;

        private bool plantBeatDone;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            //配合 DrawBehind 设 hide,免得普通弹幕层重复画一遍环带盖住其他弹幕
            Projectile.hide = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>缺口基角:初向场心(向开阔处逃生);各端由同步数据一致推导</summary>
        private float GapBase(NPC owner) {
            Vector2 toward = owner.Center;
            if (owner.TryGetOverride(out CultistBossAI overrideAI)
                && overrideAI.Context is { ArenaSpawned: true } ctx) {
                toward = ctx.ArenaCenter;
            }
            return (toward - SealCenter).ToRotation();
        }

        /// <summary>进动方向:圆心坐标奇偶定签名,各端一致</summary>
        private float DriftSign => ((int)MathF.Abs(Projectile.ai[1]) & 1) == 0 ? 1f : -1f;

        /// <summary>当前缺口中心角:点燃后匀速进动</summary>
        private float GapCenter(NPC owner) {
            float fireAge = MathHelper.Max(Age - FlyFrames - WarnFrames, 0f);
            return GapBase(owner) + DriftSign * GapDrift * fireAge;
        }

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                CultistMotion.RuneBurst(Projectile.Center, CultistMotion.RuneGold, 8, 5f);
                Projectile.Kill();
                return;
            }
            float age = Age;

            if (age < FlyFrames) {
                //飞抵:自司祭滑向钉界位(视觉互动,判定未开)
                float t = age / FlyFrames;
                float ease = 1f - (1f - t) * (1f - t);
                Projectile.Center = Vector2.Lerp(owner.Center, SealCenter, ease);
            }
            else {
                Projectile.Center = SealCenter;
                //钉界拍(各端本地一次)
                if (!plantBeatDone) {
                    plantBeatDone = true;
                    CultistMotion.SigilCommitFX(SealCenter, CultistMotion.RuneGold, 1.5f);
                    CultistMotion.Shake(SealCenter, 5f, 11);
                    CultistMotion.RuneBurst(SealCenter, CultistMotion.RuneGold, 10, 7f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.9f, Pitch = -0.35f }, SealCenter);
                    }
                }
                //点燃拍
                if ((int)age == FlyFrames + WarnFrames) {
                    CultistScreenFX.PushFlash(0.2f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.1f }, SealCenter);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.RuneGold.ToVector3() * (Firing ? 0.7f : 0.35f));
        }

        /// <summary>伤害窗=点燃可见窗</summary>
        public override bool CanHitPlayer(Player target) => Firing;

        /// <summary>环带判定:半径带内且不在缺口门扇区</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Firing) {
                return false;
            }
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active) {
                return false;
            }
            Vector2 delta = targetHitbox.Center.ToVector2() - SealCenter;
            float dist = delta.Length();
            if (Math.Abs(dist - SealRadius) > BandHalf + 18f) {
                return false;
            }
            float angDelta = Math.Abs(MathHelper.WrapAngle(delta.ToRotation() - GapCenter(owner)));
            return angDelta > GapHalfAngle;
        }

        public override void OnKill(int timeLeft) {
            //碎环:金符四散,活过弹体
            CultistMotion.RuneBurst(SealCenter, CultistMotion.RuneGold, 16, 9f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.7f, Pitch = 0.2f }, SealCenter);
            }
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active) {
                return false;
            }
            float age = Age;
            Color gold = CultistMotion.RuneGold;
            Color bright = Color.Lerp(gold, Color.White, 0.55f);
            Color deep = new(64, 46, 18);
            SpriteBatch sb = Main.spriteBatch;

            //飞行段:3D 金环自侧立翻转平铺(浑天仪语汇),DrawRing 走设备图元须出批
            if (age < FlyFrames) {
                float t = age / FlyFrames;
                float pitch = MathHelper.Lerp(1.35f, 0f, t * t);
                float yaw = (1f - t) * 2.6f;
                CultistOrreryRig.BuildBasis(yaw, pitch, out Vector3 e1, out Vector3 e2);
                float radius = MathHelper.Lerp(130f, SealRadius, t);
                sb.End();
                CultistOrreryRenderer.DrawRing(Projectile.Center, e1, e2, radius, 11f,
                    Main.GlobalTimeWrappedHourly * 0.3f, gold, bright, 0.7f, 0.9f, 0.53f, 0);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return false;
            }

            float warnT = MathHelper.Clamp((age - FlyFrames) / WarnFrames, 0f, 1f);
            bool firing = Firing;
            float fade = MathHelper.Clamp(1f - (age - FlyFrames - WarnFrames - FireFrames) / FadeFrames, 0f, 1f);
            float gapCenter = GapCenter(owner);

            //钉界环:闭环金带,缺口段透明度压零(可见开口=安全门,收窄 0.74 倍=对玩家宽容)
            const int Segs = 72;
            Vector2[] pts = new Vector2[Segs + 1];
            float[] widths = new float[Segs + 1];
            float[] alphas = new float[Segs + 1];
            float bandAlpha = (firing ? 1f : 0.35f + warnT * 0.35f) * fade;
            float bandHalf = firing ? 17f : 9f + warnT * 5f;
            for (int i = 0; i <= Segs; i++) {
                float angle = i / (float)Segs * MathHelper.TwoPi;
                pts[i] = SealCenter + angle.ToRotationVector2() * SealRadius - Main.screenPosition;
                widths[i] = bandHalf;
                float delta = Math.Abs(MathHelper.WrapAngle(angle - gapCenter));
                float open = MathHelper.Clamp((delta - GapHalfAngle * 0.74f) / (GapHalfAngle * 0.26f), 0f, 1f);
                alphas[i] = bandAlpha * open;
            }

            sb.End();
            CultistOrreryRenderer.DrawTechniqueStrip("TechRing", pts, widths, alphas,
                deep, gold, bright, 1f, 0f, firing ? 0.95f : warnT * 0.4f,
                Projectile.identity % 100 * 0.067f, bandAlpha);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //缺口门框:两颗金星钉出逃生门位置(随进动同转)
            for (int side = -1; side <= 1; side += 2) {
                float edgeAngle = gapCenter + GapHalfAngle * side;
                Vector2 pos = SealCenter + edgeAngle.ToRotationVector2() * SealRadius - Main.screenPosition;
                CultistOrreryRenderer.DrawStarBead(sb, pos, gold, new Color(150, 110, 40),
                    firing ? 0.24f : 0.14f + warnT * 0.06f, (0.6f + 0.4f * warnT) * fade,
                    Main.GlobalTimeWrappedHourly * 1.6f + side);
            }
            return false;
        }
    }
}
