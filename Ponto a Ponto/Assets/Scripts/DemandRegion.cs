using System.Collections.Generic;
using UnityEngine;

public class DemandRegion : MonoBehaviour
{
    [Header("Identificação e progressão")]
    [Min(1)]
    public int regionId = 1;
    public string regionName = "Bairro";
    [Min(1)]
    public int activationDay = 1;
    [Header("Demanda e cobertura")]
    [Min(1)]
    public int requestsPerDay = 12;
    [Min(0.2f)]
    public float walkingRadius = 3f;
    public Color regionColor = new Color(0.2f, 0.8f, 1f, 1f);
    public string Label => "R" + regionId.ToString("00");
    public Color DestinationColor => regionColor;
    public int Id => regionId;
    public readonly PontoRegionDemand Demand = new PontoRegionDemand();
    public readonly List<PassengerData> Pending = new List<PassengerData>();
    public float SpawnTimer;
    private LineRenderer boundary;
    private TextMesh marker;
    private float refreshTimer;
    private Material ownedMaterial;
    public bool IsActive(int day) => day >= activationDay;
    public bool Covers(Vector3 point)
    {
        Vector2 delta = point - transform.position;
        return delta.sqrMagnitude <= walkingRadius * walkingRadius;
    }

    private void Start()
    {
        var ring = new GameObject("Area_de_atendimento");
        ring.transform.SetParent(transform, false);
        boundary = ring.AddComponent<LineRenderer>();
        boundary.useWorldSpace = true;
        boundary.loop = true;
        boundary.startWidth = boundary.endWidth = 0.045f;
        boundary.sortingOrder = 2;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null)
        {
            ownedMaterial = new Material(shader);
            boundary.sharedMaterial = ownedMaterial;
        }

        var text = new GameObject("Regiao");
        text.transform.SetParent(transform, false);
        marker = text.AddComponent<TextMesh>();
        marker.anchor = TextAnchor.MiddleCenter;
        marker.alignment = TextAlignment.Center;
        marker.fontSize = 40;
        marker.characterSize = 0.09f;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        marker.font = font;
        var renderer = marker.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        renderer.sortingOrder = 10;
        RefreshMarker();
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0)
        {
            refreshTimer = 0.3f;
            RefreshMarker();
        }
    }

    private void RefreshMarker()
    {
        if (boundary == null || marker == null)
            return;
        var game = PontoGame.Instance;
        int day = game == null ? 1 : game.DisplayDay;
        bool active = IsActive(day);
        Color c = active ? regionColor : Color.gray;
        c.a = active ? 0.45f : 0.2f;
        boundary.startColor = boundary.endColor = c;
        boundary.positionCount = 48;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            boundary.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * walkingRadius);
        }

        marker.transform.position = transform.position + new Vector3(0, walkingRadius + 0.5f, -0.02f);
        marker.color = active ? regionColor : Color.gray;
        bool covered = game != null && game.FindStopFor(this) != null;
        marker.text = Label + " " + regionName + "\n" + (active ? (covered ? "Com ponto" : "SEM PONTO") + " | " + (game != null ? game.RequestsForDay(this, day) : requestsPerDay) + " viagens/dia" : "Disponível no dia " + activationDay);
    }

    private void OnDestroy()
    {
        if (ownedMaterial != null)
            Destroy(ownedMaterial);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = regionColor;
        Gizmos.DrawWireSphere(transform.position, walkingRadius);
    }
}
