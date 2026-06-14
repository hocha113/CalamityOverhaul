// ============================================================================
//PrimeShockRing.fx 屏幕空间冲击波环
//采样 uImage0 屏幕；无环处透传；最多 3 并发环
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAspect;        //屏幕宽高比（x 轴距离需乘以它做圆形校正）
float4 ringData[3];   //xy=环心(归一化屏幕uv) z=当前半径(以屏高归一) w=强度0~1
float ringCount;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 totalOffset = float2(0.0, 0.0);
    float glow = 0.0;

    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float active = step(float(i) + 0.5, ringCount);
        float4 rd = ringData[i];

        float2 d = (coords - rd.xy) * float2(uAspect, 1.0);
        float r = length(d) + 1e-5;
        float thick = 0.05 + rd.z * 0.07;
        float band = exp(-pow((r - rd.z) / thick, 2.0));

        float2 dir = d / r;
        dir.x /= uAspect;

        //折射：环带处把画面向外推挤
        totalOffset += dir * band * 0.014 * rd.w * active;
        glow += band * band * rd.w * active;
    }

    //RGB 色散采样：三通道偏移系数不同，环缘出现彩边
    float3 col;
    col.r = tex2D(uImage0, coords - totalOffset * 1.30).r;
    col.g = tex2D(uImage0, coords - totalOffset).g;
    col.b = tex2D(uImage0, coords - totalOffset * 0.72).b;

    //环缘炽光（机械橙白）
    col += float3(1.0, 0.62, 0.28) * glow * 0.40;

    return float4(col, 1.0);
}

technique Technique1
{
    pass PrimeShockRingPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
