// ============================================================================
//SHPCHoloLattice.fx 全息瞄具光栅屏障
//UV.x 长边 UV.y 短边；s0+s1
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;        //整体透明度 0~1（展开/收起动画由C#驱动）
float deployProgress;   //0~1 展开进度，从中线向两端展开
float glitchAmount;     //0~1 消解敌方弹幕瞬间的故障强度
float3 mainColor;       //主色（全息湖蓝）
float3 accentColor;     //强调色（亮青白）

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;

    //故障行偏移：被命中瞬间横向撕裂
    if (glitchAmount > 0.01)
    {
        float rowID = floor(uv.x * 26.0);
        float rowHash = hash21(float2(rowID, floor(uTime * 14.0)));
        float shift = step(0.78 - glitchAmount * 0.3, rowHash) * (rowHash - 0.5) * 0.18 * glitchAmount;
        uv.y += shift;
    }

    //展开遮罩：从中线向两端展开
    float fromMid = abs(uv.x - 0.5) * 2.0;
    float deployed = smoothstep(deployProgress + 0.03, deployProgress - 0.03, fromMid);

    //网格线
    float gx = frac(uv.x * 14.0);
    float gy = frac(uv.y * 5.0);
    float grid = smoothstep(0.06, 0.0, min(gx, 1.0 - gx)) + smoothstep(0.10, 0.0, min(gy, 1.0 - gy));
    grid = saturate(grid);

    //网格单元随机点亮（数据流动感）
    float cellID = floor(uv.x * 14.0) + floor(uv.y * 5.0) * 31.0;
    float cellOn = step(0.82, hash21(float2(cellID, floor(uTime * 2.5))));
    float cellFill = cellOn * (1.0 - grid) * 0.30;

    //扫描带：沿长边往复扫掠的亮带
    float sweepPos = frac(uTime * 0.45);
    sweepPos = abs(sweepPos * 2.0 - 1.0);     //0→1→0 往复
    float sweep = 1.0 - smoothstep(0.0, 0.12, abs(uv.x - sweepPos));
    sweep *= 0.7;

    //横向扫描线（细密的CRT纹理）
    float scan = step(0.5, frac(uv.y * 22.0 + uTime * 1.5)) * 0.12;

    //边框与边角支架
    float edgeX = min(uv.x, 1.0 - uv.x);
    float edgeY = min(uv.y, 1.0 - uv.y);
    float border = smoothstep(0.025, 0.0, edgeX) + smoothstep(0.06, 0.0, edgeY);
    border = saturate(border);
    //四角加粗支架
    float corner = step(edgeX, 0.10) * step(edgeY, 0.22);
    border = saturate(border + corner * 0.8);

    //整体呼吸与噪声闪烁
    float breathe = 0.85 + 0.15 * sin(uTime * 3.0);
    float flickerNoise = tex2D(noiseSamp, float2(uv.x * 2.0, uv.y + uTime * 0.4)).r;

    float3 color = float3(0.0, 0.0, 0.0);
    color += mainColor * grid * 0.55;
    color += mainColor * cellFill;
    color += accentColor * sweep * 0.6;
    color += mainColor * scan;
    color += accentColor * border * 0.9;
    color += accentColor * glitchAmount * flickerNoise * 0.5;

    float alpha = saturate(grid * 0.4 + cellFill + sweep * 0.35 + scan + border * 0.75 + 0.06);
    alpha *= fadeAlpha * deployed * breathe;

    return float4(color * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCHoloLatticePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
