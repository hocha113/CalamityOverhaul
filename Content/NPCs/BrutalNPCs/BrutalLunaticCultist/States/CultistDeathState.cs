using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 死亡演出：元素失控反噬（火燃→冰蚀→雷贯）→跪伏→符文离体→白闪碎阵→
    /// 四柱之兆升天→真死（走原版死亡事件触发天界柱）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Death, typeof(CultistStateContext))]
    internal class CultistDeathState : CultistStateBase
    {
        public override string StateName => "Death";
        public override CultistStateIndex StateIndex => CultistStateIndex.Death;

        private const int OrbMoment = 12;
        private const int FireBackfire = 70;
        private const int IceBackfire = 120;
        private const int ThunderBackfire = 170;
        private const int KneelStart = 200;
        private const int WhisperMoment = 228;
        private const int ShatterMoment = 258;
        private const int OmenMoment = 284;
        private const int TotalTime = 330;

        //四柱之兆的天界色
        private static readonly Color SolarOmen = new(255, 140, 60);
        private static readonly Color VortexOmen = new(0, 220, 180);
        private static readonly Color NebulaOmen = new(230, 80, 255);
        private static readonly Color StardustOmen = new(90, 160, 255);

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            npc.dontTakeDamage = true;
            npc.life = Math.Max(npc.life, 1);
            npc.velocity = Vector2.Zero;

            if (!VaultUtils.isClient) {
                CultistBossAI.ClearHostileProjectiles();
                CultistBossAI.DismissClones(context);
                CultistBossAI.CleanupMinions(includeDragons: true);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.SkipDefaultHover = true;
            npc.velocity *= 0.88f;
            context.ElementAura = MathHelper.Clamp(1.2f - Timer / 200f, 0f, 1f);

            float breakGrade = MathHelper.Clamp((Timer - KneelStart) / 60f, 0f, 1f);
            CultistScreenFX.DeclareVeil(npc.Center,
                MathHelper.Lerp(0.6f, 0.8f, MathHelper.Clamp(Timer / 200f, 0f, 1f)), context.Element, breakGrade);

            //脚下法阵：全程可见，白闪后碎裂
            context.StageSigilPos = npc.Center + new Vector2(0f, 52f);
            context.StageSigilRadius = 170f;
            context.StageSigilProgress = 1f;
            context.StageSigilBreak = Timer > ShatterMoment
                ? MathHelper.Clamp((Timer - ShatterMoment) / 40f, 0f, 1f) : 0f;

            //幕一 失控嘶吼+乱轨三球
            if (Timer <= KneelStart) {
                context.CastPose = CultistPose.Scream;
                context.CastGlow = 0.8f;
                if ((int)Timer % 14 == 0) {
                    CultistScreenFX.Punch(npc.Center, 2.4f, 10, "CultistDeathRumble");
                }
            }

            if ((int)Timer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.2f, Pitch = -0.55f }, npc.Center);
            }

            if ((int)Timer == OrbMoment && !VaultUtils.isClient) {
                for (int e = 0; e < 3; e++) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<CultistElementOrb>(), 0, 0f, Main.myPlayer, e, e, npc.whoAmI);
                }
            }

            //幕二 三相反噬：逐球炸在自己身上
            HandleBackfire(context, npc, FireBackfire, CultistElement.Fire);
            HandleBackfire(context, npc, IceBackfire, CultistElement.Ice);
            HandleBackfire(context, npc, ThunderBackfire, CultistElement.Thunder);

            //幕三 跪伏静默+符文离体
            if (Timer > KneelStart) {
                context.CastPose = CultistPose.Stand;
                context.CastGlow = 0.2f;
                if (!VaultUtils.isServer && Main.rand.NextBool(3) && Timer < ShatterMoment) {
                    //符文剥离身体飞散（汇聚的逆过程）
                    Vector2 away = npc.Center + Main.rand.NextVector2Unit() * 320f;
                    PRTLoader.NewParticle<PRT_CultistRune>(npc.Center + Main.rand.NextVector2Circular(26f, 40f),
                        Vector2.Zero, CultistPalette.Main(context.Element), Main.rand.NextFloat(0.7f, 1.2f))
                        ?.Configure(away, 0.06f, 40);
                }
            }

            if ((int)Timer == WhisperMoment && !VaultUtils.isServer) {
                CultistBossAI.LocalText(CultistBossAI.LunaticCultist_DeathText, new Color(210, 200, 255));
                CultistRenderHelper.ChantVoice(npc.Center, 0.5f, -0.7f);
            }

            //幕四 白闪碎阵
            if ((int)Timer == ShatterMoment) {
                CultistScreenFX.PushFlash(1f, 30);
                CultistScreenFX.Punch(npc.Center, 12f, 24, "CultistDeathShatter");
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                    for (int i = 0; i < 20; i++) {
                        PRTLoader.NewParticle<PRT_CultistShard>(context.StageSigilPos + Main.rand.NextVector2Circular(150f, 40f),
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 10f),
                            CultistPalette.Main(context.Element), Main.rand.NextFloat(0.8f, 1.5f))?.Configure(Main.rand.Next(28, 46));
                    }
                }
            }

            //幕五 四柱之兆：四色流光冲天（预告天界柱）
            if ((int)Timer == OmenMoment && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1f, Pitch = -0.2f }, npc.Center);
                Color[] omens = [SolarOmen, VortexOmen, NebulaOmen, StardustOmen];
                for (int i = 0; i < 4; i++) {
                    //四个斜向天角
                    float angle = -MathHelper.PiOver2 + (i - 1.5f) * 0.5f;
                    for (int k = 0; k < 8; k++) {
                        Vector2 vel = angle.ToRotationVector2() * (9f + k * 1.6f);
                        PRTLoader.NewParticle<PRT_CultistVolt>(npc.Center, vel, omens[i],
                            Main.rand.NextFloat(1f, 1.5f))?.Configure(30 + k * 2);
                    }
                }
            }

            //解体上升符尘
            if (Timer > ShatterMoment && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                CultistRenderHelper.SpawnElementMote(npc.Center + Main.rand.NextVector2Circular(24f, 42f),
                    -Vector2.UnitY * Main.rand.NextFloat(1f, 3.4f), context.Element,
                    Main.rand.NextFloat(0.6f, 1.1f), Main.rand.Next(24, 40));
            }
            npc.alpha = (int)MathHelper.Clamp((Timer - ShatterMoment) / (TotalTime - ShatterMoment - 10f) * 255f, 0f, 255f);

            //演出终：放行真死→原版死亡事件（战利品+四柱事件）
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }
            return null;
        }

        /// <summary>单相反噬拍：轨道球引爆在本体上</summary>
        private void HandleBackfire(CultistStateContext context, NPC npc, int moment, CultistElement element) {
            if ((int)Timer != moment) {
                return;
            }
            CultistScreenFX.PushFlash(0.35f, 14);
            CultistScreenFX.Punch(npc.Center, 6.5f, 14, "CultistBackfire");
            //击退小顿挫
            npc.velocity += Main.rand.NextVector2Unit() * 2.6f;

            if (!VaultUtils.isClient) {
                //收掉对应元素球（其OnKill自带爆点）
                foreach (var p in Main.ActiveProjectiles) {
                    if (p.type == ModContent.ProjectileType<CultistElementOrb>()
                        && (int)p.ai[2] == npc.whoAmI && (int)p.ai[0] == (int)element) {
                        p.Kill();
                    }
                }
            }
            if (!VaultUtils.isServer) {
                CultistRenderHelper.ElementImpact(npc.Center, element, 2.4f);
                SoundStyle s = element switch {
                    CultistElement.Fire => SoundID.Item74 with { Volume = 1f, Pitch = -0.3f },
                    CultistElement.Ice => SoundID.Item27 with { Volume = 1.1f, Pitch = -0.2f },
                    _ => SoundID.Thunder with { Volume = 1f, Pitch = 0f },
                };
                SoundEngine.PlaySound(s, npc.Center);
            }
        }

        public override void OnExit(CultistStateContext context) {
            base.OnExit(context);
            //异常切走恢复可伤
            if (!context.DeathPerformanceFinished && context.Npc != null) {
                context.Npc.dontTakeDamage = false;
            }
        }
    }
}
