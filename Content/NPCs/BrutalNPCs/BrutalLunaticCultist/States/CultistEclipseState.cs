using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 蚀祭:暗影盘滑向主星(食相自身即 62 帧预告),全食后先过宽限期再放冕矛;<br/>
    /// 本影楔从玩家所在角起步(先给安全区)随食相缓动渐显,宽限期慢漂,齐射期渐加速;司祭跪祷不出手<br/>
    /// 分相后手(变体主体在 UmbraShade):星尘相冕矛解禁后召幻影龙两段直线冲撞;月明相首尾各压一轮追星矢连射<br/>
    /// Timeout 必须盖过 UmbraShade 全寿命(760+出生延迟 12;月明相再加 MoonExtend),否则状态提前回 Coil 开星球火闸=双重压力
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Eclipse, typeof(CultistStateContext))]
    internal class CultistEclipseState : CultistStateBase
    {
        public override string StateName => "CultistEclipse";
        public override CultistStateIndex StateIndex => CultistStateIndex.Eclipse;

        private const int Timeout = 790;
        /// <summary>月明相首轮追星矢拍("技能刚开始"的连射)</summary>
        private const int MoonEarlyVolleyBeat = 26;
        /// <summary>月明相齐射星数(与追星矢态月明规格同)</summary>
        private const int MoonVolleyCount = 6;
        /// <summary>星尘相召龙拍:冕矛解禁(本影龄 170)后一拍,龙自带两段预瞄+冲撞</summary>
        private const int DragonBeat = 204;

        /// <summary>没抓到常驻主星时直接放弃(权威端置位)</summary>
        private bool aborted;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            aborted = false;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);
            context.PushAura(0.85f, CultistMotion.PhaseCore(context.Phase));
            context.BodyHot = MathHelper.Max(context.BodyHot, MathHelper.Clamp((Timer - 62f) / 60f, 0f, 0.7f));

            Vector2 hover = context.ArenaCenter + new Vector2(0f, -440f)
                + CultistMotion.BreathingOffset(seed: 9.2f, 8f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            //起蚀(权威端):本影基角=当下玩家方位(先给安全区),漂移方向随机签名
            if (Timer == 12 && !VaultUtils.isClient) {
                Projectile planet = FindPlanet(npc.whoAmI);
                if (planet == null) {
                    aborted = true;
                }
                else {
                    float umbraBase = (player.Center - planet.Center).ToRotation();
                    //日耀相本影旋速降 30%:流星压力换旋转宽容(率随 ai[2] 同步,楔的纯函数自动跟)
                    float driftMag = 0.0045f * (context.Phase == 3 ? 0.7f : 1f);
                    float drift = (Main.rand.NextBool() ? 1f : -1f) * driftMag;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), planet.Center, Vector2.Zero,
                        ModContent.ProjectileType<CultistUmbraShade>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, umbraBase, drift);
                    npc.netUpdate = true;
                }
            }
            if (Timer == 12 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
            }

            //月明相:技能刚开始与快结束各压一轮追星矢(既有连射弹,矢自带预瞄线与错拍)
            bool moonVolleyBeat = context.Phase >= 4
                && (Timer == MoonEarlyVolleyBeat || Timer == 12 + CultistUmbraShade.MoonLateVolleyAge);
            if (moonVolleyBeat) {
                CultistMotion.CastFlash(npc.Center + new Vector2(0f, -80f),
                    CultistMotion.PhaseCore(context.Phase), 1.2f);
                context.ScalePulse = 1.10f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    for (int slot = 0; slot < MoonVolleyCount; slot++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, -60f),
                            Vector2.Zero, ModContent.ProjectileType<CultistSeekerStar>(), 40, 0f,
                            Main.myPlayer, npc.whoAmI, slot);
                    }
                    npc.netUpdate = true;
                }
            }

            //星尘相:自本影中唤出幻影龙,两段直线冲撞后消散(龙自带预瞄线;此期主星公转冻结,见星球)
            if (context.Phase == 2 && Timer == DragonBeat) {
                CultistMotion.SigilCommitFX(npc.Center, CultistMotion.StardustCore, 1.3f);
                context.ScalePulse = 1.12f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = 0.35f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 spawnPos = player.Center + new Vector2(side * 680f, -140f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero,
                        ModContent.ProjectileType<CultistEclipseDragon>(), 48, 0f, Main.myPlayer,
                        npc.whoAmI, side);
                    npc.netUpdate = true;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }

            if (aborted) {
                return new CultistCoilState();
            }
            if (Timer > 48 && !AnyShadeAlive(npc.whoAmI)) {
                return new CultistCoilState(14);
            }
            //月明相全食延长,超时同步外扩才不至半途开星球火闸
            if (Timer >= Timeout + (context.Phase >= 4 ? CultistUmbraShade.MoonExtend : 0)) {
                return new CultistCoilState(14);
            }
            return null;
        }

        /// <summary>找常驻非幻象主星</summary>
        internal static Projectile FindPlanet(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] % 10 == 1 && (int)proj.ai[2] / 10 == 0) {
                    return proj;
                }
            }
            return null;
        }

        private static bool AnyShadeAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistUmbraShade>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho) {
                    return true;
                }
            }
            return false;
        }
    }
}
