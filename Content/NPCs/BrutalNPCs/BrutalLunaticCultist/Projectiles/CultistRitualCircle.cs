using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 大仪式法阵：进度读本体同步槽；带指向真身的导流丝（可学习破绽）；
    /// ai[0]=本体whoAmI ai[1]=崩碎标记(状态置1后播放碎裂并消亡)
    /// </summary>
    internal class CultistRitualCircle : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int GrowTime = 60;
        internal const int EndFadeTime = 40;
        private const int BreakTime = 34;

        private float spin;
        private float breakTimer;

        private NPC Boss {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC boss = Main.npc[idx];
                return boss.active && boss.type == NPCID.CultistBoss ? boss : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 120;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;
        }

        private float GrowProgress => MathHelper.Clamp(Projectile.localAI[0] / GrowTime, 0f, 1f);

        public override void AI() {
            NPC boss = Boss;
            if (boss == null) {
                Projectile.Kill();
                return;
            }

            Projectile.localAI[0]++;
            spin += 0.012f + RitualProgress(boss) * 0.05f;

            if (Projectile.localAI[0] == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item123 with { Volume = 1f }, Projectile.Center);
            }

            //崩碎流程
            if (Projectile.ai[1] >= 1f) {
                breakTimer++;
                if (breakTimer == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1.1f, Pitch = -0.3f }, Projectile.Center);
                    CultistBossAI.LocalText(CultistBossAI.LunaticCultist_RitualCollapseText, CultistPalette.Bright(ElementOf(boss)));
                    var element = ElementOf(boss);
                    for (int i = 0; i < 26; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 12f);
                        PRTLoader.NewParticle<PRT_CultistShard>(Projectile.Center + Main.rand.NextVector2Circular(220f, 90f),
                            vel, CultistPalette.Main(element), Main.rand.NextFloat(0.8f, 1.6f))?.Configure(Main.rand.Next(26, 44));
                    }
                }
                if (breakTimer >= BreakTime) {
                    Projectile.Kill();
                }
                return;
            }

            //吟唱汇聚，密度∝√进度，72%后静默（尖啸前的吸气）
            float progress = RitualProgress(boss);
            if (!VaultUtils.isServer && progress < 0.72f) {
                float density = (float)Math.Sqrt(Math.Max(progress, 0.05f)) * 1.4f;
                CultistRenderHelper.ConvergeRunes(Projectile.Center, 480f, ElementOf(boss), density);
                CultistRenderHelper.ConvergeRunes(Projectile.Center, 480f, ElementOf(boss), density);
            }

            //圆满收束演出：各端由同步进度自行触发（服务端只做召唤裁决）
            if (Projectile.localAI[1] == 0f && progress >= 0.995f) {
                Projectile.localAI[1] = 1f;
                CultistScreenFX.PushFlash(0.9f, 28);
                CultistScreenFX.Punch(Projectile.Center, 11f, 20, "CultistRitualDone");
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.2f, Pitch = -0.4f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = -0.2f }, Projectile.Center);
                    CultistRenderHelper.ElementImpact(Projectile.Center, ElementOf(boss), 2.6f);
                }
            }

            //周期吟唱声，随进度加速升调
            if (!VaultUtils.isServer) {
                int chantInterval = (int)MathHelper.Lerp(52f, 20f, progress);
                if ((int)Projectile.localAI[0] % Math.Max(chantInterval, 12) == 0) {
                    CultistRenderHelper.ChantVoice(Projectile.Center, 0.7f, MathHelper.Lerp(-0.25f, 0.3f, progress));
                }
            }

            Lighting.AddLight(Projectile.Center, CultistPalette.Main(ElementOf(boss)).ToVector3() * (0.8f + progress));
        }

        private static float RitualProgress(NPC boss) {
            var ctx = boss.GetOverride<CultistBossAI>()?.Context;
            return ctx?.RitualProgress ?? 0f;
        }

        private static CultistElement ElementOf(NPC boss) {
            var ctx = boss.GetOverride<CultistBossAI>()?.Context;
            return ctx?.Element ?? CultistElement.Fire;
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC boss = Boss;
            if (boss == null) {
                return false;
            }
            CultistElement element = ElementOf(boss);
            float progress = RitualProgress(boss);
            float breakGrade = Projectile.ai[1] >= 1f ? breakTimer / (float)BreakTime : 0f;
            //圆满收场的尾段淡出
            float endFade = Projectile.timeLeft < EndFadeTime ? Projectile.timeLeft / (float)EndFadeTime : 1f;
            float alpha = (1f - breakGrade) * endFade;

            //主法阵
            CultistRenderHelper.DrawSigil(Main.spriteBatch, Projectile.Center, 340f, element,
                GrowProgress * (0.4f + progress * 0.6f), spin, progress > 0.95f ? (progress - 0.95f) * 20f : 0f,
                breakGrade, alpha);

            //导流丝：法阵→真身的细光线（仪式期真身破绽）
            if (breakGrade <= 0f && GrowProgress > 0.6f) {
                SpriteBatch sb = Main.spriteBatch;
                CultistRenderHelper.BeginAdditive(sb);
                Texture2D line = CWRAsset.LightShot.Value;
                Vector2 toBoss = boss.Center - Projectile.Center;
                float len = toBoss.Length();
                float rot = toBoss.ToRotation();
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f);
                Color filament = CultistPalette.Bright(element) * (0.16f + 0.1f * pulse)
                    * MathHelper.Clamp(progress * 2f, 0.2f, 1f) * endFade;
                sb.Draw(line, Projectile.Center - Main.screenPosition, null, filament, rot,
                    new Vector2(0f, line.Height / 2f), new Vector2(len / line.Width, 0.16f), SpriteEffects.None, 0f);
                CultistRenderHelper.EndAdditive(sb);
            }
            return false;
        }
    }
}
