using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles
{
    /// <summary>
    /// 跃击落点印记：ai[0]=宿主NPC索引 ai[1]=0 跟踪 / 1 已锁定 ai[2]=宿主类型。
    /// 跟踪 20 帧随玩家移动，锁定后冻结（预告即承诺），随后宿主按印记位置起跳。
    /// 本体无伤害；跃击的杀伤是宿主自身接触，减益窗口由本实体盖戳
    /// </summary>
    internal class EMLeapMarkerProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Age => ref Projectile.localAI[0];
        private ref float LockPopped => ref Projectile.localAI[1];
        private int HostIndex => (int)Projectile.ai[0];
        private bool Locked => Projectile.ai[1] == 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EliteMoveNPC.LeapTrackFrames + EliteMoveNPC.LeapLockFrames + 90;
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

        public override void AI() {
            if (!TryHost(out NPC npc, out EliteMoveNPC global)) {
                Projectile.Kill();
                return;
            }
            Age++;

            //跟踪段贴住目标玩家脚下；锁定后位置冻结（由服务端同步一次性校准）
            if (!Locked) {
                Player target = Main.player[npc.target];
                if (target.Alives()) {
                    Projectile.Center = target.Center;
                }
            }
            else if (LockPopped == 0f) {
                LockPopped = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 12f),
                            DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 100, default, 1.1f);
                        dust.noGravity = true;
                    }
                }
            }

            //定身与减益窗口按固定时间表盖戳（各端一致）
            int telegraphTotal = EliteMoveNPC.LeapTrackFrames + EliteMoveNPC.LeapLockFrames;
            if (Age < telegraphTotal) {
                global.StampHold(0.1f, false);
            }
            else {
                global.StampLeapFlight();
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust ring = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * new Vector2(46f, 16f),
                    DustID.Torch, Vector2.Zero, 130, GetTint(npc), 0.8f);
                ring.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GetTint(npc).ToVector3() * 0.25f);
        }

        private static Color GetTint(NPC npc)
            => EliteMoveSets.Profiles.TryGetValue(npc.type, out EliteProfile p) ? p.Tint : Color.OrangeRed;

        public override bool PreDraw(ref Color lightColor) {
            if (!TryHost(out NPC npc, out _)) {
                return false;
            }
            Texture2D lens = CWRAsset.Extra_98.Value;
            Vector2 origin = lens.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color tint = GetTint(npc);
            int telegraphTotal = EliteMoveNPC.LeapTrackFrames + EliteMoveNPC.LeapLockFrames;
            float progress = MathHelper.Clamp(Age / telegraphTotal, 0f, 1f);
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            //落点椭圆底盘（真alpha，压住背景）
            Color basePlate = tint * (0.4f * MathHelper.Clamp(Age / 10f, 0f, 1f));
            Main.EntitySpriteDraw(lens, drawPos, null, basePlate, 0f, origin,
                new Vector2(1.35f, 0.42f), SpriteEffects.None, 0);
            //充能内环：随预告进度长大，读秒条即倒计时
            Color fill = tint with { A = 0 } * (0.5f * pulse);
            Main.EntitySpriteDraw(lens, drawPos, null, fill, 0f, origin,
                new Vector2(1.25f, 0.36f) * progress, SpriteEffects.None, 0);
            if (Locked) {
                //锁定十字：两道细白刃，宣告落点不再移动
                Color cross = Color.White with { A = 0 } * (0.7f * pulse);
                Main.EntitySpriteDraw(lens, drawPos, null, cross, MathHelper.PiOver4, origin,
                    new Vector2(0.9f, 0.07f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(lens, drawPos, null, cross, -MathHelper.PiOver4, origin,
                    new Vector2(0.9f, 0.07f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //落地扬尘（宿主着陆时被服务端击杀，各端在此播放）
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 6f),
                    DustID.Smoke, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
