using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 腐眼芽：断头闸的预告实体，自墙面长出的病变眼球。
    /// ai[0]=墙whoAmI ai[1]=眼序 ai[2]=成形帧。
    /// 成形期跟踪玩家高度并拖瞄准线→锁定闪烁(高度冻结自npc.ai[3]，预告即承诺)→
    /// 静默拍瞄准线熄灭→斩束期眼球充血→束毕爆浆。全程无伤害
    /// </summary>
    internal class WofRotEyeBudProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Wall => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private int GrowFrames => (int)Projectile.ai[2];

        /// <summary>锁定高度，0=仍在跟踪(取到npc.ai[3]后永不回退)</summary>
        private float lockedY;
        /// <summary>当前跟踪高度</summary>
        private float trackY;

        /// <summary>病变肉色：血色里掺进腐败的黄灰</summary>
        internal static readonly Color RotFlesh = new(122, 96, 58);
        internal static readonly Color RotPale = new(196, 176, 110);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        private int LockTick => GrowFrames;
        private int FireTick => GrowFrames + WofDirector.GuillotineLockFlash + WofDirector.GuillotineSilence;
        private int DieTick => FireTick + WofDirector.GuillotineSustain + WofDirector.GuillotineDecay + 4;

        private bool HostValid {
            get {
                NPC wall = Wall;
                return wall.Alives() && wall.type == NPCID.WallofFlesh
                    && WallOfFleshAI.GetStateIndex(wall) == WofStateIndex.RotGuillotine;
            }
        }

        public override void AI() {
            NPC wall = Wall;
            if (!HostValid) {
                Projectile.Kill();
                return;
            }
            if (Timer == 0) {
                trackY = Projectile.Center.Y;
            }
            Timer++;

            //跟踪：成形期黏滞追随目标高度(各端本地演出，权威在锁定值)
            if (lockedY == 0f && Timer >= LockTick && wall.ai[3] != 0f) {
                lockedY = wall.ai[3];
                if (!VaultUtils.isServer && WofMotionFX.OnScreen(Projectile.Center, 200f)) {
                    //锁定咔哒
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.8f, Volume = 0.65f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            if (lockedY != 0f) {
                trackY = lockedY;
            }
            else if (wall.target >= 0 && wall.target < Main.maxPlayers && Main.player[wall.target].Alives()) {
                float margin = WofDirector.GuillotineHalfHeight + 24f;
                float aim = MathHelper.Clamp(Main.player[wall.target].Center.Y,
                    WofWallField.Top + margin, WofWallField.Bottom - margin);
                trackY = MathHelper.Lerp(trackY, aim, 0.16f);
            }

            //锚定墙面，随墙推进
            Projectile.Center = new Vector2(WofWallField.WallFaceX(wall) - wall.direction * 10f, trackY);
            Projectile.direction = wall.direction;

            float grow = MathHelper.Clamp(Timer / (float)GrowFrames, 0f, 1f);
            Lighting.AddLight(Projectile.Center, RotPale.ToVector3() * (0.35f * grow));

            //斩束毕：爆浆收场
            if (Timer >= DieTick) {
                if (!VaultUtils.isServer && WofMotionFX.OnScreen(Projectile.Center, 150f)) {
                    WofMotionFX.SpawnBloodBurst(Projectile.Center, 1f, new Vector2(wall.direction, 0f));
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                            Main.rand.NextVector2Circular(4f, 3f), RotFlesh, Main.rand.NextFloat(0.7f, 1.2f))
                            ?.Configure(Main.rand.Next(20, 34), 0.32f);
                    }
                    SoundEngine.PlaySound(SoundID.NPCDeath12 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
                }
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            //成形期的病肉蠕动滴涎
            if (!VaultUtils.isServer && Timer < LockTick && Timer % 6 == 0
                && WofMotionFX.OnScreen(Projectile.Center, 100f)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    new Vector2(wall.direction * Main.rand.NextFloat(0.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)),
                    Color.Lerp(WofMotionFX.BloodDark, RotFlesh, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(16, 26), 0.3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC wall = Wall;
            if (!wall.Alives()) {
                return false;
            }
            Texture2D spindle = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 posScreen = Projectile.Center - Main.screenPosition;
            int dir = Projectile.direction != 0 ? Projectile.direction : 1;
            float grow = MathHelper.Clamp(Timer / (float)GrowFrames, 0f, 1f);
            float dirAngle = dir > 0 ? 0f : MathHelper.Pi;

            //眼窝：病变肉丘(暗鞘+腐黄肉芯)，实体遮挡
            Color socket = Color.Lerp(WofMotionFX.BloodDark, RotFlesh, 0.45f);
            Main.EntitySpriteDraw(spindle, posScreen, null, socket, dirAngle + MathHelper.PiOver2,
                spindle.Size() / 2f, new Vector2(2.4f, 1.5f) * grow * 0.55f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(spindle, posScreen + new Vector2(dir * 6f, 0f), null,
                Color.Lerp(WofMotionFX.BloodMid, RotFlesh, 0.6f) * 0.9f, dirAngle + MathHelper.PiOver2,
                spindle.Size() / 2f, new Vector2(1.5f, 1f) * grow * 0.55f, SpriteEffects.None, 0);

            //上下眼睑：随成形铰开的两片病肉
            for (int side = -1; side <= 1; side += 2) {
                float lidAngle = dirAngle + dir * side * (0.18f + 0.5f * grow);
                Vector2 lidMid = posScreen + lidAngle.ToRotationVector2() * (34f * grow);
                Main.EntitySpriteDraw(spindle, lidMid, null, socket * 0.95f, lidAngle + MathHelper.PiOver2,
                    spindle.Size() / 2f, new Vector2(0.9f, 68f * grow / spindle.Height), SpriteEffects.None, 0);
            }

            //瞄准线：跟踪期昏黄暗示→锁定期猩红爆闪→静默期熄灭(死寂拍)
            float beamLen = 3200f;
            Vector2 aimDir = new(dir, 0f);
            if (Timer < LockTick) {
                float trackGlow = 0.12f + 0.18f * grow + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f);
                WofMotionFX.DrawAimLine(Main.spriteBatch, Projectile.Center + aimDir * 20f, aimDir,
                    beamLen, 3.5f, RotPale with { A = 0 } * trackGlow);
            }
            else if (Timer < LockTick + WofDirector.GuillotineLockFlash) {
                float flash = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 80f);
                WofMotionFX.DrawAimLine(Main.spriteBatch, Projectile.Center + aimDir * 20f, aimDir,
                    beamLen, 6f, WofMotionFX.BloodHot with { A = 0 } * flash);
            }

            //虹膜：加色暖芯，锁定后充血转猩红，斩束期最亮
            float irisPower = Timer >= FireTick ? 1f : (lockedY != 0f ? 0.8f : 0.35f + 0.3f * grow);
            Color irisCol = lockedY != 0f ? WofMotionFX.BloodHot : RotPale;
            Main.EntitySpriteDraw(glow, posScreen + new Vector2(dir * 10f, 0f), null,
                irisCol with { A = 0 } * (irisPower * 0.9f), 0f, glow.Size() / 2f,
                0.75f * grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, posScreen + new Vector2(dir * 10f, 0f), null,
                new Color(255, 236, 200) with { A = 0 } * (irisPower * 0.6f), 0f, glow.Size() / 2f,
                0.35f * grow, SpriteEffects.None, 0);
            return false;
        }
    }
}
