using CalamityOverhaul.Common;
using Terraria.Audio;

public enum LoadingAmmoAnimationEnum
{
    /// <summary>
    /// 默认动作
    /// </summary>
    None,
    /// <summary>
    /// 霰弹枪类型
    /// </summary>
    Shotgun,
    /// <summary>
    /// 手枪类型
    /// </summary>
    Handgun,
    /// <summary>
    /// 左轮类型
    /// </summary>
    Revolver,
}

public struct LoadingAA_None_Struct
{
    /// <summary>
    /// 默认为50
    /// </summary>
    public int Roting = 50;
    /// <summary>
    /// 默认为4
    /// </summary>
    public int gunBodyX = 4;
    /// <summary>
    /// 默认为24
    /// </summary>
    public int gunBodyY = 24;

    public LoadingAA_None_Struct() { }
}

public struct LoadingAA_Shotgun_Struct
{
    /// <summary>
    /// 装载弹壳的音效
    /// </summary>
    public SoundStyle loadShellSound;
    /// <summary>
    /// 弹出弹壳的音效
    /// </summary>
    public SoundStyle pump;
    /// <summary>
    /// 默认为15
    /// </summary>
    public int pumpCoolingValue;
    /// <summary>
    /// 默认为30
    /// </summary>
    public int Roting;
    /// <summary>
    /// 默认为0
    /// </summary>
    public int gunBodyX;
    /// <summary>
    /// 默认为13
    /// </summary>
    public int gunBodyY;

    public LoadingAA_Shotgun_Struct() {
        loadShellSound = CWRSound.Gun_Shotgun_LoadShell with { Volume = 0.75f };
        pump = CWRSound.Gun_Shotgun_Pump with { Volume = 0.6f };
        pumpCoolingValue = 15;
        Roting = 30;
        gunBodyX = 0;
        gunBodyY = 12;
    }
}

public struct LoadingAA_Handgun_Struct
{
    /// <summary>
    /// 退出弹匣的音效
    /// </summary>
    public SoundStyle clipOut;
    /// <summary>
    /// 装上弹匣的音效
    /// </summary>
    public SoundStyle clipLocked;
    /// <summary>
    /// 推动枪栓的音效
    /// </summary>
    public SoundStyle slideInShoot;
    /// <summary>
    /// 默认为50/60f
    /// </summary>
    public float level1;
    /// <summary>
    /// 默认为40/60f
    /// </summary>
    public float level2;
    /// <summary>
    /// 默认为10/60f
    /// </summary>
    public float level3;
    /// <summary>
    /// 默认为-20
    /// </summary>
    public int Roting;
    /// <summary>
    /// 默认为6
    /// </summary>
    public int gunBodyX;
    /// <summary>
    /// 默认为-6
    /// </summary>
    public int gunBodyY;

    public LoadingAA_Handgun_Struct() {
        clipOut = CWRSound.Gun_HandGun_ClipOut with { Volume = 0.65f };
        clipLocked = CWRSound.Gun_HandGun_ClipLocked with { Volume = 0.65f };
        slideInShoot = CWRSound.Gun_HandGun_SlideInShoot with { Volume = 0.65f };
        level1 = 50 / 60f;
        level2 = 40 / 60f;
        level3 = 10 / 60f;
        Roting = -20;
        gunBodyX = 6;
        gunBodyY = -6;
    }
}

public struct LoadingAA_Revolver_Struct
{
    /// <summary>
    /// 转动速度，使用角度制, 默认为30
    /// </summary>
    public float Rotationratio;
    /// <summary>
    /// 抛壳音效
    /// </summary>
    public SoundStyle Sound;
    /// <summary>
    /// 默认为30
    /// </summary>
    public int OffsetRotOver;
    /// <summary>
    /// 默认为0.3f
    /// </summary>
    public float StartRotOver;
    /// <summary>
    /// 默认为4
    /// </summary>
    public int gunBodyX;
    /// <summary>
    /// 默认为6
    /// </summary>
    public int gunBodyY;

    public LoadingAA_Revolver_Struct() {
        Rotationratio = 30;
        Sound = CWRSound.CaseEjection;
        OffsetRotOver = 30;
        StartRotOver = 0.3f;
        gunBodyX = 4;
        gunBodyY = 6;
    }
}
