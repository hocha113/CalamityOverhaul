using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles
{
    /// <summary>
    /// 格挡架势实体：ai[0]=宿主NPC索引 ai[1]=武装帧数（服务端改为 -1 表示转入反击闪光）ai[2]=宿主类型。
    /// 姿态本身即预告：起手 30 帧后进入武装段，武装期被打才会招来反击。
    /// 本体不造成伤害；所有端由本弹幕向宿主镜像盖戳（可见=减伤窗）
    /// </summary>
    internal class EMParryStanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Age => ref Projectile.localAI[0];
        private ref float FlashAge => ref Projectile.localAI[1];
        private int HostIndex => (int)Projectile.ai[0];
        private bool FlashMode => Projectile.ai[1] < 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
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

        public override void AI() {
            if (!TryHost(out NPC npc, out EliteMoveNPC global)) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = npc.Center;

            if (Age == 0f && !FlashMode) {
                //时长在首帧按武装参数定死；反击提前转闪光时由 Kill 逻辑截断
                Projectile.timeLeft = EliteMoveNPC.StanceArmFrames + (int)Projectile.ai[1] + 6;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
                }
            }
            Age++;

            if (FlashMode) {
                //反击闪光段：短促白闪后放行突进（突进本身由宿主 NPC 承担）
                if (FlashAge == 0f && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = 0.25f }, Projectile.Center);
                }
                FlashAge++;
                global.StampStance();
                global.StampHold(0.3f, false);
                if (FlashAge >= EliteMoveNPC.CounterFlashFrames + 2) {
                    Projectile.Kill();
                }
                return;
            }

            bool armed = Age >= EliteMoveNPC.StanceArmFrames;
            global.StampStance();
            global.StampHold(0.25f, false);

            if (!VaultUtils.isServer) {
                if ((int)Age == EliteMoveNPC.StanceArmFrames) {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);
                }
                //武装段盾缘火花，起手段减半（预算内：≤1粒/帧）
                if (Main.rand.NextBool(armed ? 2 : 4)) {
                    Vector2 rim = npc.Center + new Vector2(npc.direction * (npc.width * 0.5f + 8f),
                        Main.rand.NextFloat(-npc.height * 0.45f, npc.height * 0.45f));
                    Dust dust = Dust.NewDustPerfect(rim, DustID.GoldCoin, new Vector2(0f, -0.4f), 120, default, 0.9f);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, GetTint(npc).ToVector3() * (armed ? 0.4f : 0.2f));
        }

        private static Color GetTint(NPC npc)
            => EliteMoveSets.Profiles.TryGetValue(npc.type, out EliteProfile p) ? p.Tint : Color.Gold;

        public override bool PreDraw(ref Color lightColor) {
            if (!TryHost(out NPC npc, out _)) {
                return false;
            }
            Texture2D lens = CWRAsset.Extra_98.Value;
            Vector2 origin = lens.Size() / 2f;
            Color tint = GetTint(npc);
            Vector2 drawPos = npc.Center - Main.screenPosition + new Vector2(npc.direction * (npc.width * 0.5f + 10f), 0f);

            if (FlashMode) {
                //反击白闪：快速扩张的亮盾面
                float t = MathHelper.Clamp(FlashAge / EliteMoveNPC.CounterFlashFrames, 0f, 1f);
                Color flash = Color.White with { A = 0 } * (0.85f * (1f - t * 0.4f));
                Main.EntitySpriteDraw(lens, drawPos, null, flash, 0f, origin,
                    new Vector2(0.5f + t * 0.5f, 1f + t * 0.8f), SpriteEffects.None, 0);
                return false;
            }

            bool armed = Age >= EliteMoveNPC.StanceArmFrames;
            float arm = MathHelper.Clamp(Age / EliteMoveNPC.StanceArmFrames, 0f, 1f);
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);
            //真alpha底层：竖立盾面（有遮挡像素才有实体感）
            Color body = tint * (0.45f * arm);
            Main.EntitySpriteDraw(lens, drawPos, null, body, 0f, origin,
                new Vector2(0.34f, 0.95f), SpriteEffects.None, 0);
            //加色辉边：武装段更亮，读作"现在别打"
            Color glow = tint with { A = 0 } * ((armed ? 0.6f : 0.28f) * pulse);
            Main.EntitySpriteDraw(lens, drawPos, null, glow, 0f, origin,
                new Vector2(0.42f, 1.05f) * (armed ? 1.08f : 1f), SpriteEffects.None, 0);
            return false;
        }
    }
}
