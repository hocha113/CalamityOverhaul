// ============================================================================
//FloodGalleryAmbience.fx 泄洪堂房内氛围（B2 频段 1.650-1.659 的 RenderHandle 专用）
//两个单 pass technique（调用点各选各的，不共 pass 防误取）：
//  TechGalleryHaze     水面以上：半淹管廊湿气 + 拱带漏光竖梯（沼绿基调）
//  TechGalleryCaustics 水面以下：焦散光网 + 水面亮线 + 深水浊暗（水位即相位表的可视层）
//uv 直接映射世界 px（world = uWorldTL + uv * uWorldSize），世界锚定不随镜头漂移；
//直线算术、无分支、无 atan2，噪声全走绑定贴图（FNA3D 翻译纪律）；
//两 pass 输出全预乘（浊暗必须能压暗，加色批物理上画不出暗）。
//uniform 是设备全局状态：调用点每帧全参数重设，防跨调用残值串场。
// ============================================================================

sampler uScreen : register(s0);   //白像素画布，不采样
sampler uNoise : register(s1);    //Masking/PerlinNoise 512²，LinearWrap；G 通道实测域 0.22~0.776

float2 uWorldTL;      //绘制矩形左上角（世界 px）
float2 uWorldSize;    //绘制矩形尺寸（世界 px）
float uSurfaceY;      //当前水面（世界 px；干房传地板行，水下层自然归零）
float uFloorY;        //地板顶（世界 px，深度归一分母）
float uTime;
float uPresence;      //整体淡入淡出（房外→房内渐进）
float uAgitate;       //扰动档：1=常态踝水，仪式涨水/死亡泄洪时抬高（快滚+高对比）
float3 uTintShallow;  //浅水/湿气沼绿
float3 uTintDeep;     //深水浊绿（压暗用，偏黑）

//Perlin G 域校准：0.22~0.776 映到 0..1（禁高分位死阈值）
float CalN(float g) {
    return saturate((g - 0.22) * 1.8);
}

//==================== 水上：湿气 + 漏光 ====================

float4 PSHaze(float2 uv : TEXCOORD0) : COLOR0 {
    float2 world = uWorldTL + uv * uWorldSize;

    //湿气：双倍频慢滚雾，贴水面一条呼吸带（管廊的水汽都从水面蒸上来）
    float n1 = CalN(tex2D(uNoise, world * 0.0009 + float2(uTime * 0.020, uTime * 0.008)).g);
    float n2 = CalN(tex2D(uNoise, world * 0.0021 + float2(-uTime * 0.031, uTime * 0.013)).g);
    float mist = n1 * 0.6 + n2 * 0.4;
    float bandT = saturate(1.0 - abs(world.y - uSurfaceY) / 150.0);
    float hazeA = mist * (0.10 + 0.22 * bandT * bandT);

    //拱带漏光：顶拱透下的竖光梯（列强度走噪声横采样，缓慢换列），
    //纵向再叠一层慢闪（漏光穿过湿气的忽明忽暗），水面前衰减完
    float colN = CalN(tex2D(uNoise, float2(world.x * 0.0011 + uTime * 0.004, 0.31)).g);
    float flick = CalN(tex2D(uNoise, world * 0.0016 + float2(uTime * 0.009, -uTime * 0.024)).g);
    float col = colN * colN * (0.55 + 0.45 * flick);
    float fallT = saturate((world.y - uWorldTL.y) / max(uSurfaceY - uWorldTL.y, 1.0));
    float above = saturate((uSurfaceY - world.y) * 0.02);
    float shaftA = col * (1.0 - fallT) * above * 0.40;

    float a = saturate((hazeA * above + shaftA) * uPresence);
    //漏光比湿气亮一档半：往浅调里掺白，湿气自身也随噪声在深浅两调间走（防单色压扁）
    float3 tint = lerp(uTintDeep + uTintShallow * 0.8, uTintShallow * 1.35, mist)
        + float3(0.30, 0.34, 0.26) * saturate(shaftA * 2.5);
    return float4(tint * a, a);
}

//==================== 水下：焦散 + 水面亮线 + 浊暗 ====================

float4 PSCaustics(float2 uv : TEXCOORD0) : COLOR0 {
    float2 world = uWorldTL + uv * uWorldSize;

    float below = saturate((world.y - uSurfaceY) * 0.03);
    float depth = saturate((world.y - uSurfaceY) / max(uFloorY - uSurfaceY, 1.0));

    //焦散光网：双层反向滚动的脊化噪声相乘（脊化 = 1-|2n-1|，噪声谷线变亮丝）
    float speed = 0.016 * uAgitate;
    float g1 = CalN(tex2D(uNoise, world * 0.0052 + float2(uTime * speed, uTime * speed * 0.6)).g);
    float g2 = CalN(tex2D(uNoise, world * 0.0037 + float2(-uTime * speed * 0.8, uTime * speed * 0.45)).g);
    float r1 = 1.0 - abs(g1 * 2.0 - 1.0);
    float r2 = 1.0 - abs(g2 * 2.0 - 1.0);
    float web = r1 * r2;
    web = web * web;   //平方锐化：亮丝收窄但网还看得见（三次会把网熄成星点）
    //焦散贴水面最亮，往深处熄灭；扰动档抬对比
    float webA = web * (1.0 - depth * 0.75) * below * (0.55 + 0.30 * saturate(uAgitate - 1.0));

    //水面亮线：表面 6px 内一条颤动亮缝（水位爬升时这条线就是演出的指针）
    float shimmer = CalN(tex2D(uNoise, float2(world.x * 0.006 + uTime * 0.05 * uAgitate, 0.71)).g);
    float lineA = saturate(1.0 - abs(world.y - uSurfaceY) / 6.0) * (0.45 + 0.45 * shimmer);

    //深水浊暗：往深处压一层墨绿（P3 深水的压迫感由它承担）
    float murkA = depth * depth * 0.50 * below;

    float a = saturate((webA + lineA * 0.7 + murkA) * uPresence);
    //焦散是"光"：比水色亮出一档；亮线接近白绿高光
    float3 webCol = lerp(uTintShallow * 1.7, uTintShallow * 0.7, depth);
    float3 lineCol = float3(0.72, 0.92, 0.78);
    float3 rgb = webCol * webA + lineCol * lineA * 0.7 + uTintDeep * murkA;
    return float4(rgb * uPresence, a);
}

technique TechGalleryHaze {
    pass GalleryHaze {
        PixelShader = compile ps_3_0 PSHaze();
    }
}

technique TechGalleryCaustics {
    pass GalleryCaustics {
        PixelShader = compile ps_3_0 PSCaustics();
    }
}
