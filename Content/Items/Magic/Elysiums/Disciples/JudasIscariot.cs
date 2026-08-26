using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 犹大·背叛(席位11)：在职时给予主人全面的增益(接线在 <see cref="ElysiumPlayer.UpdateEquips"/>)。
    /// 十二圣位齐聚且主人濒危时发动背叛：疾刺穿身，三十银币散落。
    /// ai[1]=2 为背叛状态(主人端写入同步)，演出结束后犹大离去且席位空置
    /// </summary>
    internal class JudasIscariot : BaseDisciple
    {
        public override int Seat => 11;

        private const int BetrayTime = 38;
        private const int StabFrame = 18;

        private int betrayTimer = -1;
        private Vector2 dashFrom;
        private Vector2 dashTo;

        /// <summary>背叛启动(主人端调用，状态经ai同步)</summary>
        public void BeginBetrayal() {
            if (betrayTimer >= 0 || IsMartyring) {
                return;
            }
            Projectile.ai[1] = 2f;
            Projectile.netUpdate = true;
        }

        public override void AI() {
            //背叛状态优先于常规行为
            if (Projectile.ai[1] == 2f) {
                if (betrayTimer < 0) {
                    betrayTimer = 0;
                    SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 1.3f, Pitch = -0.5f }, Owner.Center);
                    //收拢起点与穿刺终点：从当前位越过主人身后
                    dashFrom = Projectile.Center;
                    Vector2 through = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    dashTo = Owner.Center + through * 180f;
                }
                BetrayBehavior();
                return;
            }

            base.AI();
            if (IsMartyring) {
                return;
            }

            //预兆：主人濒危且圣位齐聚时，犹大的银币在暗处闪烁
            if (!Main.dedServ && Owner.statLife < Owner.statLifeMax2 * 0.45f
                && Owner.TryGetModPlayer(out ElysiumPlayer ep) && ep.AliveDiscipleCount >= ElysiumPlayer.SeatCount
                && Main.rand.NextBool(16)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 26f)
                    , new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f))
                    , new Color(200, 200, 214), Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        /// <summary>背叛演出：收拢→疾刺穿身(银币迸散)→远遁消散</summary>
        private void BetrayBehavior() {
            betrayTimer++;
            Projectile.timeLeft = 120;

            if (betrayTimer < StabFrame) {
                //收拢蓄势：压向主人一侧，末3帧死寂
                float t = betrayTimer / (float)StabFrame;
                Vector2 windup = Vector2.Lerp(dashFrom, Owner.Center - (dashTo - Owner.Center) * 0.6f
                    , VaultUtils.EaseOutCubic(Math.Min(t * 1.25f, 1f)));
                Projectile.Center = windup;
            }
            else if (betrayTimer == StabFrame) {
                //穿刺瞬间：一帧掠过主人，银币迸散
                Projectile.Center = dashTo;
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.1f, Pitch = -0.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 1.2f, Pitch = -0.4f }, Owner.Center);
                if (!Main.dedServ) {
                    //三十银币散落
                    for (int i = 0; i < 30; i++) {
                        Vector2 vel = VaultUtils.RandVr(2.5f, 8f) - new Vector2(0f, 2f);
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Owner.Center, vel
                            , new Color(205, 205, 218), Main.rand.NextFloat(0.4f, 0.8f))?.Configure(true, Main.rand.Next(24, 40));
                    }
                    //暗红裂闪
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Light>(Owner.Center, VaultUtils.RandVr(3f, 7f)
                            , new Color(140, 40, 40), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 20), 0.9f);
                    }
                }
                Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 6f);
            }
            else {
                //远遁消散
                Projectile.Center += (dashTo - Owner.Center).SafeNormalize(Vector2.UnitX) * 7f;
                if (!Main.dedServ && betrayTimer % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero
                        , new Color(120, 60, 60), 0.24f)?.Configure(12, 0.7f);
                }
                if (betrayTimer >= BetrayTime) {
                    Projectile.Kill();
                }
            }
        }
    }
}
