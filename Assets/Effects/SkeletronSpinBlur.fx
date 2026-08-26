// ============================================================================
//SkeletronSpinBlur.fx 旋杀转体涂抹（对颅骨贴图本体做真旋转模糊）
//消费方式：SpriteBatch Immediate + Additive 里直接 Draw 颅骨贴图（带 npc.rotation），
//本 shader 不画本体，只输出：①逐角回溯的幽灵残像（10 tap 剪影染色）②角向风环速度线
//贴图空间随精灵一起旋转，故 tap 的纹理空间回转即世界空间旋转拖影
//s0=颅骨贴图（LinearClamp：出界采到透明边缘），s1=PerlinNoise（LinearWrap）
//极角审计：theta 唯一消费是 u4=theta*k/2π（k=4∈Z）喂 LinearWrap 平铺噪声 → ±π 缝连续
//加色批输出：rgb 不预乘、a 携带包络
// ============================================================================

float uTime;
float uSeed;        //个体相位
float uSmear;       //回溯总角（弧度，∝转速）
float uSpinDir;     //旋向 ±1
float uIntensity;   //强度 0~1
float2 uTexSize;    //贴图像素尺寸（矩形贴图旋转采样需按px空间校正）
float uInflate;     //画布外扩系数（C# 侧 quad scale 同乘：残像/风环住在颅骨轮廓之外）
float3 uGhostA;     //幽青（新残像）
float3 uGhostB;     //深青（旧残像）
float3 uBone;       //骨白（风环提亮）

sampler texSamp : register(s0);   //SpriteBatch 主贴图：颅骨本体（刻意采样 s0）
sampler noiseSamp : register(s1);

//PerlinNoise.png 实测值域 0.22~0.776，阈值前归一
float nrm(float v)
{
    return saturate((v - 0.22) / 0.556);
}

float2 Rot(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float inflate = max(uInflate, 1.0);
    //外扩画布的 px 空间（中心原点）：原贴图占中央 1/inflate
    float2 p = (coords - 0.5) * uTexSize * inflate;

    //---- 逐角回溯残像：越旧越暗越沉色（tint×能量累计，出口归一防双重衰减）----
    float3 colSum = float3(0.0, 0.0, 0.0);
    float eSum = 0.0;
    for (int i = 1; i <= 10; i++)
    {
        float t = i / 10.0;
        float ang = uSmear * t * uSpinDir;
        float2 sp = Rot(p, ang) / uTexSize + 0.5;   //回到贴图 uv（出界=Clamp 透明缘）
        float4 s = tex2D(texSamp, sp);
        float w = pow(abs(1.0 - t), 2.2) * 0.24;
        float3 tint = lerp(uGhostA, uGhostB, t);
        //剪影承形 + 三成原色细节
        colSum += (tint * 0.85 + s.rgb * 0.3) * (s.a * w);
        eSum += s.a * w;
    }

    //---- 径向度量：R=颅骨短半径，rn>1 即颅外空域 ----
    float r = length(p);
    float R = min(uTexSize.x, uTexSize.y) * 0.5;
    float rn = r / max(R, 1.0);
    //画布护栏（外扩quad边缘归零）
    float guard = smoothstep(1.0, 0.85, rn / inflate);

    //---- 角向风环速度线：住在颅骨轮廓外的一圈（与颅骨同转 + 轻微超前滑移）----
    float band = smoothstep(0.92, 1.12, rn) * smoothstep(inflate * 0.92, inflate * 0.62, rn);
    float theta = atan2(p.y, p.x);
    float u4 = theta * (4.0 / 6.2831853) - uTime * 1.7 * uSpinDir;
    float wind = nrm(tex2D(noiseSamp, float2(u4, rn * 0.55 + uSeed)).r);
    //双层：宽弧带 + 细亮丝
    float arcE = smoothstep(0.50, 0.80, wind) * band * 0.5;
    float streakE = smoothstep(0.80, 0.95, wind) * band * 0.9;
    colSum += uGhostB * arcE + lerp(uGhostA, uBone, 0.35) * streakE;
    eSum += arcE + streakE;

    //加色批法则：rgb 承色（归一平均，不预乘），alpha 承包络
    float3 outCol = colSum / max(eSum, 0.001);
    float outA = saturate(eSum * 1.6) * uIntensity * vertexColor.a * guard;
    return float4(outCol, outA);
}

technique Technique1
{
    pass SkeletronSpinBlurPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
