using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 仪式迸发：充能满格的元素大招。40+ 帧全局预告（大印记定形+帷幕收拢）后放当前元素的仪式弹幕 ~5 秒<br/>
    /// 火·焚天螺旋：四臂螺旋，每第 5 拍全臂停一拍——径向走廊恒在<br/>
    /// 冰·霜环潮汐：三环碎晶外扩，每环 3 道 45° 裂口，环间裂口错位 40°——可读的阶梯<br/>
    /// 雷·雷格审判：立柱雷幕从一侧扫向另一侧，26 帧细弧先行——跟着波跑<br/>
    /// 结束后元素轮转、充能清零、长喘息（他也累了）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.RiteBurst, typeof(CultistStateContext))]
    internal class CultistRiteBurstState : CultistStateBase
    {
        public override string StateName => "CultistRiteBurst";
        public override CultistStateIndex StateIndex => CultistStateIndex.RiteBurst;

        private const int Windup = 64;
        private const int WaveEnd = 356;
        private const int Duration = 400;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;

            if (!VaultUtils.isClient) {
                //定场：跃至玩家上方镇场位
                npc.Center = context.Target.Center + new Vector2(0f, -300f);
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
                //迸发中心大印记：60 帧定形即全局预告
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<CultistSigilProj>(), 0, 0f, Main.myPlayer,
                    context.Element, 4f, 60f);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 13);
            npc.velocity *= 0.9f;

            Color core = CultistMotion.ElementCore(context.Element);
            context.ChantGlow = 1f;
            context.PushAura(1f, core);
            context.SigilCommit = MathHelper.Clamp(Timer / (float)Windup, 0f, 1f);
            CultistScreenFX.SetVeil(0.7f, npc.Center, core, 700f);

            if (Timer == 8 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
            }
            if (Timer == Windup) {
                CultistScreenFX.PushFlash(0.5f);
                CultistMotion.Shake(npc.Center, 7f, 14);
                CultistMotion.RuneBurst(npc.Center, core, 16, 8f);
            }

            //元素弹幕波（权威端）
            if (!VaultUtils.isClient && Timer >= Windup && Timer <= WaveEnd) {
                switch (context.Element) {
                    case 0: FireSpiral(context, (int)Timer - Windup); break;
                    case 1: IceTides(context, (int)Timer - Windup); break;
                    default: StormJudgment(context, (int)Timer - Windup); break;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                //元素轮转+充能清零：下一轮换一种法术语言
                context.Element = (context.Element + 1) % 3;
                npc.ai[1] = context.Element;
                context.RitualCharge = 0f;
                context.ChantCooldown = System.Math.Max(context.ChantCooldown, 480);
                //他也累了：长喘息（比常规连接段多 90 帧）
                return new CultistWeaveState(90);
            }
            return null;
        }

        /// <summary>火·焚天螺旋：四臂每 5 帧一拍，每第 5 拍全停——走廊拍</summary>
        private void FireSpiral(CultistStateContext context, int t) {
            if (t % 5 != 0) {
                return;
            }
            int beat = t / 5;
            if (beat % 5 == 4) {
                return;
            }
            NPC npc = context.Npc;
            int arms = context.Phase >= 2 ? 5 : 4;
            float baseAngle = beat * 0.11f;
            for (int arm = 0; arm < arms; arm++) {
                Vector2 dir = (baseAngle + MathHelper.TwoPi * arm / arms).ToRotationVector2();
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 5.3f,
                    ModContent.ProjectileType<CultistFlameBolt>(), 38, 0f, Main.myPlayer, 0f);
            }
        }

        /// <summary>冰·霜环潮汐：三环碎晶，裂口错位成阶梯</summary>
        private void IceTides(CultistStateContext context, int t) {
            int ring = t switch { 10 => 0, 100 => 1, 190 => 2, _ => -1 };
            if (ring < 0) {
                return;
            }
            NPC npc = context.Npc;
            int slots = 24;
            for (int i = 0; i < slots; i++) {
                float angle = MathHelper.TwoPi * i / slots + ring * 0.24f;
                //三道 45° 裂口，环间错位 40°
                bool inGap = false;
                for (int g = 0; g < 3; g++) {
                    float gapCenter = ring * 0.7f + MathHelper.TwoPi * g / 3f;
                    float delta = MathHelper.WrapAngle(angle - gapCenter);
                    if (System.Math.Abs(delta) < 0.39f) {
                        inGap = true;
                        break;
                    }
                }
                if (inGap) {
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, angle.ToRotationVector2() * 6.4f,
                    ModContent.ProjectileType<CultistFrostSpear>(), 40, 0f, Main.myPlayer, angle, 1f);
            }
        }

        /// <summary>雷·雷格审判：立柱雷幕左右往返横扫</summary>
        private void StormJudgment(CultistStateContext context, int t) {
            //两波：0 帧起左→右，170 帧起右→左
            int wave = t < 170 ? 0 : 1;
            int local = t - wave * 170;
            if (local % 13 != 0) {
                return;
            }
            int column = local / 13;
            int columns = context.Phase >= 2 ? 10 : 9;
            if (column >= columns) {
                return;
            }
            NPC npc = context.Npc;
            Player player = context.Target;
            float span = (columns - 1) * 175f;
            float x = player.Center.X - span * 0.5f + (wave == 0 ? column : columns - 1 - column) * 175f;
            Vector2 anchor = new(x, player.Center.Y - 430f);
            //拍点直落柱底：ArcBolt 起拍即快照，此处直接铺声明好的立柱线
            int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                ModContent.ProjectileType<CultistArcBolt>(), 52, 0f, Main.myPlayer,
                x, player.Center.Y + 260f, 0f);
            if (idx < Main.maxProjectiles) {
                Main.projectile[idx].netUpdate = true;
            }
        }
    }
}
