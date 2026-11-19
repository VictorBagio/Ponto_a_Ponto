using System.Collections.Generic;
using UnityEngine;

public class BusStop : MonoBehaviour
{
    public Vector3Int Cell { get; private set; }

    public int Id { get; private set; }

    public Color DestinationColor { get; private set; }

    public readonly List<PassengerData> Waiting = new List<PassengerData>();
    public string Label => "P" + Id.ToString("00");
    private TextMesh label;
    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
    private static Sprite markerSprite;
    private Font worldFont;
    private string lastLabel;
    private const int VisiblePassengers = 8;
    public void Configure(Vector3Int cell, int id)
    {
        Cell = cell;
        Id = id;
        DestinationColor = Color.HSVToRGB(Mathf.Repeat(id * 0.618034f, 1f), 0.65f, 1f);
        var textObject = new GameObject("Identificacao");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0.68f, -0.01f);
        label = textObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 40;
        label.characterSize = 0.065f;
        worldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.font = worldFont;
        var textRenderer = label.GetComponent<MeshRenderer>();
        textRenderer.sharedMaterial = worldFont.material;
        textRenderer.sortingOrder = 12;
        label.color = DestinationColor;
        if (markerSprite == null)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            markerSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
        }

        for (int i = 0; i < VisiblePassengers; i++)
        {
            var obj = new GameObject("Passageiro_" + i);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = new Vector3(-0.28f + (i % 4) * 0.18f, -0.55f - (i / 4) * 0.18f, 0);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.sortingOrder = 11;
            markers.Add(renderer);
        }

        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (label != null)
        {
            string value = Label + " [" + Waiting.Count + "]";
            if (value != lastLabel)
            {
                label.text = value;
                lastLabel = value;
            }
        }

        for (int i = 0; i < markers.Count; i++)
        {
            markers[i].enabled = i < Waiting.Count;
            if (i < Waiting.Count)
                markers[i].color = Waiting[i].destination != null ? Waiting[i].destination.DestinationColor : Color.gray;
        }
    }
}
