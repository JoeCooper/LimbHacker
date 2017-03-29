Shader "Noble Muffins/Doublesided (One-Tex, Unlit)" {
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" { }
    }

    Category
    {
        ZWrite On
        SubShader
        {
            Lighting Off
            Pass
            {
        		Cull Off
                SetTexture [_MainTex]
                {
                    Combine texture
                } 
            }
        } 
    }
}
