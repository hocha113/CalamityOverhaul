// ============================================================================
//CultistBoundary.fx 教徒限制圈:天界塔护盾质感的穹膜+硬边亮环+充能弧
//画布契约:主环位于画布半径 0.88,C# 折算 quadPx = 圈半径px / 0.88 * 2
//极角审计:充能弧的 atan2 缝与弧起点重合(弧从缝处生长),其余全笛卡尔
//预乘 AlphaBlend;s1=平铺 Perlin
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
float uAlpha;      //整体透明度(展开/收拢)
float uFill;       //仪式充能弧 0~1
float uPulse;      //撞墙/满充脉冲 0~1
float3 uColMain;   //穹膜主色(阶段芯色)
float3 uColRim;    //环缘亮色

static const float RimR = 0.88;

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);
    if (r > 0.99) {
        return float4(0, 0, 0, 0);
    }
    float2 unit = r > 0.001 ? p / r : float2(0.0, 1.0);

    //穹膜:整个界内都有微弱膜感(场中央也读得出"身在界内"),向缘急剧增亮
    //旧版膜从 0.54R 才起步,玩家离缘远时全屏无一像素属于界,读成"限制圈消失"
    //近缘带收窄压暗(0.30R/1.0→0.16R/0.72):贴墙时它就是整个屏幕,
    //太宽太亮会把界内弹幕全部糊进金幕里读成"弹幕消失"
    float memFar = smoothstep(0.04, RimR, r) * 0.30;
    float memNear = smoothstep(RimR - 0.16, RimR, r) * 0.72;
    float mem = (memFar + memNear) * step(r, RimR);
    float flow = noise(unit * 1.2 + r * 3.0 + uTime * float2(0.06, 0.04));
    float flow2 = noise(unit * 2.3 - uTime * float2(0.04, 0.07) + 5.1);
    float sheen = 0.30 + 0.40 * flow + 0.20 * flow2;
    float3 dome = uColMain * mem * sheen;
    float aDome = mem * (0.26 + 0.22 * flow);

    //主环:硬边双线,内伴线(加厚:远观也是一道明确的界)
    float ring = 1.0 - smoothstep(0.0, 0.026, abs(r - RimR));
    float ring2 = (1.0 - smoothstep(0.0, 0.050, abs(r - (RimR - 0.048)))) * 0.45;
    //缘外辉:护盾外溢光(拉长衰减,缘外一段仍有光晕)
    float halo = exp(-max(r - RimR, 0.0) * 13.0) * step(RimR, r);

    //充能弧:符金,自缝处顺角生长(缝=弧起点,不可见)
    float norm = (atan2(p.x, -p.y) + 3.14159265) / 6.2831853;
    float fillArc = step(norm, uFill);
    float fillBand = 1.0 - smoothstep(0.0, 0.022, abs(r - RimR));
    float3 fillGold = float3(1.0, 0.84, 0.50);

    //撞墙脉冲只点亮环线邻域:乘 mem 会把增亮摊满整张膜,
    //贴墙推挤时 uPulse 常驻 0.8,等于给全屏叠白金幕
    float pulseBand = exp(-abs(r - RimR) * 7.0);

    float3 C = dome
        + uColRim * (ring * 1.1 + ring2)
        + uColMain * halo * 0.9
        + fillGold * fillArc * fillBand * 1.15
        + uColRim * uPulse * pulseBand * 0.9;
    float A = aDome + ring * 0.85 + ring2 * 0.35 + halo * 0.30 + fillArc * fillBand * 0.55;

    return float4(C, saturate(A)) * uAlpha * vertexColor;
}

technique TechBoundary
{
    pass BoundaryPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

//===========================================================================
//TechGlyphRing 古文字光环:环带上一圈伪古文字刻痕,缓慢旋转,教徒本体装饰
//角向 48 整数格量化(跨 ±π 连续),格内噪声阈值拼出笔画块=古文字观感
//===========================================================================
float4 GlyphRingPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);
    //环带:0.62~0.88
    float band = smoothstep(0.60, 0.66, r) * (1.0 - smoothstep(0.86, 0.92, r));
    if (band <= 0.002) {
        return float4(0, 0, 0, 0);
    }

    //角向 48 格,整数倍角跨缝连续;缓慢旋转
    float normAngle = (atan2(p.x, -p.y) + 3.14159265) / 6.2831853;
    float cellPh = frac(normAngle + uTime * 0.014);
    float cellId = floor(cellPh * 48.0);
    float2 gUv = float2(frac(cellPh * 48.0), (r - 0.62) / 0.26);

    //伪古文字:格内粗网格阈值出笔画块,横竖笔织出刻文感
    float2 g = floor(gUv * float2(3.0, 4.0));
    float stroke = step(0.52, noise((g + float2(cellId * 7.13, cellId * 3.71)) * 0.061 + 0.17));
    //格间留缝,字距分明
    float gapX = step(0.10, gUv.x) * step(gUv.x, 0.90);
    float gapY = step(0.08, gUv.y) * step(gUv.y, 0.92);
    float glyph = stroke * gapX * gapY;

    //底环弱光 + 刻文亮痕
    float3 C = uColMain * band * 0.16 + uColRim * glyph * band * 1.05 + uColRim * uPulse * band * 0.5;
    float A = band * 0.10 + glyph * band * 0.72;
    return float4(C, saturate(A)) * uAlpha * vertexColor;
}

technique TechGlyphRing
{
    pass GlyphRingPass
    {
        PixelShader = compile ps_3_0 GlyphRingPS();
    }
}
