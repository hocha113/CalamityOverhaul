using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Abilities.GhostRains
{
    /// <summary>
    /// 鬼雨的本地演出宿主：雨滴/潮雾/脸痕的生成节拍、相位音效与雨喉拽入表现。<br/>
    /// 纯表现层，不保存、不参与判定；权威状态在 <see cref="GhostRainProj"/>。
    /// </summary>
    internal static class GhostRainFx
    {
        //尸雨调色：灰白为主，偶见尸斑青
        private static readonly Color RainPale = new(170, 185, 190);
        private static readonly Color RainCorpse = new(140, 170, 165);
        private static readonly Color MistDamp = new(58, 66, 70);

        private sealed class RainFxState
        {
            public uint Revision;
            //已消费的最大节拍帧：网络快照回卷时不重放音效
            public int LastBeat;
            public float DropCarry;
        }

        //按玩家槽位存放的表现簿记，客户端专用
        private static readonly Dictionary<int, RainFxState> fxByPlayer = [];

        internal static void Clear() => fxByPlayer.Clear();

        /// <summary>清掉已结束或玩家失效的簿记，由环境系统每帧调用。</summary>
        internal static void Sweep() {
            if (Main.gameMenu) {
                fxByPlayer.Clear();
                return;
            }
            if (fxByPlayer.Count == 0) {
                return;
            }
            //近两帧无任何雨幕盖戳：不必按簿记逐个 Find 全弹幕表。簿记留待
            //下一场雨出现时再清（或 ClearWorld），残留只是几条字典项，无玩法影响
            if (!GhostRainProj.PresenceStamp.ActiveWithin()) {
                return;
            }
            List<int> stale = null;
            foreach (int who in fxByPlayer.Keys) {
                if (GhostRainProj.Find(who) == null) {
                    (stale ??= []).Add(who);
                }
            }
            if (stale != null) {
                foreach (int who in stale) {
                    fxByPlayer.Remove(who);
                }
            }
        }

        /// <summary>由雨幕控制器逐帧调用（仅图形端）：音效节拍与粒子。</summary>
        internal static void OnControllerTick(Player owner, GhostRainProj rain) {
            if (Main.dedServ || owner == null || rain == null) {
                return;
            }
            RainFxState fx = EnsureFx(owner, rain);
            int t = rain.StormAge;
            bool freshBeat = t > fx.LastBeat;
            fx.LastBeat = Math.Max(fx.LastBeat, t);

            if (freshBeat && !rain.Fading) {
                PlayBeatCues(owner, t);
            }

            float density = GhostRainStorm.RainDensity(t, rain.Paid);
            if (rain.Fading) {
                //散场随 Presence 排干，避免收刀瞬间雨幕硬切
                density *= MathHelper.Clamp(rain.Presence / 0.7f, 0f, 1f);
            }

            if (density > 0.01f) {
                SpawnRainBand(owner, fx, density, rain.StormSeed);
            }
            if (density > 0.2f && t % 4 == 0) {
                SpawnMist(owner);
                if (density > 0.55f) {
                    SpawnMist(owner);
                }
            }
            //稳态雨幕偶发脸痕，仍宁少勿滥
            if (rain.Paid && !rain.Fading && t > GhostRainStorm.RainfallEnd
                && Main.rand.NextBool(55)) {
                SpawnFaceStreak(owner);
            }
            //雨里的活物挂水珠
            if (density > 0.35f && t % 4 == 0) {
                SpawnWetNpcDrips(owner);
            }
        }

        private static RainFxState EnsureFx(Player owner, GhostRainProj rain) {
            if (!fxByPlayer.TryGetValue(owner.whoAmI, out RainFxState fx)
                || fx.Revision != rain.PresenceRevision) {
                //同一弹体身份跃迁（入雨/散场）保留 LastBeat，避免闷雷重播；换弹体重置
                bool sameIdentity = fx != null
                    && (fx.Revision >> 2) == (rain.PresenceRevision >> 2);
                fx = new RainFxState {
                    Revision = rain.PresenceRevision,
                    LastBeat = sameIdentity ? fx.LastBeat : 0,
                    DropCarry = sameIdentity ? fx.DropCarry : 0f,
                };
                fxByPlayer[owner.whoAmI] = fx;
            }
            return fx;
        }

        /// <summary>主雨：优先铺满屏幕，再与域水平范围取交；密度按可视像素宽度配额。</summary>
        private static void SpawnRainBand(Player owner, RainFxState fx, float density, byte seed) {
            //屏幕外缘再扩一截，避免边缘露馅；域只裁掉真正域外的部分
            float left = Math.Max(owner.Center.X - GhostRainStorm.Radius,
                Main.screenPosition.X - 160f);
            float right = Math.Min(owner.Center.X + GhostRainStorm.Radius,
                Main.screenPosition.X + Main.screenWidth + 160f);
            if (right <= left) {
                return;
            }

            //约 0.02 滴/像素宽/帧 @密度1 → 1920 宽约 38 滴/帧，铺满竖帘
            fx.DropCarry += density * 0.02f * (right - left);
            int count = Math.Min((int)fx.DropCarry, 56);
            fx.DropCarry -= count;
            if (count <= 0) {
                return;
            }

            //风向按种子定相，整场稳定
            float wind = MathF.Sin(seed * 0.37f) * 2.2f * density;
            for (int i = 0; i < count; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.screenPosition.Y - Main.rand.NextFloat(10f, 220f));
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11f, 17f));
                Color color = (Main.rand.NextBool(7) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.42f, 0.65f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel, color,
                    Main.rand.NextFloat(0.8f, 1.25f))
                    ?.Configure(Main.rand.Next(70, 110), vel.X);
            }
        }

        /// <summary>贴地潮雾：探到地面才生，雨越大越沉；贴图用 Masking/Fog。</summary>
        private static void SpawnMist(Player owner) {
            float span = Math.Min(GhostRainStorm.Radius * 0.95f, Main.screenWidth * 0.55f + 200f);
            float x = owner.Center.X + Main.rand.NextFloat(-span, span);
            //只在屏幕附近生雾，远处浪费配额
            if (x < Main.screenPosition.X - 120f
                || x > Main.screenPosition.X + Main.screenWidth + 120f) {
                return;
            }
            if (!TryFindGround(x, owner.Center.Y - 60f, out float groundY)) {
                return;
            }
            Vector2 pos = new(x, groundY - Main.rand.NextFloat(6f, 40f));
            Vector2 vel = new(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(-0.08f, 0f));
            PRTLoader.NewParticle<PRT_GhostRainMist>(pos, vel,
                MistDamp * Main.rand.NextFloat(0.75f, 1f),
                Main.rand.NextFloat(0.7f, 1.25f))
                ?.Configure(Main.rand.Next(90, 160));
        }

        /// <summary>雨幕稀有的脸痕竖丝。</summary>
        private static void SpawnFaceStreak(Player owner) {
            float left = Math.Max(owner.Center.X - GhostRainStorm.Radius * 0.8f,
                Main.screenPosition.X + 60f);
            float right = Math.Min(owner.Center.X + GhostRainStorm.Radius * 0.8f,
                Main.screenPosition.X + Main.screenWidth - 60f);
            if (right <= left) {
                return;
            }
            Vector2 pos = new(Main.rand.NextFloat(left, right),
                Main.screenPosition.Y + Main.rand.NextFloat(60f, 280f));
            PRTLoader.NewParticle<PRT_GhostRainFaceStreak>(pos,
                new Vector2(0f, Main.rand.NextFloat(1.6f, 2.4f)),
                RainPale * 0.5f, Main.rand.NextFloat(0.85f, 1.15f))
                ?.Configure(Main.rand.Next(50, 74));
        }

        /// <summary>域内活物身上偶发挂珠，雨是落在东西上的。</summary>
        private static void SpawnWetNpcDrips(Player owner) {
            int budget = 6;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (budget <= 0) {
                    break;
                }
                if (npc.friendly || npc.dontTakeDamage
                    || Vector2.DistanceSquared(npc.Center, owner.Center)
                        > GhostRainStorm.Radius * GhostRainStorm.Radius
                    || !Main.rand.NextBool(4)) {
                    continue;
                }
                budget--;
                Vector2 pos = npc.Center + new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f) * npc.width,
                    Main.rand.NextFloat(-0.4f, 0.1f) * npc.height);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                    new Vector2(npc.velocity.X * 0.3f, Main.rand.NextFloat(1.5f, 3f)),
                    RainPale * 0.4f, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24), 0f);
            }
        }

        /// <summary>雨喉拽入表现：目标上方雨丝收束成漏斗 + 上抽水花。</summary>
        internal static void TriggerYank(Vector2 target, Vector2 throat) {
            if (Main.dedServ) {
                return;
            }
            //漏斗收束丝：从目标四周向喉点收拢
            for (int i = 0; i < 14; i++) {
                float angle = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 pos = target + angle.ToRotationVector2()
                    * Main.rand.NextFloat(26f, 70f);
                Vector2 vel = (throat - pos).SafeNormalize(-Vector2.UnitY)
                    * Main.rand.NextFloat(3f, 6f);
                PRTLoader.NewParticle<PRT_GhostRainYank>(pos, vel,
                    RainPale * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.8f, 1.1f))
                    ?.Configure(throat, Main.rand.Next(18, 28));
            }
            //上抽的碎珠
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    target + Main.rand.NextVector2Circular(20f, 24f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-7f, -3.5f)),
                    RainPale * 0.5f, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 26), 0f);
            }

            //短促"布被扯紧"的闷吸声
            SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                Pitch = -0.75f,
                Volume = 0.4f,
                MaxInstances = 3,
            }, target);
            if (Main.LocalPlayer?.active == true
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, target) < 1200f * 1200f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2f);
            }
        }

        private static void PlayBeatCues(Player owner, int t) {
            //阴叠起：远处一声闷雷，不带闪电
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = -0.85f,
                    Volume = 0.38f,
                    MaxInstances = 3,
                }, owner.Center);
            }
            //入雨：一记压低的闷锣
            else if (t == GhostRainStorm.CommitFrame + 1) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Pitch = -0.7f,
                    Volume = 0.45f,
                    MaxInstances = 3,
                }, owner.Center);
            }
            //雨幕爬满：更沉的第二声闷雷
            else if (t == GhostRainStorm.RainfallEnd + 1) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = -0.95f,
                    Volume = 0.3f,
                    MaxInstances = 3,
                }, owner.Center);
            }
        }

        /// <summary>从起始高度向下探地表，探不到就不生潮雾。</summary>
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 46; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }
    }
}
