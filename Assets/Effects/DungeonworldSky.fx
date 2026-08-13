// ============================================================================
//DungeonworldSky.fx 地牢子世界天幕——暗石蓝灰永夜 + 地平烛金残光
//倒置明度天穹(OniRainSky 同法):头顶近黑,地平残光;随 uDepthGrade 压暗并染当层强调色
//预乘淡入(整体乘 uIntensity);直线算术,无分支,无采样器,fbm ≤3 octave
//色板与 DungeonworldLoadTheme 同源,改动必须双改
// ============================================================================

float uTime;
float uIntensity;
float uAspectRatio;
float uDepthGrade;   //0..1 玩家深度(0=世界顶)
float3 uAccent;      //当层强调色(CPU 按深度混合后传入)

#define ABYSS      float3(0.0196, 0.0275, 0.0549)
#define STONE      float3(0.1216, 0.1529, 0.2078)
#define CANDLE     float3(0.9137, 0.7255, 0.4000)

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm3(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * valueNoise(p);
        p = p * 2.07 + float2(3.1, 7.7);
        a *= 0.5;
    }
    return v;
}

float4 PSDungeonworldSky(float2 uv : TEXCOORD0) : COLOR0
{
    float t = uTime;

    //倒置明度天穹:头顶近黑,地平残光
    float g = smoothstep(0.02, 0.95, uv.y);
    float3 col = lerp(ABYSS * 0.8, STONE * 0.85, g * g);

    //两层慢流云(不同尺度反向漂,层间差速给纵深)
    float2 cuv = float2(uv.x * uAspectRatio, uv.y);
    float c1 = fbm3(cuv * 2.1 + float2(t * 0.011, 0.0));
    float c2 = fbm3(cuv * 4.3 - float2(t * 0.007, 0.05));
    col += (c1 - 0.5) * 0.034 * (0.35 + g);
    col += (c2 - 0.5) * 0.020;

    //地平烛金残光(井口灯火的方向感)
    col += CANDLE * exp(-(1.0 - uv.y) * 5.0) * 0.085;

    //当层强调色轻染 + 随深度整体压暗
    col = lerp(col, col * 0.70 + uAccent * 0.14, 0.18 + uDepthGrade * 0.42);
    col *= 1.0 - uDepthGrade * 0.42;

    //逐帧细尘,压数字平滑感
    col *= 1.0 - hash21(uv * 997.3 + floor(t * 9.0) * 13.7) * 0.02;

    return float4(saturate(col), 1.0) * uIntensity;
}

technique DungeonworldSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSDungeonworldSky();
    }
}
