using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 死亡演出（锁血 1 接管）：清场敌弹与军团 → 全身僵直 →
    /// 四工具按拍火花一嘬、链条卸劲坠地 → 头目镜乱闪、探照失控、液压降调 →
    /// 头坠地闷响 + 全场最大震屏 + 烟柱，战利品从核心喷出。
    /// 是进场"自组装"的倒放：拼起来的东西一件件还回去
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Death, typeof(ScrapStateContext))]
    internal class ScrapDeathState : ScrapStateBase
    {
        public override string StateName => "Death";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Death;

        //==================== 时序 ====================

        private const int StiffFrames = 22;
        /// <summary>第 slot 件工具熄火脱落的拍（与进场坠落同序）</summary>
        private static int DropBeat(int slot) => StiffFrames + slot * 14;
        private const int FlickerStart = 80;
        private const int HeadFallBeat = 190;
        /// <summary>头触地兜底拍（找不到地面也在此谢幕）</summary>
        private const int SlamDeadline = 262;

        private static readonly int[] DropOrder = {
            ScrapCommander.ArmLaser, ScrapCommander.ArmVice,
            ScrapCommander.ArmSaw, ScrapCommander.ArmCannon,
        };

        //==================== 本地闩 ====================

        private bool cleared;
        private readonly bool[] dropped = new bool[ScrapCommander.ArmCount];
        private readonly bool[] landed = new bool[ScrapCommander.ArmCount];
        private readonly float[] toolGroundY = new float[ScrapCommander.ArmCount];
        private bool headFalling;
        private bool slammed;
        private float headGroundY;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.dontTakeDamage = true;
            npc.damage = 0;

            if (t == 0) {
                //清场：敌弹与军团一起谢幕（公平阀 + 舞台只留主角）
                if (!cleared) {
                    cleared = true;
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 1 }, npc.Center);
                    if (!VaultUtils.isClient) {
                        ClearBattlefield(npc);
                    }
                }
            }

            //==================== 僵直段：动作全部冻住 ====================
            if (t < StiffFrames) {
                npc.velocity *= 0.85f;
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    ctx.Arms[i] = ArmDirective.HoldAt(owner.GetArmPos(i), 0.4f, 0.6f);
                }
                ctx.EyeScan = t % 6 < 3 ? 0.5f : -1f;
                Timer++;
                return null;
            }

            npc.velocity *= 0.92f;

            //==================== 工具逐件熄火坠地 ====================
            for (int slot = 0; slot < DropOrder.Length; slot++) {
                int arm = DropOrder[slot];
                if (t >= DropBeat(slot) && !dropped[arm]) {
                    dropped[arm] = true;
                    //悬空兜底：残骸最多坠一屏就停
                    toolGroundY[arm] = MathF.Min(FindGroundY(owner.GetArmPos(arm)),
                        owner.GetArmPos(arm).Y + 620f) - 14f;
                    ToolPowerDownBeat(owner, arm, slot);
                }
                if (dropped[arm]) {
                    ctx.Arms[arm] = new ArmDirective {
                        Mode = ArmMode.Fall,
                        Target = new Vector2(owner.GetArmPos(arm).X, toolGroundY[arm]),
                        UseRot = true,
                        WantRot = MathF.Sin(owner.Seed + arm * 2.13f) * 0.55f,
                        RotRate = landed[arm] ? 0.06f : 0.2f,
                    };
                    //坠地拍（位置闩）
                    if (!landed[arm] && owner.GetArmPos(arm).Y >= toolGroundY[arm] - 2f) {
                        landed[arm] = true;
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                            Volume = 0.5f,
                            Pitch = -0.5f + slot * 0.05f,
                            MaxInstances = 3
                        }, owner.GetArmPos(arm));
                        ShakeNearby(owner.GetArmPos(arm), 1.4f);
                        if (!Main.dedServ) {
                            for (int k = 0; k < 8; k++) {
                                Dust dust = Dust.NewDustPerfect(
                                    owner.GetArmPos(arm) + new Vector2(Main.rand.NextFloat(-18f, 18f), 8f),
                                    DustID.Dirt, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3.5f)),
                                    80, default, Main.rand.NextFloat(0.9f, 1.4f));
                                dust.noGravity = Main.rand.NextBool(3);
                            }
                        }
                    }
                    //坠落中链条早已卸劲：把工具透明度交回默认 1（残骸留在地上）
                }
            }

            //死亡演出全程：天色渐渐转灰
            ScrapSiegeScreen.PushGray(MathHelper.Clamp(t / 160f, 0f, 0.85f));

            //==================== 头：目镜乱闪、探照失控、液压降调 ====================
            if (t >= FlickerStart && t < HeadFallBeat) {
                //确定性乱闪：两组正弦拍出破碎节奏
                float flicker = MathF.Sin(t * 0.9f + owner.Seed) * MathF.Sin(t * 0.37f);
                ctx.EyeScan = flicker > 0.2f ? MathF.Abs(MathF.Sin(t * 0.23f)) : -1f;
                npc.rotation = MathF.Sin(t * 0.11f + owner.Seed) * 0.08f;
                //探照灯失控：光锥抽搐乱扫（确定性正弦哈希）
                if (flicker > -0.3f) {
                    float beamAng = MathHelper.PiOver2
                        + MathF.Sin(t * 0.31f + owner.Seed * 2f) * 1.3f
                        + MathF.Sin(t * 0.083f) * 0.5f;
                    Vector2 eye = npc.Center + new Vector2(0f, 8f);
                    ctx.AddSolidBeam(eye, beamAng.ToRotationVector2(), 760f,
                        0.35f + 0.25f * MathF.Abs(flicker), 0.6f);
                }
                //液压降调
                if (t % 26 == 0) {
                    SoundEngine.PlaySound(SoundID.Item13 with {
                        Volume = 0.35f,
                        Pitch = -0.2f - (t - FlickerStart) / 140f,
                        MaxInstances = 2
                    }, npc.Center);
                }
                if (!Main.dedServ && t % 7 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 2.5f)),
                        ScrapCommander.WeldOrange * 0.8f, Main.rand.NextFloat(0.4f, 0.8f))
                        ?.Configure(true, Main.rand.Next(10, 16));
                }
                if (!Main.dedServ && t % 16 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(npc.Center + new Vector2(0f, -20f),
                        new Vector2(0f, -0.5f), ScrapCommander.SmokeGray * 0.9f,
                        Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(40, 66));
                }
            }

            //==================== 头坠地谢幕 ====================
            if (t >= HeadFallBeat && !headFalling) {
                headFalling = true;
                headGroundY = MathF.Min(FindGroundY(npc.Center), npc.Center.Y + 620f)
                    - npc.height * 0.5f + 12f;
                npc.velocity = new Vector2(0f, 6f);
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.5f, Pitch = -0.85f, MaxInstances = 1 }, npc.Center);
                //提前放行 CheckDead：迟到的击杀包也能在各端正常放出死亡爆点
                ctx.DeathPerformanceFinished = true;
            }
            if (headFalling && !slammed) {
                npc.velocity.Y = MathF.Min(npc.velocity.Y + 1.6f, 32f);
                npc.rotation += 0.012f;
                if (npc.Center.Y >= headGroundY || t >= SlamDeadline) {
                    slammed = true;
                    npc.Center = new Vector2(npc.Center.X, MathF.Min(npc.Center.Y, headGroundY));
                    npc.velocity = Vector2.Zero;
                    FinalSlam(ctx, npc);
                }
            }

            Timer++;
            return null;
        }

        /// <summary>清场（服务端）：己方敌弹全灭、军团断电</summary>
        private static void ClearBattlefield(NPC npc) {
            int[] hostileTypes = {
                ModContent.ProjectileType<ScrapGroundSaw>(),
                ModContent.ProjectileType<ScrapMortarShell>(),
                ModContent.ProjectileType<ScrapLaserPulse>(),
                ModContent.ProjectileType<ScrapDebris>(),
                ModContent.ProjectileType<ScrapFlungTool>(),
                ModContent.ProjectileType<ScrapArmHitbox>(),
            };
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && Array.IndexOf(hostileTypes, p.type) >= 0) {
                    p.Kill();
                }
            }
            int probeType = ModContent.NPCType<ScrapLegionProbe>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC probe = Main.npc[i];
                if (probe.active && probe.type == probeType && (int)probe.ai[0] == npc.whoAmI) {
                    probe.StrikeInstantKill();
                }
            }
        }

        /// <summary>单件工具的熄火拍：火花一嘬 + 降调泄压，链条随即卸劲</summary>
        private static void ToolPowerDownBeat(ScrapCommander owner, int arm, int slot) {
            Vector2 pos = owner.GetArmPos(arm);
            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.4f, Pitch = -0.7f + slot * 0.08f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.32f, Pitch = -0.6f, MaxInstances = 3 }, pos);
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_Spark>(pos + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    Color.Lerp(ScrapCommander.WeldOrange, ScrapCommander.EyeRed, 0.4f),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 16));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(pos, new Vector2(0f, -0.4f),
                ScrapCommander.SmokeGray * 0.8f, 0.6f)?.Configure(40);
        }

        /// <summary>头触地：全场唯一一次打满的 impact frame，战利品在此喷出</summary>
        private void FinalSlam(ScrapStateContext ctx, NPC npc) {
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1f, Pitch = -0.7f, MaxInstances = 1 }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.8f, Pitch = -0.35f, MaxInstances = 1 }, npc.Center);
            ShakeNearby(npc.Center, 7f, 1800f);
            ScrapSiegeScreen.TriggerImpactFrame(1f);
            ScrapVfx.GroundSlam(npc.Center, 1.6f);
            ScrapVfx.MetalExplosion(npc.Center, 1.4f);
            if (!Main.dedServ) {
                //烟柱
                for (int k = 0; k < 5; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        npc.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -10f - k * 14f),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.6f, 1.3f)),
                        ScrapCommander.SmokeGray, Main.rand.NextFloat(0.9f, 1.3f))
                        ?.Configure(Main.rand.Next(70, 110));
                }
            }

            //残骸零件从核心迸出（无归属自由坠落件，纯演出伤害 0）
            if (!VaultUtils.isClient) {
                for (int k = 0; k < 5; k++) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        npc.Center + Main.rand.NextVector2Circular(20f, 12f),
                        new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(3f, 8f)),
                        ModContent.ProjectileType<ScrapDebris>(), 0, 0f, Main.myPlayer, -1f);
                }
            }

            //放行真死：战利品从核心喷出
            ctx.DeathPerformanceFinished = true;
            npc.dontTakeDamage = false;
            if (!VaultUtils.isClient) {
                npc.StrikeInstantKill();
            }
        }
    }
}
