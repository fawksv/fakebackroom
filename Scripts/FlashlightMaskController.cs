using UnityEngine;

public class FlashlightMaskController : MonoBehaviour
{
    [Header("Mask Settings")]
    public float darkness = 0.98f;
    public float radius = 0.2f;
    public float softness = 0.05f;
    public float followSmoothness = 10f;

    private Material maskMaterial;
    private GameObject maskQuad;
    private Camera mainCam;
    private Vector2 currentScreenPos;
    private bool initialized;

    void Start()
    {
        mainCam = Camera.main;
        Shader maskShader = Shader.Find("Custom/FlashlightMask");
        if (maskShader == null)
        {
            Debug.LogError("FlashlightMask shader not found!");
            return;
        }

        maskMaterial = new Material(maskShader);
        maskMaterial.hideFlags = HideFlags.HideAndDontSave;

        maskQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        maskQuad.name = "FlashlightMaskQuad";
        DestroyImmediate(maskQuad.GetComponent<Collider>());

        MeshRenderer mr = maskQuad.GetComponent<MeshRenderer>();
        mr.material = maskMaterial;
        mr.sortingOrder = 100;

        maskQuad.transform.SetParent(transform);
        maskQuad.SetActive(false);

        initialized = true;
    }

    void Update()
    {
        if (!initialized || mainCam == null) return;
        if (!maskQuad.activeSelf) return;

        Vector3 flashlightWorldPos = mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, mainCam.nearClipPlane + 0.1f));
        Vector3 screenPos = mainCam.WorldToViewportPoint(flashlightWorldPos);

        Vector2 target = new Vector2(screenPos.x, screenPos.y);
        currentScreenPos = Vector2.Lerp(currentScreenPos, target, followSmoothness * Time.deltaTime);

        maskMaterial.SetVector("_FlashlightPos", new Vector4(currentScreenPos.x, currentScreenPos.y, 0, 0));
        maskMaterial.SetFloat("_FlashlightRadius", radius);
        maskMaterial.SetFloat("_FlashlightSoftness", softness);
        maskMaterial.SetFloat("_Darkness", darkness);

        PositionQuadInFrontOfCamera();
    }

    void PositionQuadInFrontOfCamera()
    {
        if (mainCam == null) return;

        float dist = mainCam.nearClipPlane + 0.05f;
        maskQuad.transform.position = mainCam.transform.position + mainCam.transform.forward * dist;
        maskQuad.transform.rotation = mainCam.transform.rotation;
        maskQuad.transform.localScale = new Vector3(2f, 2f, 1f);
    }

    public void ShowMask()
    {
        if (maskQuad != null)
        {
            maskQuad.SetActive(true);
            if (mainCam != null)
            {
                Vector3 screenPos = mainCam.WorldToViewportPoint(
                    mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, mainCam.nearClipPlane + 0.1f)));
                currentScreenPos = new Vector2(screenPos.x, screenPos.y);
            }
        }
    }

    public void HideMask()
    {
        if (maskQuad != null)
            maskQuad.SetActive(false);
    }

    void OnDestroy()
    {
        if (maskMaterial != null)
            DestroyImmediate(maskMaterial);
        if (maskQuad != null)
            DestroyImmediate(maskQuad);
    }
}
