Shader "Custom/Transparent Shadow Receiver" {
Properties { }

SubShader {
Tags {"Queue"="AlphaTest" }
Pass {
    Tags {"LightMode" = "ForwardBase" }
    Cull Back
    ZWrite Off
    Blend SrcAlpha OneMinusSrcAlpha

    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma multi_compile_fwdbase

    #include "UnityCG.cginc"
    #include "AutoLight.cginc"

    struct v2f {
        float4 pos : SV_POSITION;
        LIGHTING_COORDS(0,1)
    };

    v2f vert(appdata_base v) {
        v2f o;
        o.pos = UnityObjectToClipPos (v.vertex);
        TRANSFER_VERTEX_TO_FRAGMENT(o);
        
        return o;
    }

    fixed4 frag(v2f i) : COLOR {
        return fixed4(0, 0, 0, 1.0f - LIGHT_ATTENUATION(i));
    }
    ENDCG
}
}
Fallback "VertexLit"
}