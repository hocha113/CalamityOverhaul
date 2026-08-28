using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖对 NPC 的介质交互：粘稠减速 + 入水/出水水花 + 水下移动血泡 + 近线尾波。
    /// 减速在所有端跑同一规则（服务器 2026-08 起持有领域镜像，见 <see cref="KikasaDomainNet"/>），
    /// 权威模拟与各端预测因此不打架；表现只落观看端。
    /// 湖面平台仍只对玩家生效（<see cref="KikasaLakeSurface"/>），NPC 是沉进水里的那一方；
    /// 被沉溺钉身的目标速度恒零，粘阻自然无感，入湖大水花由 KikasaDrownFX 自己演
    /// </summary>
    internal class KikasaLakeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==================== 活跃湖缓存 ====================

        //帧戳懒重建：每帧至多扫一次玩家槽，逐 NPC 只查短列表
        private static readonly List<KikasaDomainPlayer> activeLakes = new();
        private static uint lakesStamp = uint.MaxValue;

        internal static List<KikasaDomainPlayer> ActiveLakes {
            get {
                if (lakesStamp != Main.GameUpdateCount) {
                    lakesStamp = Main.GameUpdateCount;
                    activeLakes.Clear();
                    for (int i = 0; i < Main.maxPlayers; i++) {
                        Player player = Main.player[i];
                        if (player?.active == true
                            && player.TryGetModPlayer(out KikasaDomainPlayer domain)
                            && domain.LakeBodySolid) {
                            activeLakes.Add(domain);
                        }
                    }
                }
                return activeLakes;
            }
        }

        //==================== 逐 NPC 状态 ====================

        //上帧没入比例与水线，入水/出水边沿检测用
        private float prevSubFrac;
        private float prevLakeY;
        //血泡与尾波节流
        private int bubbleTimer;
        private int wakeTimer;

        //入水水花的帧内限量：群怪齐落只放头几朵，防齐崩连响（同 KikasaDrownFX 纪律）
        private static uint splashStamp;
        private static int splashLeft;

        private static bool TakeSplashBudget() {
            if (splashStamp != Main.GameUpdateCount) {
                splashStamp = Main.GameUpdateCount;
                splashLeft = 3;
            }
            if (splashLeft <= 0) {
                return false;
            }
            splashLeft--;
            return true;
        }

        public override void PostAI(NPC npc) {
            List<KikasaDomainPlayer> lakes = ActiveLakes;
            if (lakes.Count == 0) {
                prevSubFrac = 0f;
                return;
            }

            //取没入最深的湖；横向以施术者为中心（与湖面平台同口径）
            KikasaDomainPlayer lake = null;
            float frac = 0f;
            for (int i = 0; i < lakes.Count; i++) {
                KikasaDomainPlayer cand = lakes[i];
                if (MathF.Abs(npc.Center.X - cand.Player.Center.X) > KikasaLakeSurface.HalfWidth) {
                    continue;
                }
                float f = MathHelper.Clamp(
                    (npc.Bottom.Y - cand.LakeWorldY) / MathF.Max(npc.height, 1f), 0f, 1f);
                if (lake == null || f > frac) {
                    lake = cand;
                    frac = f;
                }
            }
            if (lake == null) {
                prevSubFrac = 0f;
                return;
            }

            if (frac > 0f && !SlowExempt(npc)) {
                ApplyViscousDrag(npc, frac);
            }

            //表现只落在把这面湖画在屏上的客户端
            if (!Main.dedServ && ReferenceEquals(KikasaDomain.Viewed, lake)) {
                SurfaceFx(npc, lake, frac);
            }

            prevSubFrac = frac;
            prevLakeY = lake.LakeWorldY;
        }

        /// <summary>boss 与蠕虫免减速（整链随头，分段拖拽会拉断链体），只吃表现</summary>
        private static bool SlowExempt(NPC npc) {
            if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) {
                return true;
            }
            if (npc.aiStyle == NPCAIStyleID.Worm
                || (npc.realLife >= 0 && npc.realLife != npc.whoAmI)) {
                return true;
            }
            return npc.type == NPCID.TargetDummy;
        }

        //稠血粘阻：按没入比例缩放，脚湿轻、全没全额

        private static void ApplyViscousDrag(NPC npc, float frac) {
            npc.velocity.X *= MathHelper.Lerp(1f, 0.92f, frac);
            if (npc.velocity.Y > 3f) {
                //缓沉：下落趋向粘稠终速
                npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, 3f, 0.2f * frac);
            }
            else if (npc.velocity.Y < 0f) {
                //跃出费力
                npc.velocity.Y *= MathHelper.Lerp(1f, 0.95f, frac);
            }
        }

        //==================== 表面表现（仅观看端） ====================

        private void SurfaceFx(NPC npc, KikasaDomainPlayer lake, float frac) {
            float lakeY = lake.LakeWorldY;
            float size = MathHelper.Clamp(npc.width / 40f, 0.5f, 2.2f);

            //入水：上帧未沾水、本帧带冲击下穿。被钉身拖入的目标速度为零，不在这触发
            if (prevSubFrac <= 0f && frac > 0.02f && npc.velocity.Y > 2.5f
                && TakeSplashBudget()) {
                float k = MathHelper.Clamp((npc.velocity.Y - 2.5f) / 10f, 0f, 1f);
                Vector2 hit = new(npc.Center.X, lakeY);
                KikasaDomainDeco.SplashAt(hit, (int)((5f + 8f * k) * MathF.Min(size, 1.6f)));
                KikasaDomainDeco.RippleAt(hit, (0.55f + 0.7f * k) * MathF.Min(size, 1.5f));
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.3f + 0.3f * k,
                    Pitch = -0.2f - 0.35f * MathHelper.Clamp(size - 0.5f, 0f, 1f),
                    MaxInstances = 3,
                }, hit);
            }
            //出水：半没入以上跃离水面
            else if (prevSubFrac >= 0.3f && frac <= 0f && npc.velocity.Y < -2f
                && TakeSplashBudget()) {
                Vector2 hit = new(npc.Center.X, prevLakeY);
                KikasaDomainDeco.SplashAt(hit, (int)(4f * MathF.Min(size, 1.4f)));
                KikasaDomainDeco.RippleAt(hit, 0.5f * MathF.Min(size, 1.4f));
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.28f,
                    Pitch = 0.05f,
                    MaxInstances = 3,
                }, hit);
            }

            //水下血泡：泡在水里动弹就冒泡，越快越密、体型越大越密
            if (frac >= 0.25f) {
                float speed = npc.velocity.Length();
                if (speed > 1.2f) {
                    int interval = (int)MathHelper.Clamp(
                        26f - speed * 2.4f - npc.width * 0.06f, 6f, 24f);
                    if (++bubbleTimer >= interval) {
                        bubbleTimer = 0;
                        float subTop = MathF.Max(npc.position.Y, lakeY);
                        Vector2 at = new(
                            npc.position.X + Main.rand.NextFloat(npc.width),
                            Main.rand.NextFloat(subTop, MathF.Max(npc.Bottom.Y, subTop + 1f)));
                        PRTLoader.NewParticle<PRT_KikasaLakeBubble>(at,
                            new Vector2(npc.velocity.X * 0.12f, -0.3f),
                            default, Main.rand.NextFloat(0.5f, 0.9f) * MathF.Min(size, 1.5f))
                            ?.Configure(Main.rand.Next(36, 70), lakeY);
                    }
                }
                else {
                    bubbleTimer = 0;
                }
            }

            //近线尾波：半没入横向游动搅出小圈（幅度压在行波登记阈值下，群怪不抢波槽）
            if (frac > 0.02f && frac < 0.95f && MathF.Abs(npc.velocity.X) > 1.5f) {
                int interval = (int)MathHelper.Clamp(17f - MathF.Abs(npc.velocity.X) * 1.2f, 8f, 15f);
                if (++wakeTimer >= interval) {
                    wakeTimer = 0;
                    KikasaDomainDeco.RippleAt(
                        new Vector2(npc.Center.X + Main.rand.NextFloat(-6f, 6f), lakeY),
                        Main.rand.NextFloat(0.18f, 0.28f));
                }
            }
            else {
                wakeTimer = 0;
            }
        }
    }
}
