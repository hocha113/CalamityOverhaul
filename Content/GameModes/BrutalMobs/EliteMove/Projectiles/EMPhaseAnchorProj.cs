using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles
{
    /// <summary>
    /// 相位锚：ai[0]=宿主NPC索引 ai[1]=隐没帧数 ai[2]=宿主类型。
    /// 固定时间表：淡出(26)→隐没(ai1)→凝形(42)→突刺窗(18)→消亡。
    /// 隐没与凝形全程宿主无杀伤且淡出不可选中；凝形 ≥40 帧可见成形即预告。
    /// 宿主的透明度/无害窗/定身全部由本实体逐帧盖戳（各端一致）
    /// </summary>
    internal class EMPhaseAnchorProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Age => ref Projectile.localAI[0];
        private ref float CueFlags => ref Projectile.localAI[1];
        private int HostIndex => (int)Projectile.ai[0];
        private int HiddenFrames => (int)Projectile.ai[1];

        private int CondenseStart => EliteMoveNPC.PhaseFadeFrames + HiddenFrames;
        private int FormedAge => CondenseStart + EliteMoveNPC.PhaseCondenseFrames;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        private bool TryHost(out NPC npc, out EliteMoveNPC global) {
            global = null;
            if (!HostIndex.TryGetNPC(out npc) || npc.type != (int)Projectile.ai[2]
                || !npc.TryGetGlobalNPC(out global)) {
                return false;
            }
            return true;
        }

        /// <summary>时间表上的当前可见度</summary>
        private float ScheduleAlpha() {
            if (Age < EliteMoveNPC.PhaseFadeFrames) {
                return 1f - 0.92f * (Age / EliteMoveNPC.PhaseFadeFrames);
            }
            if (Age < CondenseStart) {
                return 0.08f;
            }
            if (Age < FormedAge) {
                return 0.08f + 0.87f * ((Age - CondenseStart) / EliteMoveNPC.PhaseCondenseFrames);
            }
            return 1f;
        }

        public override void AI() {
            if (!TryHost(out NPC npc, out EliteMoveNPC global)) {
                Projectile.Kill();
                return;
            }
            if (Age == 0f) {
                Projectile.timeLeft = FormedAge + EliteMoveNPC.PhaseLungeWindow + 2;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.4f }, Projectile.Center);
                }
            }
            Age++;
            Projectile.Center = npc.Center;

            Color tint = GetTint(npc);
            if (Age < FormedAge) {
                //成形前：透明度按时间表盖戳，无害窗全程有效
                global.StampPhase(ScheduleAlpha(), harmless: true);
                if (Age >= CondenseStart) {
                    global.StampHold(0.12f, true);    //凝形定身：重现处即承诺处
                    if ((int)Age == CondenseStart && CueFlags == 0f) {
                        CueFlags = 1f;
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);
                        }
                    }
                    //凝形漩涡：外圈向内收拢（≤2粒/帧）
                    if (!VaultUtils.isServer) {
                        float t = (Age - CondenseStart) / EliteMoveNPC.PhaseCondenseFrames;
                        for (int i = 0; i < 2; i++) {
                            float ang = Age * 0.31f + i * MathHelper.Pi;
                            float radius = MathHelper.Lerp(44f, 8f, t);
                            Vector2 pos = npc.Center + ang.ToRotationVector2() * radius;
                            Dust dust = Dust.NewDustPerfect(pos, DustID.Shadowflame,
                                (npc.Center - pos) * 0.06f, 130, tint, 1f);
                            dust.noGravity = true;
                        }
                    }
                }
            }
            else {
                //成形完毕：镜像停止盖戳自动过期→恢复可见可伤；尾段只喂突刺减益窗
                global.StampLungeWindow();
                if (CueFlags < 2f) {
                    CueFlags = 2f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = 0.1f }, Projectile.Center);
                        for (int i = 0; i < 6; i++) {
                            Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(20f, 20f),
                                DustID.Shadowflame, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, tint, 1.2f);
                            dust.noGravity = true;
                        }
                    }
                }
            }
            if (Age >= CondenseStart) {
                Lighting.AddLight(npc.Center, tint.ToVector3() * 0.35f);
            }
        }

        private static Color GetTint(NPC npc)
            => EliteMoveSets.Profiles.TryGetValue(npc.type, out EliteProfile p) ? p.Tint : new Color(170, 140, 255);

        public override bool PreDraw(ref Color lightColor) {
            //凝形段：辉光聚拢做能量底衬；成形中的躯体由宿主 GlobalNPC.PreDraw 按镜像透明度绘制
            if (!TryHost(out NPC npc, out _) || Age < CondenseStart || Age >= FormedAge) {
                return false;
            }
            float t = (Age - CondenseStart) / EliteMoveNPC.PhaseCondenseFrames;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = npc.Center - Main.screenPosition;
            Color tint = GetTint(npc);
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
            //收拢的外晕：从散到聚（加色只做敷料，本体是宿主逐渐显形的原版贴图）
            Color outer = tint with { A = 0 } * (0.35f * pulse * t);
            Main.EntitySpriteDraw(glow, drawPos, null, outer, 0f, glow.Size() / 2f,
                MathHelper.Lerp(2.4f, 1.1f, t) * (npc.width / 40f + 0.6f), SpriteEffects.None, 0);
            Color core = Color.White with { A = 0 } * (0.3f * t * pulse);
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f,
                0.5f + 0.3f * t, SpriteEffects.None, 0);
            return false;
        }
    }
}
