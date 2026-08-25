using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 仪式印记：预告即实体，描绘→定形→释放三段<br/>
    /// ai[0]=阶段(取色) ai[1]=模式(0焚焰扇 1挪移标记 2霜晶阵列 3雷律锚 4迸发中心) ai[2]=定形帧数
    /// </summary>
    internal class CultistSigilProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Element => (int)Projectile.ai[0];
        private int Mode => (int)Projectile.ai[1];
        private int ChargeTime => (int)Projectile.ai[2];
        /// <summary>已存活帧</summary>
        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>定形前 24 帧进入 commit 语调</summary>
        internal const int CommitLead = 24;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.netImportant = true;
        }

        /// <summary>模式半径</summary>
        internal float SigilRadius => Mode switch {
            1 => 64f,
            2 => 96f,
            3 => 88f,
            4 => 300f,
            _ => 86f,
        };

        public override void AI() {
            Timer++;

            //出生音：描绘起笔
            if (Timer == 1f && !VaultUtils.isServer) {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.35f, Pitch = -0.2f }, Projectile.Center);
            }

            Color core = CultistMotion.PhaseCore(Element);
            Lighting.AddLight(Projectile.Center, core.ToVector3() * 0.5f * DrawProgress);

            //定形顿音（各非服务端本地演出）
            if (ChargeTime > 0 && Timer == ChargeTime - CommitLead) {
                CultistMotion.SigilCommitFX(Projectile.Center, core, Mode == 4 ? 1.6f : 1f);
            }

            //释放
            if (ChargeTime > 0 && Timer == ChargeTime) {
                Fire();
            }

            //迸发中心与挪移标记：纯视觉，靠 timeLeft 自灭
            if (Mode is 1 or 4) {
                return;
            }

            //攻击印记在释放后短暂余辉即灭
            if (ChargeTime > 0 && Timer > ChargeTime + 16) {
                Projectile.Kill();
            }
        }

        /// <summary>描绘进度 0~1</summary>
        private float DrawProgress => ChargeTime <= 0
            ? MathHelper.Clamp(Timer / 30f, 0f, 1f)
            : MathHelper.Clamp(Timer / MathF.Max(ChargeTime - CommitLead, 1f), 0f, 1f);

        /// <summary>定形迸发 0~1</summary>
        private float CommitGlow {
            get {
                if (ChargeTime <= 0) {
                    return 0f;
                }
                if (Timer < ChargeTime - CommitLead) {
                    return 0f;
                }
                return MathHelper.Clamp((Timer - (ChargeTime - CommitLead)) / CommitLead, 0f, 1f);
            }
        }

        /// <summary>释放：按模式出招，权威端裁决</summary>
        private void Fire() {
            //释放闪与震（各端本地）
            CultistMotion.CastFlash(Projectile.Center, CultistMotion.PhaseCore(Element), Mode == 4 ? 1.5f : 1f);
            CultistMotion.Shake(Projectile.Center, 3f, 8);

            if (VaultUtils.isClient) {
                return;
            }

            Player target = FindNearestPlayer();
            if (target == null) {
                return;
            }

            switch (Mode) {
                case 0: {
                    //焚焰扇：朝玩家当前位置 ±35°，慢启动增压（公平阀：出膛慢）
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = -2; i <= 2; i++) {
                        Vector2 vel = dir.RotatedBy(i * 0.305f) * 6.4f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                            ModContent.ProjectileType<CultistFlameBolt>(), 38, 0f, Main.myPlayer, i == 0 ? 1f : 0f);
                    }
                    break;
                }
                case 2: {
                    //霜晶阵列：3 条放射晶枪列，列间保底 50° 空档（公平阀：走位走廊恒在）
                    float baseAngle = (target.Center - Projectile.Center).ToRotation();
                    for (int ray = -1; ray <= 1; ray++) {
                        float angle = baseAngle + ray * 0.87f;
                        for (int i = 0; i < 5; i++) {
                            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * (110f + i * 96f);
                            //逐节延迟生长由 ai[2] 传递
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, Vector2.Zero,
                                ModContent.ProjectileType<CultistFrostSpear>(), 45, 0f, Main.myPlayer,
                                angle, 0f, i * 5f);
                        }
                    }
                    break;
                }
                case 3: {
                    //雷律三拍：拍点在各 ArcBolt 出生时快照，拍间隔递缩（44→38）
                    for (int beat = 0; beat < 3; beat++) {
                        int delay = beat switch { 0 => 0, 1 => 44, _ => 82 };
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                            ModContent.ProjectileType<CultistArcBolt>(), 52, 0f, Main.myPlayer,
                            0f, 0f, delay);
                    }
                    break;
                }
            }
        }

        private Player FindNearestPlayer() {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float dist = player.DistanceSQ(Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = player;
                }
            }
            return best;
        }

        public override bool PreDraw(ref Color lightColor) {
            float alpha = 1f;
            //攻击印记释放后余辉衰减
            if (ChargeTime > 0 && Mode is not 1 and not 4 && Timer > ChargeTime) {
                alpha = 1f - (Timer - ChargeTime) / 16f;
            }
            //生命尾段渐隐
            if (Projectile.timeLeft < 20) {
                alpha = MathF.Min(alpha, Projectile.timeLeft / 20f);
            }

            CultistRenderHelper.DrawSigil(Main.spriteBatch, Projectile.Center, SigilRadius,
                CultistMotion.PhaseCore(Element), DrawProgress, CommitGlow, 0f, alpha);
            return false;
        }
    }
}
