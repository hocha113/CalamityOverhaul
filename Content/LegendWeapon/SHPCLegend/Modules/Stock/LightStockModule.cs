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
    /// <summary>轻量枪托：主光束拖 2 节轨迹光链，各 40% 独立伤害</summary>
    internal sealed class LightStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //轻量银白
        public override Color TintColor => new(220, 230, 240);

        /// <summary>已挂载子节的光束，OnBeamKill 清理</summary>
        private readonly HashSet<int> linkedBeams = [];

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.2f;
            ctx.DamageMul += -0.15f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if (!linkedBeams.Add(beam.Projectile.whoAmI)) return;

            //为新生光束挂载 2 节链节：滞后 12 / 24 帧位
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.4f), 1);
            for (int i = 1; i <= 2; i++) {
                Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                    beam.Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<SHPCBeamSegmentProj>(),
                    dmg, beam.Projectile.knockBack * 0.5f, beam.Projectile.owner,
                    ai0: beam.Projectile.whoAmI,
                    ai1: i * 12,
                    ai2: beam.Projectile.ai[0]); //继承主题色索引
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            linkedBeams.Remove(beam.Projectile.whoAmI);
        }
    }

    /// <summary>光链子节：沿父束 oldPos 滞后爬行，父束消亡后惯性散链</summary>
    internal sealed class SHPCBeamSegmentProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //三种主题近似色（蓝/黄/青），与 CyberTraceBeamProj 的主题索引对应
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

        private int ParentIndex => (int)Projectile.ai[0];
        private int LagFrames => (int)Projectile.ai[1];
        private int ThemeIndex => Math.Clamp((int)Projectile.ai[2], 0, ThemeMain.Length - 1);

        private float fadeAlpha;
        private bool orphaned;

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
            Projectile.localNPCHitCooldown = 16;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => orphaned;

        public override void AI() {
            Projectile parent = ParentIndex >= 0 && ParentIndex < Main.maxProjectiles
                ? Main.projectile[ParentIndex] : null;
            bool parentValid = parent != null && parent.active
                && parent.owner == Projectile.owner
                && parent.ModProjectile is CyberTraceBeamProj;

            if (parentValid) {
                //从父束轨迹缓存取滞后位置；缓存未填充时退回父束当前位置
                int lag = Math.Clamp(LagFrames, 0, parent.oldPos.Length - 1);
                Vector2 raw = parent.oldPos[lag];
                Vector2 targetPos = raw == Vector2.Zero ? parent.Center : raw + parent.Size * 0.5f;
                Vector2 delta = targetPos - Projectile.Center;
                Projectile.Center = targetPos;
                if (delta.LengthSquared() > 0.01f) {
                    Projectile.rotation = delta.ToRotation();
                    Projectile.velocity = delta * 0.4f; //仅作击退方向参考
                }
                fadeAlpha = MathF.Min(fadeAlpha + 0.1f, 1f);
            }
            else {
                //父束消亡：惯性散链 18 帧后熄灭
                if (!orphaned) {
                    orphaned = true;
                    Projectile.timeLeft = 18;
                    Projectile.velocity = Projectile.rotation.ToRotationVector2() * 7f;
                }
                Projectile.velocity *= 0.93f;
                fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            }

            Lighting.AddLight(Projectile.Center, ThemeMain[ThemeIndex].ToVector3() * 0.35f * fadeAlpha);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    ThemeMain[ThemeIndex], Main.rand.NextFloat(0.3f, 0.6f))
                    .Configure(ThemeEdge[ThemeIndex], Main.rand.Next(8, 16));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.25f, Pitch = 0.6f }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center,
                    Main.rand.NextVector2CircularEdge(3f, 3f),
                    ThemeMain[ThemeIndex], Main.rand.NextFloat(0.4f, 0.8f))
                    .Configure(ThemeEdge[ThemeIndex], Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) return;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color main = ThemeMain[ThemeIndex];
            Color edge = ThemeEdge[ThemeIndex];
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f + LagFrames);

            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, edge * fadeAlpha * 0.6f * pulse, 0f,
                    glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, main * fadeAlpha * 0.8f * pulse, 0f,
                    glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0f);
            }
            if (white != null) {
                //节体：沿行进方向拉长的胶囊状光块，越靠后的节越小
                float bodyLen = LagFrames > 12 ? 16f : 20f;
                spriteBatch.Draw(white, drawPos, null, main * fadeAlpha * 0.95f,
                    Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(bodyLen, 7f), SpriteEffects.None, 0f);
                spriteBatch.Draw(white, drawPos, null, Color.White * fadeAlpha * 0.85f,
                    Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(bodyLen * 0.55f, 3.5f), SpriteEffects.None, 0f);
            }
        }
    }
}
