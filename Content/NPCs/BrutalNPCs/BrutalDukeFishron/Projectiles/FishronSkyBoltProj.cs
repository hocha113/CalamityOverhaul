using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 敌对天雷：从云底劈到地面预告点。生成于高空锚点，ai[2]=落点 y。
    /// 复用 Lightning 的 ThunderTrail 管线，仅改为敌对判定
    /// </summary>
    internal class FishronSkyBoltProj : Lightning
    {
        internal const int BoltDamage = 44;

        public override float BaseSpeed => 20f;
        public override int LingerTime => 20;
        public override int FadeTime => 12;
        public override float BaseWidth => 40f;
        public override int MaxBranches => 2;

        public override Color GetLightningColor(float factor) => FishronMotionFX.StormBolt;

        public override void SetLightningDefaults() {
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
        }

        public override Vector2 FindTargetPosition() {
            //落点：本体 x 垂直向下到 ai[2] 指定的 y。
            //ai[0]/ai[1] 是 Lightning 基类的状态与命中标记，落点参数只能挂 ai[2]（同 StormLightning）
            return new Vector2(Projectile.Center.X, Projectile.ai[2]);
        }

        public override void OnStrike() {
            //落点白闪+水花+短震
            FishronStormSky.PushFlash(0.45f, TargetPosition);
            FishronMotionFX.SpawnSplashBurst(TargetPosition, 0.9f, playSound: false);
            FishronMotionFX.CameraPunch(TargetPosition, 4f, 8, "FishronBolt");
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 5 }, TargetPosition);
        }
    }
}
