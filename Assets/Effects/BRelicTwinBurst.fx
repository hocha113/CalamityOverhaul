// ============================================================================
//BRelicTwinBurst.fx 交叉冲锋交点引爆：双源干涉爆纹
//本地空间方形面片 p∈[-1,1]；红/绿两个干涉源沿x轴对置(C#把x轴旋成系绳分离轴)，
//等程差双曲条纹随撕裂波前扩张显影，核心随进度掏空成环退潮
//Additive 批，a 携带包络；全笛卡尔无 atan2 无动态分支
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);   //PerlinNoise

float3 uColorA;     //红(视界)
float3 uColorB;     //青焰绿(焚瞳)
float uTime;
float uProgress;    //0~1 爆发进度
float uOpacity;
float uSep;         //干涉源半间距(本地空间)
float uFlash;       //起爆白闪0~1

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

//波前扩张曲线：前快后缓，与 C# 侧判定半径同式
float FrontAt(float t)
{
    float u = 1.0 - t;
    return 1.0 - u * u * pow(max(u, 0.0001), 0.6);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 p = input.TexCoords * 2.0 - 1.0;
    float r = length(p);

    float2 c = float2(uSep, 0.0);
    float d1 = length(p - c);
    float d2 = length(p + c);

    //=== 等程差干涉条纹：红绿反相双曲族 ===
    float phase = (d1 - d2) * 22.0;
    float fringeWave = sin(phase);
    float fringeA = saturate(fringeWave);
    float fringeB = saturate(-fringeWave);
    //衬度随径向衰减，边缘不糊成色罩
    float contrast = 1.0 - smoothstep(0.0, 1.0, r);

    //=== 撕裂波前 ===
    float front = FrontAt(uProgress);
    float noiseTear = tex2D(uImage1, float2(p.x * 0.45 + 0.5, p.y * 0.45 + 0.5 + uTime * 0.07)).r;
    float rr = r + (noiseTear - 0.5) * 0.18;
    float inside = smoothstep(front, front - 0.16, rr);
    float rim = exp(-pow((rr - front) * 9.0, 2.0)) * 1.6;

    //=== 退潮：整体衰减+核心掏空成环 ===
    float fade = 1.0 - uProgress * uProgress;
    float centerHole = smoothstep(0.05, 0.35, r + uProgress * 0.3);

    //=== 合成 ===
    float bands = (fringeA + fringeB) * contrast * inside * centerHole;
    float flash = uFlash * pow(saturate(1.0 - r * 1.4), 2.0) * 2.2;

    float intensity = bands * 0.9 + rim * (0.4 + 0.6 * contrast) + flash;
    intensity *= uOpacity * fade;

    float3 col = uColorA * fringeA + uColorB * fringeB;
    col = col * contrast + lerp(uColorA, uColorB, 0.5) * rim * 0.5;
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(flash));

    col *= input.Color.rgb;
    return float4(col * intensity, saturate(intensity) * input.Color.a);
}

technique Technique1
{
    pass BurstPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
