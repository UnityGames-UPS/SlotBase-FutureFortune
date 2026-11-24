using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FF: Task 2 - Symbol Vertical Motion Blur Generator
public class SymbolBlurGenerator : EditorWindow
{
    private const string PrefKeyBlurStrength = "FF_SymbolBlur_Strength";
    [SerializeField] private float blurStrength = 0.18f; // 18% of sprite height

    [MenuItem("Tools/FF/Generate Blurred Symbols")]
    public static void ShowWindow()
    {
        var win = GetWindow<SymbolBlurGenerator>("Symbol Blur Generator");
        win.blurStrength = EditorPrefs.GetFloat(PrefKeyBlurStrength, 0.18f);
        win.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("FF Symbol Vertical Motion Blur", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        blurStrength = EditorGUILayout.Slider("Blur Strength (% of Height)", blurStrength, 0.05f, 0.40f);
        EditorPrefs.SetFloat(PrefKeyBlurStrength, blurStrength);

        EditorGUILayout.HelpBox(
            "Reads the 11 symbol sprites from SlotView, applies a vertical Gaussian motion blur " +
            "with premultiplied alpha (no dark halos), saves to Assets/SlL-FF/Art/Art/SpritsBlur/, " +
            "matches source TextureImporter settings, and assigns them to SlotView.blurSymbolSprites.",
            MessageType.Info
        );

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate and Assign Blurred Symbols", GUILayout.Height(35)))
        {
            GenerateAll(blurStrength);
        }
    }

    public static void GenerateAll(float strength = 0.18f)
    {
        var slotView = Object.FindFirstObjectByType<SlotView>();
        if (slotView == null)
        {
            Debug.LogError("[SymbolBlurGenerator] SlotView not found in active scene!");
            return;
        }

        var so = new SerializedObject(slotView);
        string[] spriteFieldNames = {
            "spriteDiamondShot", "spriteDiamond", "spritePilot", "spriteSeven3",
            "spriteBonus", "spriteSeven2", "spriteSeven", "spriteScout",
            "spriteBar5", "spriteBar3", "spriteBar"
        };

        Sprite[] sourceSprites = new Sprite[spriteFieldNames.Length];
        for (int i = 0; i < spriteFieldNames.Length; i++)
        {
            var prop = so.FindProperty(spriteFieldNames[i]);
            sourceSprites[i] = prop != null ? prop.objectReferenceValue as Sprite : null;
            if (sourceSprites[i] == null)
            {
                Debug.LogError($"[SymbolBlurGenerator] Sprite field '{spriteFieldNames[i]}' (ID {i}) is null on SlotView!");
                return;
            }
        }

        string outputFolder = "Assets/SlL-FF/Art/Art/SpritsBlur";
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        Sprite[] generatedSprites = new Sprite[11];

        for (int i = 0; i < sourceSprites.Length; i++)
        {
            Sprite srcSprite = sourceSprites[i];
            EditorUtility.DisplayProgressBar("Generating Blurred Symbols", $"Processing ID {i}: {srcSprite.name}...", (float)i / sourceSprites.Length);

            Texture2D sourceTex = CopySpriteViaRenderTexture(srcSprite);
            if (sourceTex == null)
            {
                Debug.LogError($"[SymbolBlurGenerator] Failed to read pixels for sprite {srcSprite.name}");
                continue;
            }

            Texture2D blurredTex = ApplyVerticalGaussianBlur(sourceTex, strength);
            Object.DestroyImmediate(sourceTex);

            string destPath = $"{outputFolder}/{srcSprite.name}_blur.png";
            byte[] pngData = blurredTex.EncodeToPNG();
            Object.DestroyImmediate(blurredTex);

            File.WriteAllBytes(destPath, pngData);

            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

            // Copy source TextureImporter settings
            string srcPath = AssetDatabase.GetAssetPath(srcSprite);
            TextureImporter srcImporter = AssetImporter.GetAtPath(srcPath) as TextureImporter;
            TextureImporter dstImporter = AssetImporter.GetAtPath(destPath) as TextureImporter;

            if (dstImporter != null && srcImporter != null)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                srcImporter.ReadTextureSettings(settings);
                dstImporter.SetTextureSettings(settings);
                dstImporter.maxTextureSize = srcImporter.maxTextureSize;
                dstImporter.textureCompression = srcImporter.textureCompression;
                dstImporter.SaveAndReimport();
            }

            generatedSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        // Assign to SlotView.blurSymbolSprites in ID order
        Undo.RecordObject(slotView, "Assign Blurred Symbol Sprites");
        var blurProp = so.FindProperty("blurSymbolSprites");
        blurProp.arraySize = 11;
        for (int i = 0; i < 11; i++)
        {
            blurProp.GetArrayElementAtIndex(i).objectReferenceValue = generatedSprites[i];
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(slotView);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"<color=green>[SymbolBlurGenerator] Successfully generated and assigned 11 blurred sprites to SlotView.blurSymbolSprites!</color>");
    }

    private static Texture2D CopySpriteViaRenderTexture(Sprite sprite)
    {
        Texture2D srcTex = sprite.texture;
        RenderTexture rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;

        Graphics.Blit(srcTex, rt);

        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        copy.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return copy;
    }

    private static Texture2D ApplyVerticalGaussianBlur(Texture2D source, float strengthRatio)
    {
        int w = source.width;
        int h = source.height;

        Color[] pixels = source.GetPixels();
        float sigma = h * strengthRatio;
        int radius = Mathf.CeilToInt(2.0f * sigma);

        // Precompute 1D Gaussian kernel
        float[] kernel = new float[2 * radius + 1];
        float kernelSum = 0f;
        for (int i = -radius; i <= radius; i++)
        {
            float val = Mathf.Exp(-(i * i) / (2f * sigma * sigma));
            kernel[i + radius] = val;
            kernelSum += val;
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= kernelSum;

        // Convert source to premultiplied alpha in column-major order [x * h + y]
        float[] premulR = new float[w * h];
        float[] premulG = new float[w * h];
        float[] premulB = new float[w * h];
        float[] alpha = new float[w * h];

        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                Color c = pixels[rowOffset + x];
                int colIdx = x * h + y;
                premulR[colIdx] = c.r * c.a;
                premulG[colIdx] = c.g * c.a;
                premulB[colIdx] = c.b * c.a;
                alpha[colIdx]   = c.a;
            }
        }

        // Apply 1D vertical convolution in parallel over columns
        float[] outR = new float[w * h];
        float[] outG = new float[w * h];
        float[] outB = new float[w * h];
        float[] outA = new float[w * h];

        Parallel.For(0, w, x =>
        {
            int colOffset = x * h;
            for (int y = 0; y < h; y++)
            {
                float rAcc = 0f, gAcc = 0f, bAcc = 0f, aAcc = 0f;
                int kMin = Mathf.Max(-radius, -y);
                int kMax = Mathf.Min(radius, h - 1 - y);

                for (int k = kMin; k <= kMax; k++)
                {
                    float wgt = kernel[k + radius];
                    int sampleIdx = colOffset + y + k;
                    rAcc += premulR[sampleIdx] * wgt;
                    gAcc += premulG[sampleIdx] * wgt;
                    bAcc += premulB[sampleIdx] * wgt;
                    aAcc += alpha[sampleIdx]   * wgt;
                }

                int outIdx = colOffset + y;
                outR[outIdx] = rAcc;
                outG[outIdx] = gAcc;
                outB[outIdx] = bAcc;
                outA[outIdx] = aAcc;
            }
        });

        // Un-premultiply and assemble output Texture2D in row-major order
        Color[] outPixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                int colIdx = x * h + y;
                float a = outA[colIdx];
                if (a > 0.0001f)
                {
                    float invA = 1f / a;
                    outPixels[rowOffset + x] = new Color(
                        Mathf.Clamp01(outR[colIdx] * invA),
                        Mathf.Clamp01(outG[colIdx] * invA),
                        Mathf.Clamp01(outB[colIdx] * invA),
                        Mathf.Clamp01(a)
                    );
                }
                else
                {
                    outPixels[rowOffset + x] = Color.clear;
                }
            }
        }

        Texture2D blurred = new Texture2D(w, h, TextureFormat.RGBA32, false);
        blurred.SetPixels(outPixels);
        blurred.Apply();

        return blurred;
    }
}
