// ============================================================================
//StitcherBoneDress.fx 干骨拼装体材质（精灵重绘，SpriteBatch Immediate）
//s0=宿主精灵（SpriteBatch 主纹理） s1=PerlinNoise（实测值域 0.227~0.776，阈值过 nrm()）
//输入顶点色 = Draw 传入的光照染色；uUvRect 帧区域钳制（配 C# 源矩形内缩 1px 双通道防帧表渗色）
//材质：陈年干骨 + 缝线接缝。签名行为：
//①干白重调（亮度→骨影~骨白斜坡，保留精灵原细节）②裂纹网随磨损加深
//③粉尘缘光——上缘落灰更亮（灰尘沉降方向性，不是均匀描边）
//④缝线接缝带：交叉线迹 X 纹 + 金色呼吸；磨损把针脚咬断成虚线
//⑤uWear 高段噪声蚀块（散架用）。输出预乘，A>0 实体遮挡
//无极角、无动态分支；8 次取样（1 本体+4 邻域+3 噪声）
// ============================================================================

sampler spriteSamp : register(s0); //批主纹理（Draw 覆写 s0 语义）
sampler noiseSamp : register(s1);

float uTime;
float uSeed;       //实例相位
float4 uUvRect;    //帧区域 (x, y, w, h)（整图 UV）
float2 uTexel;     //1/纹理尺寸
float uChalk;      //干白重调强度 0~1
float uWear;       //磨损/散架 0~1
float uSeamGlow;   //接缝金亮 0~1
float2 uSeamY;     //两道接缝的帧内高度（0~1）
float3 uBonePale;
float3 uBoneShadow;
float3 uGold;

float nrm(float raw)
{
    return saturate((raw - 0.227) / 0.549);
}

//邻域取样钳进帧内（防跨帧渗色）
float alphaAt(float2 uv)
{
    float2 lo = uUvRect.xy + uTexel;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel;
    return tex2D(spriteSamp, clamp(uv, lo, hi)).a;
}

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    float4 c = tex2D(spriteSamp, uv);
    float a = c.a;
    float2 fuv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001); //帧内 0~1

    //---- 干白重调：亮度斜坡映入骨色谱，保留原图细节 ----
    float luma = dot(c.rgb, float3(0.30, 0.59, 0.11));
    float3 boneRamp = lerp(uBoneShadow, uBonePale, saturate(luma * 1.30));
    float3 body = lerp(c.rgb, boneRamp, uChalk);

    //---- 裂纹网：噪声等值线，磨损加深 ----
    float n1 = nrm(tex2D(noiseSamp, fuv * 2.2 + uSeed).r);
    float crackLine = 1.0 - smoothstep(0.015, 0.055, abs(n1 - 0.5));
    body *= 1.0 - crackLine * (0.10 + uWear * 0.55);

    //---- 粉尘缘光：上缘更亮（灰尘落在朝上的面）----
    float aUp = alphaAt(uv - float2(0.0, uTexel.y * 1.6));
    float aDn = alphaAt(uv + float2(0.0, uTexel.y * 1.6));
    float aLf = alphaAt(uv - float2(uTexel.x * 1.6, 0.0));
    float aRt = alphaAt(uv + float2(uTexel.x * 1.6, 0.0));
    float top = saturate((a - aUp) * 2.6);
    float rim = saturate(a * 4.0 - (aUp + aDn + aLf + aRt));
    float n2 = nrm(tex2D(noiseSamp, fuv * 5.6 + uSeed * 2.0).r);
    body += uBonePale * (top * (0.45 + 0.55 * n2) * 0.34 + rim * 0.08) * uChalk;

    //---- 缝线接缝：两道交叉线迹带 ----
    float seam = 0.0;
    float band1 = 1.0 - smoothstep(0.020, 0.052, abs(fuv.y - uSeamY.x));
    float band2 = 1.0 - smoothstep(0.020, 0.052, abs(fuv.y - uSeamY.y));
    float zA = abs(frac(fuv.x * 7.0 + fuv.y * 3.0 + uSeed) - 0.5);
    float zB = abs(frac(fuv.x * 7.0 - fuv.y * 3.0 - uSeed) - 0.5);
    float lattice = max(1.0 - smoothstep(0.04, 0.13, zA), 1.0 - smoothstep(0.04, 0.13, zB));
    //磨损把针脚咬断成虚线
    float stitchGate = step(uWear * 0.72, nrm(tex2D(noiseSamp, float2(fuv.x * 4.6 + uSeed * 3.0, fuv.y * 1.3)).r) + 0.06);
    float breath = 0.72 + 0.28 * sin(uTime * 5.0 + uSeed * 9.0);
    seam = max(band1, band2) * lattice * stitchGate * uSeamGlow * breath;

    //---- 高磨损蚀块（散架前沿）----
    float n3 = nrm(tex2D(noiseSamp, fuv * 3.4 + uSeed * 4.0).r);
    float erode = step(uWear * 0.92, n3 + 0.10);
    a *= erode;

    //光照染色 + 全局包络；预乘输出。缝金向金色 lerp 收拢不叠加，暖材质不白热截断
    float3 lit = body * vColor.rgb;
    lit = lerp(lit, uGold, saturate(seam) * 0.72);
    float alpha = a * vColor.a;
    return float4(lit * alpha, alpha);
}

technique Technique1
{
    pass StitcherBoneDressPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
