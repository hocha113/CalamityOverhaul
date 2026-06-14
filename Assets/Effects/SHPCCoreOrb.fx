// ============================================================================
//SHPCCoreOrb.fx SHPC 启动 HUD 能量核心
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //quad 像素尺寸
float2 uCenter;      //核心圆心局部像素坐标
float uCoreRingR;    //外环半径
float uCoreRadius;   //内核呼吸半径基准
float uExpand;       //展开进度 0~1
float uHover;        //悬停强度 0~1
float uPulse;        //悬停脉冲衰减 0~1
float uClickFlash;   //点击瞬闪 0~1

//================== 工具函数 ==================

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//六边形SDF，输入为相对中心的像素坐标与边长
float hexDist(float2 p, float s) {
    p = abs(p);
    float c = dot(p, normalize(float2(1.0, 1.732)));
    c = max(c, p.x);
    return c - s;
}

//六边形网格坐标，返回到所在六角中心的距离
float hexGrid(float2 p, float size) {
    float2 r = float2(1.0, 1.732);
    float2 h = r * 0.5;
    float2 a = fmod(p, r) - h;
    float2 b = fmod(p + h, r) - h;
    float2 gv = dot(a, a) < dot(b, b) ? a : b;
    return hexDist(gv, size * 0.5);
}

//环SDF：到半径radius且厚度为thick的环的距离
float ringSDF(float r, float radius, float thick) {
    return abs(r - radius) - thick * 0.5;
}

//角度环绕到[-pi,pi]
float wrapPi(float a) {
    a = fmod(a + 3.14159265, 6.28318530);
    if (a < 0) a += 6.28318530;
    return a - 3.14159265;
}

//================== 主像素着色 ==================

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vcol : COLOR0) : COLOR0
{
    float2 p = uv * uResolution;
    float2 d = p - uCenter;
    float r = length(d);
    float ang = atan2(d.y, d.x);

    //展开进度的整体强度调制
    float expand = saturate(uExpand);
    //悬停加亮
    float hover = saturate(uHover);
    //呼吸基频
    float breath = sin(uTime * 2.4) * 0.5 + 0.5;

    float3 col = float3(0, 0, 0);
    float a = 0.0;

    //=========================================================
    //1) 投影：核心整体下方一层柔和暗影
    //=========================================================
    {
        float2 sd = (p - (uCenter + float2(0.0, 2.5)));
        float sr = length(sd);
        float shadow = smoothstep(uCoreRingR + 6.0, uCoreRingR - 2.0, sr);
        col = lerp(col, float3(0.0, 0.015, 0.025), shadow * 0.55);
        a = max(a, shadow * 0.55);
    }

    //=========================================================
    //2) 背景圆盘：深色科技底，含六角数据网纹
    //=========================================================
    float diskMask = 1.0 - smoothstep(uCoreRingR - 1.0, uCoreRingR + 0.8, r);
    if (diskMask > 0.001) {
        //基础暗青底色
        float3 bg = float3(0.025, 0.07, 0.10);
        //径向暗角
        float vign = saturate(1.0 - r / (uCoreRingR + 1.0));
        bg *= 0.55 + vign * 0.65;

        //六角网格数据底纹（旋转坐标系，缓慢漂移）
        float ca = cos(uTime * 0.12);
        float sa = sin(uTime * 0.12);
        float2 hp = float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
        float hexD = hexGrid(hp * 0.45 + float2(uTime * 0.6, 0), 1.0);
        float hexLine = smoothstep(0.05, 0.0, abs(hexD));
        bg += float3(0.05, 0.18, 0.22) * hexLine * 0.45;
        //每个六角内的微弱数据闪烁
        float2 cellId = floor((hp + 6.0) * 0.5);
        float blink = step(0.85, hash21(cellId + floor(uTime * 1.5)));
        float hexFill = smoothstep(0.5, 0.0, hexD) * (1.0 - hexLine);
        bg += float3(0.12, 0.45, 0.55) * hexFill * blink * 0.35;

        //水平扫描线
        float scan = step(0.55, frac(p.y * 0.7 + uTime * 0.6));
        bg *= 1.0 - scan * 0.08;

        col = lerp(col, bg, diskMask);
        a = max(a, diskMask * 0.92);
    }

    //=========================================================
    //3) 中央能量核心：径向梯度+脉动+色散
    //=========================================================
    float coreR = uCoreRadius * (0.78 + breath * 0.15 + uPulse * 0.4) * 0.5;
    {
        //软边光斑
        float t = saturate(1.0 - r / coreR);
        float core = pow(t, 1.3);
        //径向条纹（聚变能量感）
        float rays = 0.5 + 0.5 * sin(ang * 16.0 + uTime * 3.5);
        rays = pow(rays, 4.0) * smoothstep(coreR, coreR * 0.4, r);
        float3 hot = float3(0.65, 1.0, 1.0);
        float3 cool = float3(0.0, 0.55, 0.70);
        float3 coreCol = lerp(cool, hot, core) + hot * rays * 0.35;
        //内焦点白热
        float bright = exp(-r * 0.18);
        coreCol += float3(0.8, 1.0, 1.0) * bright * 0.55;
        col += coreCol * core * (0.85 + hover * 0.4);
        a = max(a, core * 0.95);
    }

    //=========================================================
    //4) 外环主体：SDF抗锯齿圆环，含色散
    //=========================================================
    {
        float ringR = uCoreRingR;
        float thick = 1.6;
        //三层径向偏移做RGB色散
        float aaR = smoothstep(0.8, -0.8, ringSDF(r - 0.6, ringR, thick));
        float aaG = smoothstep(0.8, -0.8, ringSDF(r,        ringR, thick));
        float aaB = smoothstep(0.8, -0.8, ringSDF(r + 0.6, ringR, thick));
        float ringGlow = 0.55 + expand * 0.35 + hover * 0.25 + uClickFlash * 0.5;
        ringGlow = saturate(ringGlow);

        float3 baseCol = lerp(float3(0.20, 0.66, 0.78), float3(0.65, 1.0, 1.0), ringGlow);
        float3 ringCol = float3(baseCol.r * aaR, baseCol.g * aaG, baseCol.b * aaB);
        col += ringCol * (0.85 + hover * 0.4);
        a = max(a, max(aaR, max(aaG, aaB)) * 0.95);

        //外侧柔光
        float outerHalo = exp(-max(r - ringR - thick * 0.5, 0.0) / 4.0)
                       * step(ringR + thick * 0.5, r);
        col += baseCol * outerHalo * (0.35 + hover * 0.3);
        a = max(a, outerHalo * 0.4);
    }

    //=========================================================
    //5) 旋转刻度：4个L形括号，环绕在外环外侧
    //=========================================================
    {
        float markRot = uTime * 0.35;
        float markGap = 6.28318530 / 4.0;
        float markSpan = 0.42;
        float markR = uCoreRingR + 4.0;
        //角度归一化到一个markGap周期
        float aRel = wrapPi(ang - markRot);
        //映射到最近一个标记中心
        float aMod = fmod(aRel + 3.14159265, markGap) - markGap * 0.5;
        float aDist = abs(aMod);
        //径向带
        float rBand = smoothstep(1.4, 0.0, abs(r - markR));
        //角度宽度内
        float angIn = smoothstep(markSpan * 0.5, markSpan * 0.5 - 0.06, aDist);
        float mark = rBand * angIn;
        col += float3(0.25, 0.65, 0.78) * mark * 0.55;
        a = max(a, mark * 0.6);

        //刻度两端的小封口竖线
        float capDist = abs(aDist - markSpan * 0.5);
        float cap = smoothstep(0.04, 0.0, capDist) * smoothstep(3.5, 0.5, abs(r - markR));
        col += float3(0.45, 0.85, 0.95) * cap * 0.6;
        a = max(a, cap * 0.55);
    }

    //=========================================================
    //6) 内圈刻度环：紧贴核心外侧的细密tick
    //=========================================================
    {
        float tickR = uCoreRadius * 0.55 + 2.0;
        float tickBand = smoothstep(1.0, 0.0, abs(r - tickR));
        float tickPhase = ang * 32.0 / 6.28318530 + uTime * 0.2;
        float tickFrac = abs(frac(tickPhase) - 0.5);
        float tick = step(0.42, tickFrac) * tickBand;
        col += float3(0.20, 0.55, 0.66) * tick * 0.5;
        a = max(a, tick * 0.45);
    }

    //=========================================================
    //7) 角度扫描臂：从中心扫出一道光线，慢速旋转
    //=========================================================
    if (r < uCoreRingR + 1.0) {
        float sweepAng = uTime * 0.9;
        float aDelta = abs(wrapPi(ang - sweepAng));
        //仅在前向小角度内显示
        float sweep = exp(-pow(aDelta / 0.08, 2.0))
                    * smoothstep(uCoreRingR, uCoreRadius * 0.4, r);
        col += float3(0.45, 0.95, 1.0) * sweep * 0.55;
        a = max(a, sweep * 0.5);
    }

    //=========================================================
    //8) 中央十字微元素，叠加薄遮罩制造层次
    //=========================================================
    {
        float cross = 0.0;
        cross = max(cross, step(abs(d.y), 0.55) * step(abs(d.x), 3.2));
        cross = max(cross, step(abs(d.x), 0.55) * step(abs(d.y), 3.2));
        col = lerp(col, float3(0.02, 0.06, 0.08), cross * 0.85);
    }

    //=========================================================
    //9) 点击瞬闪：以核心为中心扩散的薄环
    //=========================================================
    if (uClickFlash > 0.01) {
        float flashR = uCoreRingR + (1.0 - uClickFlash) * 30.0;
        float fSDF = abs(r - flashR);
        float flash = exp(-fSDF * 0.45) * uClickFlash;
        col += float3(0.65, 1.0, 1.0) * flash * 0.85;
        a = max(a, flash * 0.85);
    }

    //=========================================================
    //10) 悬停整体辉光：核心外侧呼吸式柔光
    //=========================================================
    if (hover > 0.01) {
        float outer = exp(-max(r - uCoreRingR - 1.0, 0.0) / 9.0);
        float pulseHover = 0.65 + sin(uTime * 4.5) * 0.35;
        col += float3(0.25, 0.85, 0.95) * outer * hover * pulseHover * 0.45;
        a = max(a, outer * hover * 0.4);
    }

    //展开未到则整体减暗（核心始终可见但稍弱）
    float globalScale = 0.85 + expand * 0.15;
    col *= globalScale;

    //=========================================================
    //输出
    //=========================================================
    float finalA = saturate(a) * uAlpha;
    return float4(col * uAlpha, finalA);
}

technique Technique1
{
    pass SHPCCoreOrbPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
