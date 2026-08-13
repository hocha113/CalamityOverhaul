// ============================================================================
//MLordCelestial.fx 天体屏幕后效（月总）
//采样 uImage0 屏幕；三层门控叠加：星光冲击环（冷色折射+青缘光）
//超新星虹膜（白环+整数倍角射线）+引力昏暗（光被吸向井心）
//直线算术无分支；角向仅 sin(12θ)（整数倍连续）
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAspect;
float4 ringData[3];   //xy=环心uv z=半径(屏高归一) w=强度
float ringCount;
float4 uNova;         //xy=虹膜心uv z=进度0~1 w=强度
float4 uDim;          //xy=井心uv w=昏暗强度

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 totalOffset = float2(0.0, 0.0);
    float glow = 0.0;

    //=========================================================
    //星光冲击环：折射推挤 + 环缘青光
    //=========================================================
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float active = step(float(i) + 0.5, ringCount);
        float4 rd = ringData[i];

        float2 d = (coords - rd.xy) * float2(uAspect, 1.0);
        float r = length(d) + 1e-5;
        float thick = 0.05 + rd.z * 0.06;
        float band = exp(-pow((r - rd.z) / thick, 2.0));

        float2 dir = d / r;
        dir.x /= uAspect;

        totalOffset += dir * band * 0.013 * rd.w * active;
        glow += band * band * rd.w * active;
    }

    //=========================================================
    //超新星虹膜：外扩白环 + 12 瓣射线（整数倍角，跨缝连续）
    //=========================================================
    float2 nd = (coords - uNova.xy) * float2(uAspect, 1.0);
    float nr = length(nd) + 1e-5;
    float novaR = uNova.z * 1.35;
    float novaBand = exp(-pow((nr - novaR) / (0.06 + uNova.z * 0.1), 2.0));
    float2 ndir = nd / nr;
    float rays = 0.5 + 0.5 * sin(12.0 * atan2(ndir.y, ndir.x) + uTime * 2.0);
    float novaGlow = (novaBand * (0.7 + 0.3 * rays) + exp(-nr * nr * 30.0) * 0.8) * uNova.w;
    float2 novaDir = ndir;
    novaDir.x /= uAspect;
    totalOffset += novaDir * novaBand * 0.02 * uNova.w;

    //=========================================================
    //引力昏暗：屏幕向井心的轻微光偏折 + 周边压暗
    //=========================================================
    float2 gd = (coords - uDim.xy) * float2(uAspect, 1.0);
    float gr = length(gd) + 1e-5;
    float2 gdir = gd / gr;
    gdir.x /= uAspect;
    //光被拉向井心（负向偏移）
    float pullBand = exp(-gr * gr * 3.2);
    totalOffset -= gdir * pullBand * 0.011 * uDim.w;
    float dimMask = (1.0 - exp(-gr * gr * 2.2)) * uDim.w;

    //=========================================================
    //RGB 色散采样：冷端更强（青蓝先弯折）
    //=========================================================
    float3 col;
    col.r = tex2D(uImage0, coords - totalOffset * 0.74).r;
    col.g = tex2D(uImage0, coords - totalOffset).g;
    col.b = tex2D(uImage0, coords - totalOffset * 1.28).b;

    //环缘星光（幽蓝青，与机械橙白划清界限）
    col += float3(0.42, 0.90, 0.84) * glow * 0.34;
    //超新星白爆
    col += float3(0.92, 0.97, 1.00) * novaGlow * 0.9;
    //引力压暗
    col *= 1.0 - dimMask * 0.42;

    return float4(col, 1.0);
}

technique Technique1
{
    pass MLordCelestialPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
